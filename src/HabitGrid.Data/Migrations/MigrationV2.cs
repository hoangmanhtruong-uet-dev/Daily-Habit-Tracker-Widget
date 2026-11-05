using Microsoft.Data.Sqlite;

namespace HabitGrid.Data.Migrations;

/// <summary>
/// Migration v2: thêm cờ <c>read_only</c> cho thói quen.
///
/// Cờ này phục vụ quy tắc gói Free của đặc tả tổng (mục 4.5) và quy tắc nhập sao lưu của SPEC-02 mục 4.3:
/// khi người dùng Free nạp file chứa nhiều hơn 2 thói quen, các thói quen vượt hạn mức được gắn cờ chỉ đọc
/// thay vì bị từ chối hoặc mất dữ liệu.
/// </summary>
public sealed class MigrationV2 : IMigration
{
    public int Version => 2;

    public void Up(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;

        command.CommandText = "ALTER TABLE habits ADD COLUMN read_only INTEGER NOT NULL DEFAULT 0;";

        command.ExecuteNonQuery();
    }
}
