using Microsoft.Data.Sqlite;

namespace HabitGrid.Data.Migrations;

/// <summary>
/// Một bước nâng cấp lược đồ CSDL. Mỗi migration chạy trong một transaction riêng.
/// </summary>
public interface IMigration
{
    /// <summary>Phiên bản lược đồ sau khi migration này được áp dụng.</summary>
    int Version { get; }

    /// <summary>Thực hiện thay đổi lược đồ.</summary>
    void Up(SqliteConnection connection, SqliteTransaction transaction);
}

/// <summary>
/// Bộ chạy migration.
/// </summary>
public interface IMigrationRunner
{
    /// <summary>Áp dụng toàn bộ migration còn thiếu.</summary>
    void ApplyMigrations();
}
