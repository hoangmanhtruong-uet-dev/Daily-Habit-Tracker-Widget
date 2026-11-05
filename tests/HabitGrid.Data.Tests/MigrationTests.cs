using FluentAssertions;
using HabitGrid.Data;
using HabitGrid.Data.Migrations;
using Microsoft.Data.Sqlite;
using Xunit;

namespace HabitGrid.Data.Tests;

/// <summary>
/// Bộ kiểm thử hệ thống migration theo SPEC-02 mục 5.1.
/// </summary>
public class MigrationTests
{
    [Fact]
    public void ApplyMigrations_FromEmpty_ShouldCreateAllTables()
    {
        using var database = HabitGridDatabase.CreateInMemory();
        var runner = new MigrationRunner(database);

        runner.ApplyMigrations();

        runner.GetCurrentVersion().Should().Be(HabitGridDatabase.CurrentSchemaVersion);

        using var connection = database.OpenConnection();
        var tables = GetTableNames(connection);

        tables.Should().Contain(new[] { "habits", "entries", "widget_state", "settings", "schema_info" });
    }

    [Fact]
    public void ApplyMigrations_UpToV1_ShouldCreateSchemaAndRecordVersion()
    {
        using var database = HabitGridDatabase.CreateInMemory();
        var runner = new MigrationRunner(database, new IMigration[] { new MigrationV1() });

        runner.ApplyMigrations();

        runner.GetCurrentVersion().Should().Be(1);

        using var connection = database.OpenConnection();
        GetTableNames(connection).Should().Contain(new[] { "habits", "entries", "widget_state", "settings" });
    }

    [Fact]
    public void ApplyMigrations_CalledTwice_ShouldBeIdempotent()
    {
        using var database = HabitGridDatabase.CreateInMemory();
        var runner = new MigrationRunner(database);

        runner.ApplyMigrations();
        runner.ApplyMigrations();

        runner.GetCurrentVersion().Should().Be(HabitGridDatabase.CurrentSchemaVersion);
    }

    [Fact]
    public void ApplyMigrations_ShouldEnableForeignKeys()
    {
        using var database = HabitGridDatabase.CreateInMemory();
        new MigrationRunner(database).ApplyMigrations();

        using var connection = database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys;";

        var result = command.ExecuteScalar();
        Convert.ToInt32(result).Should().Be(1);
    }

    [Fact]
    public async Task InsertEntry_WithNonExistentHabit_ShouldBeBlockedByForeignKey()
    {
        using var database = HabitGridDatabase.CreateInMemory();
        await database.InitializeAsync();

        await using var connection = await database.OpenConnectionAsync();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO entries (habit_id, day, value, updated_at)
            VALUES ('khong-ton-tai', '2026-10-22', 1, '2026-10-22T00:00:00Z');
            """;

        var act = async () => await command.ExecuteNonQueryAsync();

        await act.Should().ThrowAsync<SqliteException>()
            .Where(e => e.Message.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ApplyMigrations_WithFailingMigration_ShouldThrowAndKeepOriginalSchema()
    {
        using var database = HabitGridDatabase.CreateInMemory();
        new MigrationRunner(database).ApplyMigrations();

        var failing = new FailingMigration();
        var runner = new MigrationRunner(database, new IMigration[] { failing });

        var act = () => runner.ApplyMigrations();

        act.Should().Throw<MigrationException>()
            .Which.Version.Should().Be(99);

        // Phiên bản lược đô vẫn nguyên vẹn sau khi migration lỗi.
        runner.GetCurrentVersion().Should().Be(HabitGridDatabase.CurrentSchemaVersion);
    }

    [Fact]
    public void ApplyMigrations_OnFileDatabase_ShouldCreateSafetyBackup()
    {
        var directory = Path.Combine(Path.GetTempPath(), "habitgrid-tests-" + Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(directory, "habitgrid.db");

        try
        {
            using (var database = HabitGridDatabase.CreateAtPath(databasePath))
            {
                new MigrationRunner(database).ApplyMigrations();
            }

            // Mở lại và áp dụng một migration mới -> phải sinh bản sao lưu an toàn.
            using (var database = HabitGridDatabase.CreateAtPath(databasePath))
            {
                var runner = new MigrationRunner(database, new IMigration[] { new MigrationV1(), new MigrationV2(), new NoOpMigrationV3() });
                runner.ApplyMigrations();
            }

            var backupPath = Path.Combine(directory, $"habitgrid.db.bak-v{HabitGridDatabase.CurrentSchemaVersion}");
            File.Exists(backupPath).Should().BeTrue("migration phải sao lưu CSDL trước khi nâng cấp lược đồ");
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static List<string> GetTableNames(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table';";

        var names = new List<string>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    private sealed class FailingMigration : IMigration
    {
        public int Version => 99;

        public void Up(SqliteConnection connection, SqliteTransaction transaction)
        {
            throw new InvalidOperationException("Migration cố tình thất bại.");
        }
    }

    private sealed class NoOpMigrationV3 : IMigration
    {
        public int Version => 3;

        public void Up(SqliteConnection connection, SqliteTransaction transaction)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "CREATE TABLE IF NOT EXISTS migration_probe (id INTEGER PRIMARY KEY);";
            command.ExecuteNonQuery();
        }
    }
}
