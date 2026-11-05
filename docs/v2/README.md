# HabitGrid v2 — Bộ đặc tả xây dựng lại (A → Z)

> Ngày lập: 05/11/2026 • Thay thế định hướng của `docs/specs/` (v1)
> Quyết định nền đã chốt: **local-first + cloud sync tùy chọn**, **Windows trước, mobile sau**, **UI viết lại bằng WinUI 3**, tài liệu dạng gói SPEC để giao cho AI coding.

## 1. Đọc theo thứ tự nào

| # | File | Nội dung | Ai cần đọc |
|---|---|---|---|
| 0 | [00-MASTER-PLAN.md](00-MASTER-PLAN.md) | Tầm nhìn, so sánh đối thủ, khoảng trống của v1, phạm vi, roadmap, KPI, rủi ro | Tất cả, đọc trước |
| 1 | [01-DESIGN-SYSTEM.md](01-DESIGN-SYSTEM.md) | Thương hiệu, token màu/chữ/khoảng cách, chuyển động, âm thanh, linh vật, component | UI, mọi gói có giao diện |
| 2 | [02-UX-SCREENS.md](02-UX-SCREENS.md) | Đặc tả từng màn hình và luồng người dùng | UI, QA |
| 3 | [SPEC-V2-01-domain.md](SPEC-V2-01-domain.md) | Domain v2: loại thói quen mới, tần suất linh hoạt, streak freeze, ghi chú, mood | Core |
| 4 | [SPEC-V2-02-local-data-sync-client.md](SPEC-V2-02-local-data-sync-client.md) | SQLite schema v3, migration từ v1, outbox, engine đồng bộ phía máy khách | Data |
| 5 | [SPEC-V2-03-backend-api.md](SPEC-V2-03-backend-api.md) | Server ASP.NET Core + PostgreSQL: tài khoản, sync API, entitlement, bảo mật | Backend |
| 6 | [SPEC-V2-04-winui-shell.md](SPEC-V2-04-winui-shell.md) | Ứng dụng WinUI 3: kiến trúc, điều hướng, MVVM, chuyển đổi từ WPF | UI |
| 7 | [SPEC-V2-05-widgets.md](SPEC-V2-05-widgets.md) | Widget ghim desktop v2 + Windows Widgets Board + quick check-in | UI, Platform |
| 8 | [SPEC-V2-06-gamification.md](SPEC-V2-06-gamification.md) | XP, level, huy hiệu, linh vật "Grid Buddy", streak freeze, Wrapped | Core, UI |
| 9 | [SPEC-V2-07-insights-ai.md](SPEC-V2-07-insights-ai.md) | Thống kê nâng cao, insight tự động, AI coach | Core, Backend, UI |
| 10 | [SPEC-V2-08-social.md](SPEC-V2-08-social.md) | Bạn bè, buddy, thử thách nhóm, share card | Backend, UI |
| 11 | [SPEC-V2-09-reminders-focus-integrations.md](SPEC-V2-09-reminders-focus-integrations.md) | Nhắc nhở thông minh, Focus timer, phím tắt toàn cục, command palette, lịch | Platform, UI |
| 12 | [SPEC-V2-10-monetization.md](SPEC-V2-10-monetization.md) | Free / Plus (thuê bao + trọn đời), Store subscription, xác minh phía server | Platform, Backend |
| 13 | [SPEC-V2-11-quality-observability.md](SPEC-V2-11-quality-observability.md) | Test, CI, telemetry opt-in, crash report, hiệu năng, trợ năng, i18n | Tất cả |
| 14 | [SPEC-V2-12-release-growth.md](SPEC-V2-12-release-growth.md) | MSIX, ký số, Store listing, ASO, kế hoạch ra mắt cho GenZ | Release, Marketing |
| 15 | [SPEC-V2-13-mobile-companion.md](SPEC-V2-13-mobile-companion.md) | App mobile giai đoạn sau (định hướng, chưa làm chi tiết) | Giai đoạn 4 |

## 2. Thứ tự thực thi và phụ thuộc

```
Giai đoạn 0  POC-V2 (ghim desktop với WinUI) ─┐
                                                ├─► SPEC-V2-01 ─► SPEC-V2-02 ─► SPEC-V2-04 ─► SPEC-V2-05
Design system (01, 02) ────────────────────────┘                                  │
                                                                                   ├─► SPEC-V2-06 ─► SPEC-V2-09
Giai đoạn 2  SPEC-V2-03 (backend) ─► nối sync vào 02 ─► SPEC-V2-10 ────────────────┤
Giai đoạn 3  SPEC-V2-07 ─► SPEC-V2-08 ─► Wrapped (06)                              │
Xuyên suốt   SPEC-V2-11 (chất lượng)  •  Cuối mỗi giai đoạn: SPEC-V2-12 (phát hành)
Giai đoạn 4  SPEC-V2-13 (mobile)
```

## 3. Quy tắc giao việc cho AI coding (giữ từ v1, bổ sung)

1. Mỗi lần chỉ giao **một** gói SPEC, hoặc một mục con được đánh số trong gói.
2. Mỗi prompt yêu cầu đủ 5 bước: đọc SPEC + code liên quan → nêu kế hoạch file → code → `dotnet build` + `dotnet test` → báo cáo theo tiêu chí nghiệm thu.
3. **Mới ở v2:** gói nào có giao diện phải kèm ít nhất một **smoke test dựng XAML thật** và một lần **chạy app thật, chụp màn hình** trước khi báo xong. Bản v1 báo "Go-Live Ready" nhưng sập ngay khi mở vì thiếu bước này.
4. Không gói nào được xóa dữ liệu người dùng. Mọi thay đổi schema đi qua migration có backup.
5. Hợp đồng dùng chung giữa client và server nằm trong `HabitGrid.Contracts`. Sửa hợp đồng phải bump version API.

## 4. Quy ước trong tài liệu

- **MUST / NÊN / CÓ THỂ**: bắt buộc / khuyến nghị mạnh / tùy chọn.
- `[v2.0]`, `[v2.1]`, `[v2.2]`, `[v3]`: tính năng thuộc bản phát hành nào (xem roadmap trong 00).
- Con số có dấu `≈` là ước lượng cần đo lại. Thông tin thị trường đánh dấu `(cần xác minh)` phải được kiểm chứng trước khi dùng cho marketing.
