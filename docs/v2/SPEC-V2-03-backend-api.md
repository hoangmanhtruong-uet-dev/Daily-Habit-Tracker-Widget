# SPEC-V2-03 — Backend & API (`HabitGrid.Server`)

> Phát hành: [v2.1] (tài khoản, sync, entitlement), [v2.2] (social, AI) • Phụ thuộc: SPEC-V2-01, Contracts

## 1. Công nghệ & hạ tầng
| Thành phần | Lựa chọn | Lý do |
|---|---|---|
| Runtime | ASP.NET Core (.NET 10) Minimal API | Dùng chung Core + Contracts với client |
| DB | PostgreSQL 16+ (EF Core + Npgsql, migration bằng code) | JSONB cho field_hlc, đáng tin, rẻ |
| Realtime | SignalR (WebSocket) | Báo "có thay đổi" để client pull ngay |
| Cache/rate limit | Bộ nhớ trong tiến trình ở giai đoạn đầu; Redis khi > 1 instance | Tránh chi phí sớm |
| Lưu file | Azure Blob / S3-compatible | Avatar, share card |
| Hàng đợi việc nền | Bảng `jobs` trong Postgres + worker (giai đoạn đầu) | Đơn giản |
| Push | Windows Push Notification Service (WNS); APNs/FCM ở giai đoạn mobile | |
| Hosting | **Đề xuất:** Azure Container Apps (scale về 0–1 lúc đầu) + Azure Database for PostgreSQL Flexible (tier nhỏ). Phương án rẻ: Fly.io + Neon | Quyết định mở #2 |
| Observability | OpenTelemetry → Azure Monitor / Grafana Cloud | |
| CI/CD | GitHub Actions: test → build image → deploy staging → smoke → prod (duyệt tay) | |

Môi trường: `local` (docker compose), `staging`, `prod`. Secrets trong Key Vault / secret store của nền tảng, không bao giờ trong repo.

## 2. Xác thực & tài khoản
- Đăng nhập: **Microsoft account**, **Google**, và **email magic link** (mã 6 số, hết hạn 10 phút). Apple Sign In thêm khi có app iOS.
- Luồng desktop: OAuth 2.0 Authorization Code + **PKCE** qua trình duyệt hệ thống, redirect về `http://127.0.0.1:<port ngẫu nhiên>` (loopback). Không dùng WebView nhúng.
- Server phát **access token JWT 15 phút** + **refresh token 60 ngày, xoay vòng**, phát hiện tái sử dụng (dùng lại refresh cũ → thu hồi cả chuỗi).
- Client lưu refresh token bằng **Windows Credential Locker** (`PasswordVault`), không lưu trong SQLite.
- Mỗi thiết bị đăng ký `device_id`, tên máy, nền tảng; người dùng xem và thu hồi thiết bị trong Cài đặt.
- Xóa tài khoản: xóa mềm ngay (không đăng nhập được), xóa cứng toàn bộ dữ liệu sau 7 ngày (job), có email xác nhận.

## 3. Mô hình dữ liệu server (rút gọn)
```
users(id, email, email_verified, display_name, avatar_url, locale, timezone, created_at, deleted_at)
identities(user_id, provider, provider_subject, UNIQUE(provider, provider_subject))
devices(id, user_id, name, platform, app_version, wns_channel, last_seen_at, revoked_at)
refresh_tokens(id, user_id, device_id, hash, family_id, expires_at, used_at, revoked_at)

-- Dữ liệu đồng bộ: mỗi bảng có user_id, id, data JSONB, field_hlc JSONB, deleted_at, server_seq BIGINT
habits, reminders, day_logs(user_id, habit_id, day, ...), routines, profile_kv, xp_events, achievements
sync_ops_applied(user_id, client_op_id, applied_at)   -- idempotency, dọn sau 30 ngày
user_seq(user_id, last_seq)                           -- cấp server_seq tăng dần theo user

entitlements(user_id, product, source, expires_at, store_ref, verified_at)
-- social [v2.2]
friendships(user_a, user_b, status, created_at)  invites(code, user_id, expires_at)
challenges(id, owner_id, name, habit_template_json, start, end, visibility)
challenge_members(challenge_id, user_id, habit_id, joined_at)
share_events(id, user_id, kind, created_at)      nudges(from, to, day)
ai_usage(user_id, month, requests, tokens_in, tokens_out)
```
Ghi chú (`day_logs.data.note`) và mood được **mã hóa cấp ứng dụng** (AES-GCM, khóa theo user bọc bởi KMS) trước khi lưu.

## 4. API (tiền tố `/v1`, JSON, camelCase)

