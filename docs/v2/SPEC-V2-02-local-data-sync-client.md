# SPEC-V2-02 — Dữ liệu cục bộ & engine đồng bộ phía máy (`HabitGrid.Data`, `HabitGrid.Sync`)

> Phát hành: phần A [v2.0], phần B [v2.1] • Phụ thuộc: SPEC-V2-01; phần B cần SPEC-V2-03

## A. Lưu trữ cục bộ [v2.0]

### A.1 Công nghệ
- SQLite (Microsoft.Data.Sqlite), WAL, `foreign_keys=ON`, `busy_timeout=5000`. Giữ cách truy cập SQL thuần + mapping tay như v1 (nhẹ, AOT-friendly), hoặc Dapper. **Không** dùng EF Core ở client.
- Vị trí: **`%LOCALAPPDATA%\HabitGrid\habitgrid-v3.db`**, một file MỚI cạnh `habitgrid.db` của v1 (đóng gói MSIX: `ApplicationData.LocalFolder`). *Điều chỉnh so với bản đầu (05/11/2026): không migrate tại chỗ; xem A.3.*
- Hai tiến trình (app + widget host) cùng truy cập: chỉ **app** ghi; widget ghi thông qua IPC tới app (named pipe), hoặc tự ghi nếu app không chạy (WAL đảm bảo an toàn). Mọi thay đổi phát sự kiện qua pipe để tiến trình còn lại làm mới.

### A.2 Schema v3
```sql
habits(id TEXT PK, name, description, icon, color_hue, kind INT, target_value INT, target_unit,
       frequency_json, time_of_day INT, start_date, ask_mood INT, allow_notes INT, allow_freeze INT,
       routine_id, routine_order INT, tags_json, sort_key TEXT, archived_at, deleted_at,
       created_at, updated_at, hlc TEXT, shared_challenge_id)
reminders(id TEXT PK, habit_id FK, time TEXT, days_mask INT, smart INT, follow_up INT, hlc TEXT, deleted_at)
day_logs(habit_id FK, day TEXT, value INT, status INT, note TEXT, mood INT, checked_at, updated_at, hlc TEXT,
         PRIMARY KEY(habit_id, day))
routines(id PK, name, icon, time_of_day, sort_key, hlc, deleted_at)
profile(key TEXT PK, value TEXT, hlc TEXT)            -- XP, level, buddy, freeze…
xp_events(id PK, kind, amount, occurred_at, habit_id, hlc)
achievements(id PK, unlocked_at, hlc)
widgets(id PK, kind, habit_ids_json, monitor, x REAL, y REAL, size INT, theme_id, opacity REAL, visible INT)  -- không sync (theo máy)
settings(key PK, value)                               -- cục bộ theo máy
sync_outbox(seq INTEGER PK AUTOINCREMENT, entity, entity_id, op, payload_json, hlc, created_at, attempts INT)
sync_state(key PK, value)                             -- cursor, device_id, last_sync_at
schema_info(version PK, applied_at)
```
Index: `day_logs(day)`, `habits(deleted_at, archived_at)`, `sync_outbox(seq)`.

Phân loại dữ liệu: **sync** (habits, reminders, day_logs, routines, profile, xp_events, achievements) và **theo máy** (widgets, settings).

