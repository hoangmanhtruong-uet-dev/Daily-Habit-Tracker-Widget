# Gói Công Việc 02: Tầng Lưu Trữ & Dịch Vụ Sao Lưu (SPEC-02)

> **Mã công việc**: `SPEC-02-DATA`  
> **Thư viện mục tiêu**: `HabitGrid.Data` (.NET 8, tham chiếu `HabitGrid.Core`, `Microsoft.Data.Sqlite`, `System.Text.Json`)  
> **Dự án kiểm thử**: `HabitGrid.Data.Tests` (Kiểm thử với SQLite In-Memory)  
> **Mục tiêu**: Hiện thực hóa hệ thống cơ sở dữ liệu cục bộ SQLite, cơ chế di trú (Migrations) có phiên bản, Repository pattern mỏng tối ưu hiệu năng, và bộ công cụ sao lưu / phục hồi dữ liệu (JSON & CSV) với hai chế độ Gộp (Merge) và Thay thế (Replace).

---

## 1. THIẾT KẾ CƠ SỞ DỮ LIỆU & LƯỢC ĐỒ (SCHEMA)

Đường dẫn cơ sở dữ liệu: `%LocalAppData%\HabitGrid\habitgrid.db`.  
Cấu hình SQLite bắt buộc khi mở kết nối:
```sql
PRAGMA journal_mode = WAL;
PRAGMA foreign_keys = ON;
PRAGMA synchronous = NORMAL;
```

### 1.1 Chi tiết bảng (Bản Migration v1)

```sql
-- 1. Bảng thói quen
CREATE TABLE habits (
    id            TEXT PRIMARY KEY,              -- Guid (Format "D")
    name          TEXT NOT NULL,                 -- Tên thói quen (<= 40 ký tự)
    icon          TEXT NOT NULL,                 -- Key icon Fluent
    kind          INTEGER NOT NULL,              -- 0: Boolean, 1: Count
    target        INTEGER NOT NULL DEFAULT 1,    -- Mục tiêu/ngày
    schedule_mask INTEGER NOT NULL DEFAULT 127,  -- Bitmask thứ 2 -> CN (127: cả tuần)
    palette_id    TEXT NOT NULL DEFAULT 'default',
    custom_color  TEXT NULL,                     -- Mã Hex #RRGGBB (Pro)
    start_date    TEXT NOT NULL,                 -- ISO-8601 Date (yyyy-MM-dd)
    reminder_time TEXT NULL,                     -- HH:mm
    reminder_mask INTEGER NOT NULL DEFAULT 127,
    sort_order    INTEGER NOT NULL DEFAULT 0,
    archived_at   TEXT NULL,                     -- ISO-8601 UTC timestamp
    deleted_at    TEXT NULL,                     -- ISO-8601 UTC timestamp (Soft delete)
    created_at    TEXT NOT NULL                  -- ISO-8601 UTC timestamp
);

CREATE INDEX idx_habits_sort ON habits(sort_order);
CREATE INDEX idx_habits_status ON habits(deleted_at, archived_at);

-- 2. Bảng nhật ký hoàn thành (Entries)
CREATE TABLE entries (
    habit_id   TEXT NOT NULL REFERENCES habits(id) ON DELETE CASCADE,
    day        TEXT NOT NULL,                    -- Ngày logic (yyyy-MM-dd)
    value      INTEGER NOT NULL,                 -- Số lượng hoặc 1/0
    updated_at TEXT NOT NULL,                    -- ISO-8601 UTC timestamp
    PRIMARY KEY (habit_id, day)
);

CREATE INDEX idx_entries_day ON entries(day);

-- 3. Bảng trạng thái Widget trên màn hình
CREATE TABLE widget_state (
    habit_id  TEXT PRIMARY KEY REFERENCES habits(id) ON DELETE CASCADE,
    monitor   TEXT NOT NULL,                     -- Tên thiết bị hiển thị (DisplayDevice)
    x         REAL NOT NULL,                     -- Tọa độ tương đối X (0.0 -> 1.0)
    y         REAL NOT NULL,                     -- Tọa độ tương đối Y (0.0 -> 1.0)
    size      INTEGER NOT NULL DEFAULT 1,        -- 0: Small, 1: Medium, 2: Large
    visible   INTEGER NOT NULL DEFAULT 1         -- 1: Hiển thị, 0: Tạm ẩn
);

-- 4. Bảng cài đặt ứng dụng
CREATE TABLE settings (
    key   TEXT PRIMARY KEY,
    value TEXT NOT NULL
);

-- 5. Quản lý phiên bản CSDL
CREATE TABLE schema_info (
    version     INTEGER PRIMARY KEY,
    applied_at  TEXT NOT NULL
);
```

---

## 2. QUẢN LÝ DI TRÚ PHIÊN BẢN (DATABASE MIGRATIONS)

```csharp
namespace HabitGrid.Data.Migrations;

public interface IMigration
{
    int Version { get; }
    void Up(SqliteConnection connection, SqliteTransaction transaction);
}

public interface IMigrationRunner
{
    void ApplyMigrations();
}
```

**Quy trình an toàn dữ liệu**:
1. Trước khi chạy bất kỳ Migration nào có `Version > currentVersion`, hệ thống tự động sao lưu file `habitgrid.db` thành `habitgrid.db.bak-v{currentVersion}`.
2. Mỗi Migration chạy bên trong 1 `SqliteTransaction`. Nếu lỗi, tự động Rollback và ném ngoại lệ rõ ràng, không làm hỏng file DB gốc.

---

## 3. HỢP ĐỒNG REPOSITORY (DATA ACCESS INTERFACES)