### 4.1 Auth
| Method | Path | Mô tả |
|---|---|---|
| POST | `/auth/oauth/{provider}/exchange` | Đổi code + verifier → token |
| POST | `/auth/email/start` | Gửi mã |
| POST | `/auth/email/verify` | Xác minh mã → token |
| POST | `/auth/refresh` | Xoay refresh token |
| POST | `/auth/logout` | Thu hồi refresh của thiết bị |
| GET/DELETE | `/me`, `/me/devices`, `/me/devices/{id}` | Hồ sơ, thiết bị |
| DELETE | `/me` | Xóa tài khoản |
| GET | `/me/export` | Xuất toàn bộ dữ liệu (JSON, job nền, gửi link) |

### 4.2 Sync
```
POST /sync/push
{ "deviceId": "...", "ops": [ { "clientOpId": "dev:123", "entity": "dayLog",
    "key": {"habitId":"...","day":"2026-11-05"}, "op": "upsert",
    "fields": {"value": {"v": 1, "hlc": "..."}, "note": {"v": "...", "hlc": "..."}},
    "baseHlc": "..." } ] }
→ { "accepted": ["dev:123"], "rejected": [ {"clientOpId": "...", "code": "validation", "detail": "..."} ],
    "serverSeq": 4521 }

GET /sync/pull?cursor=4400&limit=1000
→ { "changes": [ { "entity": "...", "key": {...}, "data": {...}, "fieldHlc": {...}, "deletedAt": null, "seq": 4401 } ],
    "nextCursor": 4521, "hasMore": false }
```
- Server áp dụng cùng `MergeEngine` của Core (một mã nguồn, hai nơi chạy).
- Idempotent theo `clientOpId`. Mỗi lần ghi tăng `server_seq` của user và đẩy tín hiệu SignalR `changed(seq)` tới các thiết bị khác của user.
- Giới hạn: 500 op/request, 60 request/phút/thiết bị; payload ≤ 1 MB.
- Validation server-side lặp lại `HabitValidator` của Core; vượt giới hạn gói (số habit active) → reject mã `entitlement` (client hiển thị paywall, dữ liệu cục bộ vẫn giữ).

### 4.3 Entitlement
| Method | Path | Mô tả |
|---|---|---|
| POST | `/entitlements/store/verify` | Client gửi Store ID key / receipt; server gọi Microsoft Store Collections/Purchase API xác minh, ghi `entitlements` |
| GET | `/entitlements` | Quyền hiện tại (client cache, hạn 7 ngày offline) |

### 4.4 Social [v2.2] — xem SPEC-V2-08
`/friends`, `/invites`, `/challenges`, `/nudges`, `/share-cards`.

### 4.5 AI [v2.2] — xem SPEC-V2-07
`POST /ai/weekly-summary`, `POST /ai/coach` (stream SSE). Server giữ API key; client không bao giờ gọi trực tiếp nhà cung cấp AI.

### 4.6 Quy ước chung
- Lỗi theo RFC 9457 (`application/problem+json`) với `code` ổn định để client dịch.
- Header `X-App-Version`, `X-Device-Id`; server trả `426` khi phiên bản client quá cũ không còn tương thích.
- OpenAPI sinh tự động, công bố ở staging; client DTO nằm trong `HabitGrid.Contracts` (không sinh lại từ OpenAPI để tránh lệch).

## 5. Bảo mật & quyền riêng tư
- TLS bắt buộc, HSTS. CORS đóng (không có web client ở v2).
- Mọi truy vấn lọc theo `user_id` lấy từ token; test tự động đảm bảo không endpoint nào trả dữ liệu user khác (IDOR test).
- Rate limit theo IP cho auth (5 lần gửi mã/giờ/email).
- Log không chứa email đầy đủ, ghi chú, mood, token.
- Sao lưu Postgres tự động hằng ngày, giữ 14 ngày, diễn tập khôi phục mỗi quý.
- Tuân thủ: chính sách riêng tư cập nhật (thu thập gì, lưu ở đâu, xóa thế nào); xuất dữ liệu và xóa tài khoản tự phục vụ (đáp ứng GDPR, Nghị định 13/2023 của VN).

## 6. Vận hành & chi phí
- Health: `/health/live`, `/health/ready`. Cảnh báo: 5xx > 1%, p95 > 800 ms, DB CPU > 80%.
- Ước lượng tải đầu: 10.000 user hoạt động × ~20 request/ngày ≈ 200.000 request/ngày. Mức này chạy được trên 1 instance nhỏ.
- Dashboard chi phí hằng tháng; đặt ngân sách cảnh báo.

## 7. Tiêu chí nghiệm thu
1. Integration test (Testcontainers Postgres) cho mọi endpoint, gồm IDOR và idempotency.
2. Sync: test fuzz của SPEC-V2-02 chạy với server thật trong container → hội tụ.
3. Đăng nhập Microsoft/Google/email từ app desktop thành công; refresh xoay vòng; tái sử dụng refresh bị thu hồi.
4. Xóa tài khoản: sau job, không còn dòng dữ liệu nào của user (kiểm bằng truy vấn).
5. Tải: 200 request/giây sync pull, p95 < 300 ms trên cấu hình staging.
6. Pen-test nhẹ (OWASP ASVS L1 checklist) trước khi mở v2.1 công khai.