### A.3 Nhập dữ liệu v1/v2 → v3 (`LegacyImporter`)
**Quyết định:** không sửa file v1 tại chỗ mà **dựng file v3 mới từ file v1**. Lý do: file v1 chỉ bị mở đọc nên không bao giờ có thể hỏng; không cần rollback; v1 và v2 có thể cùng tồn tại khi chuyển tiếp; mất điện giữa chừng không để lại trạng thái nửa vời.
1. Mở `habitgrid.db` bằng kết nối **chỉ đọc** (nhận cả CSDL v1 chưa có cột `read_only`).
2. Ánh xạ bằng `V1Mapper` (xem SPEC-V2-01) rồi ghi vào file tạm `habitgrid-v3.db.importing` trong một transaction: `entries` → `day_logs` (status suy ra); `schedule_mask` → `frequency_json` (127 → Daily, khác → WeekDays); `palette_id`/`custom_color` → `color_hue`; `sort_order` → `sort_key` fractional giữ đúng thứ tự cũ; `widget_state` → `widgets`; `reminder_time/mask` → `reminders`; `settings` sao chép nguyên.
3. Mọi bản ghi nhận một HLC và một dòng `sync_outbox` (sẵn sàng đẩy lên cloud ở v2.1). Ghi `sync_state`: `import.legacy_at`, `import.legacy_path`, `device_id`.
4. `wal_checkpoint(TRUNCATE)` rồi `File.Move` nguyên tử sang `habitgrid-v3.db`.
5. **Bất biến an toàn:** chỉ thay file đích khi nó chắc chắn là CSDL v3 của HabitGrid còn trống và chưa nhập (kiểm bằng kết nối chỉ đọc, không tạo bảng). File lạ, kể cả SQLite của app khác, không bao giờ bị ghi đè. Chạy lại là no-op (`target.initialized`).
6. Hủy/lỗi: xóa file tạm; không bao giờ có DB v3 nửa vời. File tạm còn sót từ lần crash trước được thay.

`MigrationRunner` sao lưu theo tên file DB (`{tên file}.bak-v{n}`) và bỏ qua khi DB còn trống (version 0). Trước đây tên cố định `habitgrid.db.bak-v{n}` có thể đè lên bản sao lưu v1 khi có thêm một DB khác cùng thư mục.

### A.4 Repository
Giữ interface trong Core (`IHabitRepository`, `IDayLogRepository`, `IRoutineRepository`, `IProfileRepository`…). Mọi hàm ghi MUST:
1. Gán `UpdatedAt` + `hlc = clock.Now()`.
2. Ghi bản ghi và một dòng `sync_outbox` **trong cùng transaction**.
3. Phát `DataChanged(entity, id)` lên `IDataChangeBus`.

### A.4b Ghi chú triển khai (đã làm)
- Interface (`IHabitRepository`, `IDayLogRepository`, `IRoutineRepository`, `IProfileRepository`, `IDataChangeBus`) nằm ở `HabitGrid.Core.Domain`; hiện thực ở `HabitGrid.Data.V3`. Repository v1 và `habitgrid.db` được giữ nguyên cho tới khi bỏ app WPF.
- Payload outbox hiện là **ảnh chụp toàn bộ thực thể** (nhắc nhở nằm trong payload của habit). Diff theo từng trường kèm HLC từng trường (B.1) sẽ làm cùng `MergeEngine` ở phần B.
- Xóa habit là tombstone (`deleted_at`); `PurgeDeletedAsync` chỉ xóa hẳn khi quá hạn và không còn thay đổi nào của habit đó trong outbox.
- Lưu ý NativeAOT: serialization JSON dùng reflection (`DomainJson`). Nếu sau này bật NativeAOT cho app thì cần chuyển sang source generator.

