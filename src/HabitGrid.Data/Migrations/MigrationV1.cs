using Microsoft.Data.Sqlite;

namespace HabitGrid.Data.Migrations;

/// <summary>
/// Migration v1: tạo toàn bộ lược đồ CSDL theo đặc tả SPEC-02 mục 1.1.
/// </summary>
public sealed class MigrationV1 : IMigration
{
    public int Version => 1;

    public void Up(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;

        command.CommandText = """
            CREATE TABLE IF NOT EXISTS schema_info (
                version     INTEGER PRIMARY KEY,
                applied_at  TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS habits (
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

            CREATE INDEX IF NOT EXISTS idx_habits_sort ON habits(sort_order);
            CREATE INDEX IF NOT EXISTS idx_habits_status ON habits(deleted_at, archived_at);

            CREATE TABLE IF NOT EXISTS entries (
                habit_id   TEXT NOT NULL REFERENCES habits(id) ON DELETE CASCADE,
                day        TEXT NOT NULL,                    -- Ngày logic (yyyy-MM-dd)
                value      INTEGER NOT NULL,                 -- Số lượng hoặc 1/0
                updated_at TEXT NOT NULL,                    -- ISO-8601 UTC timestamp
                PRIMARY KEY (habit_id, day)
            );

            CREATE INDEX IF NOT EXISTS idx_entries_day ON entries(day);

            CREATE TABLE IF NOT EXISTS widget_state (
                habit_id  TEXT PRIMARY KEY REFERENCES habits(id) ON DELETE CASCADE,
                monitor   TEXT NOT NULL,                     -- Tên thiết bị hiển thị (DisplayDevice)
                x         REAL NOT NULL,                     -- Tọa độ tương đối X (0.0 -> 1.0)
                y         REAL NOT NULL,                     -- Tọa độ tương đối Y (0.0 -> 1.0)
                size      INTEGER NOT NULL DEFAULT 1,        -- 0: Small, 1: Medium, 2: Large
                visible   INTEGER NOT NULL DEFAULT 1         -- 1: Hiển thị, 0: Tạm ẩn
            );

            CREATE TABLE IF NOT EXISTS settings (
                key   TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            """;

        command.ExecuteNonQuery();
    }
}