### 3.1 `IHabitRepository`
```csharp
namespace HabitGrid.Data.Repositories;

public interface IHabitRepository
{
    Task<IReadOnlyList<Habit>> GetAllActiveAsync();
    Task<IReadOnlyList<Habit>> GetAllIncludingArchivedAsync();
    Task<Habit?> GetByIdAsync(Guid id);
    Task InsertAsync(Habit habit);
    Task UpdateAsync(Habit habit);
    Task SoftDeleteAsync(Guid id);
    Task HardDeleteAsync(Guid id);
    Task ArchiveAsync(Guid id);
    Task UnarchiveAsync(Guid id);
    Task UpdateSortOrdersAsync(IReadOnlyList<(Guid HabitId, int SortOrder)> orders);
    Task<int> CountNonDeletedAsync(); // Dùng để kiểm tra hạn mức gói Free (Active + Archived)
}
```

### 3.2 `IEntryRepository`
```csharp
namespace HabitGrid.Data.Repositories;

public interface IEntryRepository
{
    Task<Entry?> GetAsync(Guid habitId, DateOnly day);
    Task<IReadOnlyList<Entry>> GetRangeAsync(Guid habitId, DateOnly fromDay, DateOnly toDay);
    Task<IReadOnlyList<Entry>> GetAllForHabitAsync(Guid habitId);
    Task UpsertAsync(Entry entry); // Ghi đè hoặc tạo mới nếu chưa tồn tại
    Task DeleteAsync(Guid habitId, DateOnly day);
}
```

### 3.3 `ISettingsRepository`
```csharp
namespace HabitGrid.Data.Repositories;

public interface ISettingsRepository
{
    Task<string?> GetAsync(string key);
    Task<T> GetAsync<T>(string key, T defaultValue);
    Task SetAsync<T>(string key, T value);
}
```

---

## 4. DỊCH VỤ SAO LƯU & PHỤC HỒI (BACKUP & RESTORE SERVICE)

```csharp
namespace HabitGrid.Data.Backup;

public record BackupData(
    string App,
    int SchemaVersion,
    DateTime ExportedAt,
    IReadOnlyList<Habit> Habits,
    IReadOnlyList<Entry> Entries,
    IReadOnlyDictionary<string, string> Settings
);

public enum ImportMode
{
    Merge = 0,   // Giữ nguyên dữ liệu hiện tại, gộp thêm dữ liệu mới (nếu trùng ngày, lấy giá trị lớn hơn)
    Replace = 1  // Xóa toàn bộ dữ liệu hiện tại và nạp dữ liệu từ file sao lưu
}

public interface IBackupService
{
    Task<string> ExportJsonAsync();
    Task<byte[]> ExportCsvForHabitAsync(Guid habitId); // File định dạng: Date,Value,Status
    Task ImportJsonAsync(string jsonContent, ImportMode mode, bool isProUser);
}
```

**Quy tắc nghiệp vụ khi Nhập (Import)**:
1. **Kiểm tra tính hợp lệ**: `schemaVersion` trong file phải $\le$ phiên bản hiện tại của app. Nếu cao hơn, ném ngoại lệ `UnsupportedSchemaVersionException`.
2. **Quy tắc an toàn khi Replace**: Trước khi xóa dữ liệu cũ, tự động sao lưu CSDL hiện tại vào thư mục `%LocalAppData%\HabitGrid\backups\pre-import-{timestamp}.db`.
3. **Quy tắc gói Free khi Import**:
   - Nếu `isProUser == false` mà file chứa > 2 habits: Quá trình import **vẫn thành công**, toàn bộ lịch sử được nạp vào DB. Tuy nhiên, 2 habit đầu tiên (theo `sort_order`) được kích hoạt bình thường, các habit còn lại được gắn cờ chỉ đọc và widget hiển thị ở chế độ khóa Pro. **Tuyệt đối không từ chối hay làm mất dữ liệu của người dùng**.

---

## 5. BỘ KIỂM THỬ ĐƠN VỊ CẦN THỰC THI (`HabitGrid.Data.Tests`)

1. **Migration Tests**:
   - Chạy migration từ rỗng lên v1 thành công trên SQLite in-memory.
   - Thử nghiệm chèn dữ liệu vi phạm foreign key (bị chặn).
2. **Repository Tests**:
   - CRUD Habit: Insert, Get, Update, SoftDelete, Archive.
   - Entry Upsert: Ghi giá trị mới vào ô đã có dữ liệu -> cập nhật chính xác `value` và `updated_at`.
   - `CountNonDeletedAsync()`: Trả về chính xác tổng số Active + Archived (bỏ qua SoftDeleted).
3. **BackupService Tests**:
   - `ExportJsonAsync()` -> `ImportJsonAsync(mode: Replace)` -> CSDL phục hồi 100% khớp dữ liệu gốc.
   - `ImportJsonAsync(mode: Merge)` với bản ghi trùng: Kiểm tra lấy `Math.Max(oldValue, newValue)`.
   - File JSON không đúng định dạng -> Rollback toàn bộ transaction, giữ nguyên dữ liệu hiện tại.

---

## 6. TIÊU CHUẨN NGHIỆM THU (ACCEPTANCE CRITERIA)
- [ ] Chạy `dotnet test` vượt qua 100% test cases trong `HabitGrid.Data.Tests`.
- [ ] Hỗ trợ đa luồng an toàn nhờ SQLite WAL mode và Connection String `Pooling=True`.
- [ ] Toàn bộ thao tác ghi (Write) đều được bọc trong SqliteTransaction để chống phân mảnh / hư hỏng file khi tắt máy đột ngột.