### A.5 Sao lưu
- Tự động: mỗi ngày 1 bản snapshot JSON nén (`.hgbackup`, zip + manifest có `schema_version`) vào `Backups\`, giữ 14 bản. Free có, vì đây là dữ liệu của người dùng.
- Xuất/nhập thủ công JSON/CSV (giữ định dạng v1 khi nhập để tương thích).
- Bản Plus: chọn thư mục đích (ví dụ thư mục OneDrive).

## B. Engine đồng bộ [v2.1]

### B.1 Mô hình
- **Local-first, op-based push + state-based pull.**
- Mỗi bản ghi mang `hlc` (Hybrid Logical Clock: `wallMs:counter:deviceId`, so sánh theo thứ tự từ điển).
- Hợp nhất **theo từng trường** với Last-Writer-Wins dựa trên hlc của trường. Để làm được, payload outbox chứa *các trường thay đổi* kèm hlc từng trường; server lưu `field_hlc_json` cho mỗi bản ghi.
- Ngoại lệ hợp nhất cho `day_logs.value` khi hai thiết bị cùng sửa trong cùng ngày logic:
  - Boolean/Count/Duration: lấy **max** (không bao giờ làm mất check-in), trừ khi một bên là thao tác "bỏ check" có hlc mới hơn **và** cùng thiết bị đã thấy giá trị bên kia (dùng `base_hlc`), để hoàn tác vẫn đúng.
  - Note: LWW; nếu cả hai khác rỗng và khác nhau, ghép `A\n---\nB` và đánh dấu để người dùng xem.
- Xóa: tombstone (`deleted_at` + hlc), thắng mọi sửa đổi có hlc cũ hơn.

### B.2 Vòng đời sync
```
Kích hoạt: mở app • mỗi 5 phút khi app mở • sau thao tác ghi (debounce 2 s) • tín hiệu SignalR từ server • mạng có lại
1. Push: lấy tối đa 500 dòng outbox theo seq → POST /v1/sync/push (kèm client_op_id = device_id:seq)
   → server trả danh sách accepted/rejected → xóa dòng accepted
2. Pull: GET /v1/sync/pull?cursor=<server_seq> → nhận thay đổi > cursor (phân trang 1000)
   → merge vào SQLite trong transaction (không tạo outbox cho thay đổi đến từ server)
   → lưu cursor mới → phát DataChanged
3. Lặp tới khi hết. Lỗi mạng: thử lại backoff mũ (1 s → 5 phút, có jitter). 401: refresh token; refresh hỏng → trạng thái "cần đăng nhập lại", không mất dữ liệu.
```

### B.3 Lần đăng nhập đầu tiên trên máy đã có dữ liệu
- Tài khoản mới, máy có dữ liệu: đẩy toàn bộ lên.
- Tài khoản đã có dữ liệu, máy cũng có dữ liệu: hiện lựa chọn **Gộp (khuyến nghị)** / Dùng dữ liệu cloud / Dùng dữ liệu máy này. Gộp = merge theo B.1; habit trùng tên + cùng loại được đề xuất nhập làm một (người dùng xác nhận).
- Luôn sao lưu cục bộ trước khi gộp/thay thế.

### B.4 Đăng xuất
Hỏi: giữ dữ liệu trên máy (mặc định) hoặc xóa khỏi máy. Không bao giờ xóa dữ liệu trên server khi đăng xuất.

### B.5 Thành phần
| Lớp | Việc |
|---|---|
| `HlcClock` | Sinh/nhận hlc, chịu lệch đồng hồ ≤ 1 giờ, cảnh báo nếu lớn hơn |
| `OutboxReader` | Đọc, gộp nhiều op cùng bản ghi thành 1 trước khi gửi |
| `SyncClient` | HTTP + SignalR, dùng DTO trong `HabitGrid.Contracts` |
| `MergeEngine` | Hàm thuần: `(local, remote) → merged`, test kỹ |
| `SyncCoordinator` | Lịch chạy, trạng thái (`Idle/Syncing/Offline/NeedsLogin/Error`) cho UI |

## C. Tiêu chí nghiệm thu
1. Migrate v1 → v3 trên fixture: 100% habit/entry giữ nguyên, streak bằng v1, widget giữ vị trí.
2. Mất điện giữa migration (giết tiến trình ở bước ngẫu nhiên, 50 lần): DB luôn ở trạng thái v1 hoặc v3 hợp lệ.
3. Fuzz sync: 3 thiết bị giả lập, 10.000 thao tác ngẫu nhiên, mạng chập chờn, thứ tự giao ngẫu nhiên → mọi thiết bị hội tụ cùng trạng thái; không check-in nào bị mất (trừ bỏ check hợp lệ).
4. Ghi một check-in: phản hồi UI < 16 ms (ghi DB nền), sync tới máy khác < 5 s khi cả hai online.
5. Offline 30 ngày rồi online: đồng bộ thành công, không trùng bản ghi.
