using Microsoft.Data.Sqlite;

namespace HabitGrid.Data.Migrations;

/// <summary>
/// Bộ chạy migration có an toàn dữ liệu:
/// 1. Sao lưu file CSDL thành <c>habitgrid.db.bak-v{currentVersion}</c> trước khi nâng cấp.
/// 2. Mỗi migration chạy trong một transaction riêng; lỗi sẽ được rollback và ném lại.
/// </summary>
public sealed class MigrationRunner : IMigrationRunner
{
    private readonly HabitGridDatabase _database;
    private readonly IReadOnlyList<IMigration> _migrations;
    private readonly string? _backupDirectory;

    public MigrationRunner(
        HabitGridDatabase database,
        IEnumerable<IMigration>? migrations = null,
        string? backupDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(database);

        _database = database;
        _migrations = (migrations ?? new IMigration[] { new MigrationV1(), new MigrationV2() })
            .OrderBy(m => m.Version)
            .ToArray();
        _backupDirectory = backupDirectory;
    }

    /// <summary>Phiên bản lược đồ hiện tại trong CSDL (0 nếu chưa có migration nào được áp dụng).</summary>
    public int GetCurrentVersion()
    {
        using var connection = _database.OpenConnection();
        EnsureSchemaInfoTable(connection);
        return ReadCurrentVersion(connection);
    }

    public void ApplyMigrations()
    {
        using var connection = _database.OpenConnection();
        EnsureSchemaInfoTable(connection);

        var currentVersion = ReadCurrentVersion(connection);
        var pending = _migrations.Where(m => m.Version > currentVersion).ToArray();

        if (pending.Length == 0)
        {
            return;
        }

        // Sao lưu trước khi nâng cấp lược đồ.
        CreateSafetyBackup(connection, currentVersion);

        foreach (var migration in pending)
        {
            ApplySingle(connection, migration);
        }
    }

    private void ApplySingle(SqliteConnection connection, IMigration migration)
    {
        using var transaction = connection.BeginTransaction();

        try
        {
            migration.Up(connection, transaction);
            RecordVersion(connection, transaction, migration.Version);
            transaction.Commit();
        }
        catch (Exception ex)
        {
            try
            {
                transaction.Rollback();
            }
            catch (InvalidOperationException)
            {
                // Transaction đã được rollback tự động.
            }

            throw new MigrationException(
                migration.Version,
                $"Migration phiên bản {migration.Version} thất bại. CSDL được giữ nguyên ở phiên bản trước.",
                ex);
        }
    }

    private static void EnsureSchemaInfoTable(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS schema_info (
                version     INTEGER PRIMARY KEY,
                applied_at  TEXT NOT NULL
            );
            """;

        command.ExecuteNonQuery();
    }

    private static int ReadCurrentVersion(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COALESCE(MAX(version), 0) FROM schema_info;";

        var result = command.ExecuteScalar();
        return result is null or DBNull ? 0 : Convert.ToInt32(result);
    }

    private static void RecordVersion(SqliteConnection connection, SqliteTransaction transaction, int version)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT OR REPLACE INTO schema_info (version, applied_at) VALUES ($version, $appliedAt);";
        command.Parameters.AddWithValue("$version", version);
        command.Parameters.AddWithValue("$appliedAt", DateTime.UtcNow.ToString("O"));

        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Tạo bản sao lưu an toàn của CSDL trước khi migrate.
    /// Dùng <c>VACUUM INTO</c> để bản sao luôn nhất quán kể cả khi đang ở chế độ WAL.
    /// </summary>
    private void CreateSafetyBackup(SqliteConnection connection, int currentVersion)
    {
        if (_database.IsInMemory)
        {
            // CSDL in-memory không tồn tại trên đĩa nên không có gì để sao lưu.
            return;
        }

        var directory = _backupDirectory
                        ?? Path.GetDirectoryName(_database.DatabasePath!)
                        ?? HabitGridDatabase.DefaultBackupDirectory;

        Directory.CreateDirectory(directory);

        var backupPath = Path.Combine(directory, $"habitgrid.db.bak-v{currentVersion}");

        // VACUUM INTO không ghi đè file đã tồn tại.
        if (File.Exists(backupPath))
        {
            File.Delete(backupPath);
        }

        using var command = connection.CreateCommand();
        command.CommandText = "VACUUM INTO $path;";
        command.Parameters.AddWithValue("$path", backupPath);

        command.ExecuteNonQuery();
    }
}
