using System.Security.Cryptography;
using FluentAssertions;
using HabitGrid.Core.Domain;
using HabitGrid.Data.V3;
using Microsoft.Data.Sqlite;
using Xunit;
using V1 = HabitGrid.Core.Models;
using V1Streak = HabitGrid.Core.Services.StreakCalculator;
using V1Schedule = HabitGrid.Core.Services.ScheduleCalculator;

namespace HabitGrid.Data.Tests.V3;

public sealed class LegacyImporterTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "habitgrid-v3-" + Guid.NewGuid().ToString("N"));
    private readonly HlcClock _clock = new("import-test");

    public LegacyImporterTests() => Directory.CreateDirectory(_directory);

    private string LegacyPath => Path.Combine(_directory, "habitgrid.db");

    private string TargetPath => Path.Combine(_directory, "habitgrid-v3.db");

    [Fact]
    public async Task Import_CopiesEverything_AndStreaksMatchV1()
    {
        var legacy = await LegacyDatabaseBuilder.BuildAsync(LegacyPath);

        var result = await LegacyImporter.ImportAsync(LegacyPath, TargetPath, _clock);

        result.Imported.Should().BeTrue();
        result.Habits.Should().Be(3);
        result.DayLogs.Should().Be(legacy.Entries.Count);
        result.Widgets.Should().Be(1);
        result.Settings.Should().Be(2);

        using var database = HabitGridV3.Open(TargetPath);
        var habits = new HabitRepository(database, _clock);
        var logs = new DayLogRepository(database, _clock);

        var imported = await habits.GetAllAsync();
        imported.Select(h => h.Name).Should().Equal("Uống nước", "Tập gym", "Đọc sách");

        var v1Calc = new V1Streak(new V1Schedule());
        var v2Calc = new HabitGrid.Core.Domain.StreakCalculator();

        foreach (var v1Habit in legacy.Habits)
        {
            var v2Habit = imported.Single(h => h.Id == v1Habit.Id);
            var v2Logs = await logs.GetAllForHabitAsync(v1Habit.Id);
            var v1Entries = legacy.Entries.Where(e => e.HabitId == v1Habit.Id).ToList();

            var expected = v1Calc.Calculate(v1Habit, v1Entries, LegacyDatabaseBuilder.Today);
            var actual = v2Calc.Calculate(v2Habit, v2Logs, LegacyDatabaseBuilder.Today);

            actual.Current.Should().Be(expected.CurrentStreak, v1Habit.Name);
            actual.Best.Should().Be(expected.LongestStreak, v1Habit.Name);
            v2Logs.Count.Should().Be(v1Entries.Count, v1Habit.Name);
        }
    }

    [Fact]
    public async Task Import_MapsFieldsAndKeepsWidgetPositionAndSettings()
    {
        var legacy = await LegacyDatabaseBuilder.BuildAsync(LegacyPath);
        await LegacyImporter.ImportAsync(LegacyPath, TargetPath, _clock);

        using var database = HabitGridV3.Open(TargetPath);
        var habits = new HabitRepository(database, _clock);

        var water = (await habits.GetByIdAsync(legacy.Habits[0].Id))!;
        water.Kind.Should().Be(HabitKind.Count);
        water.Target.Value.Should().Be(8);
        water.ColorHue.Should().Be("#5AB0F5");
        water.Icon.Value.Should().Be("fluent:Drop24Regular");
        water.Reminders.Should().ContainSingle()
            .Which.Should().Be(new Reminder(new TimeOnly(8, 30), V1.DayOfWeekMask.Monday | V1.DayOfWeekMask.Friday));

        var gym = (await habits.GetByIdAsync(legacy.Habits[1].Id))!;
        gym.Frequency.Should().Be(new HabitFrequency.WeekDays(V1.DayOfWeekMask.Monday | V1.DayOfWeekMask.Wednesday | V1.DayOfWeekMask.Friday));
        gym.ColorHue.Should().Be("#AA3355", "màu tùy chỉnh thắng preset");

        (await habits.GetByIdAsync(legacy.Habits[2].Id))!.IsArchived.Should().BeTrue();

        using var connection = database.OpenConnection();
        Query(connection, "SELECT x || ',' || y || ',' || size FROM widgets;").Should().Equal("0.25,0.5,2");
        Query(connection, "SELECT value FROM settings WHERE key = 'theme';").Should().Equal("dark");
        Query(connection, "SELECT value FROM sync_state WHERE key = 'import.legacy_path';").Should().Equal(LegacyPath);
        Query(connection, "SELECT COUNT(*) FROM sync_outbox;").Single().Should().NotBe("0", "mọi bản ghi được đưa vào outbox để đẩy lên cloud");
    }

    [Fact]
    public async Task Import_NeverModifiesTheLegacyDatabase()
    {
        await LegacyDatabaseBuilder.BuildAsync(LegacyPath);
        SqliteConnection.ClearAllPools();
        var before = Sha256(LegacyPath);

        await LegacyImporter.ImportAsync(LegacyPath, TargetPath, _clock);

        Sha256(LegacyPath).Should().Be(before, "file v1 chỉ được mở đọc");
    }

    [Fact]
    public async Task Import_SecondRun_IsSkipped_AndDoesNotOverwriteNewData()
    {
        await LegacyDatabaseBuilder.BuildAsync(LegacyPath);
        await LegacyImporter.ImportAsync(LegacyPath, TargetPath, _clock);

        using (var database = HabitGridV3.Open(TargetPath))
        {
            var habits = new HabitRepository(database, _clock);
            await habits.InsertAsync(new Habit { Name = "Mới ở v2", SortKey = "Z", StartDate = LegacyDatabaseBuilder.Today });
        }

        var second = await LegacyImporter.ImportAsync(LegacyPath, TargetPath, _clock);

        second.Imported.Should().BeFalse();
        second.SkippedReason.Should().Be("target.initialized");

        using var reopened = HabitGridV3.Open(TargetPath);
        (await new HabitRepository(reopened, _clock).GetAllAsync()).Select(h => h.Name).Should().Contain("Mới ở v2");
    }

    [Fact]
    public async Task Import_MissingOrEmptyLegacy_IsSkipped()
    {
        (await LegacyImporter.ImportAsync(LegacyPath, TargetPath, _clock)).SkippedReason.Should().Be("legacy.missing");

        // CSDL v1 vừa tạo, chưa có gì.
        using (var empty = HabitGridDatabase.CreateAtPath(LegacyPath))
        {
            await empty.InitializeAsync();
        }

        SqliteConnection.ClearAllPools();
        (await LegacyImporter.ImportAsync(LegacyPath, TargetPath, _clock)).SkippedReason.Should().Be("legacy.empty");
        File.Exists(TargetPath).Should().BeFalse();
    }

    [Fact]
    public async Task Import_Cancelled_LeavesNoTargetAndNoTempFiles()
    {
        await LegacyDatabaseBuilder.BuildAsync(LegacyPath);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await FluentActions.Awaiting(() => LegacyImporter.ImportAsync(LegacyPath, TargetPath, _clock, cts.Token))
            .Should().ThrowAsync<OperationCanceledException>();

        File.Exists(TargetPath).Should().BeFalse("không bao giờ có DB v3 nửa vời");
        Directory.GetFiles(_directory, "*.importing*").Should().BeEmpty();
    }

    [Fact]
    public async Task Import_StaleTempFromEarlierCrash_IsReplaced()
    {
        await LegacyDatabaseBuilder.BuildAsync(LegacyPath);
        await File.WriteAllTextAsync(TargetPath + ".importing", "garbage from a crashed run");

        var result = await LegacyImporter.ImportAsync(LegacyPath, TargetPath, _clock);

        result.Imported.Should().BeTrue();
        Directory.GetFiles(_directory, "*.importing*").Should().BeEmpty();
    }

    [Fact]
    public async Task Import_ReplacesEmptyFreshV3Database_ButNeverAForeignFile()
    {
        await LegacyDatabaseBuilder.BuildAsync(LegacyPath);

        // v3 vừa tạo và còn trống → được thay.
        HabitGridV3.Open(TargetPath).Dispose();
        SqliteConnection.ClearAllPools();
        (await LegacyImporter.ImportAsync(LegacyPath, TargetPath, _clock)).Imported.Should().BeTrue();

        // Một file lạ không phải SQLite → không đụng tới.
        var foreign = Path.Combine(_directory, "other.db");
        await File.WriteAllTextAsync(foreign, "not a database, user data");
        var result = await LegacyImporter.ImportAsync(LegacyPath, foreign, _clock);

        result.Imported.Should().BeFalse();
        (await File.ReadAllTextAsync(foreign)).Should().Be("not a database, user data");
    }

    [Fact]
    public async Task Import_NeverOverwritesAnotherAppsSqliteDatabase()
    {
        await LegacyDatabaseBuilder.BuildAsync(LegacyPath);

        var foreign = Path.Combine(_directory, "otherapp.db");
        using (var connection = new SqliteConnection($"Data Source={foreign};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE notes (id INTEGER PRIMARY KEY, body TEXT); INSERT INTO notes (body) VALUES ('keep me');";
            command.ExecuteNonQuery();
        }

        var result = await LegacyImporter.ImportAsync(LegacyPath, foreign, _clock);

        result.Imported.Should().BeFalse();
        using var check = new SqliteConnection($"Data Source={foreign};Mode=ReadOnly;Pooling=False");
        check.Open();
        Query(check, "SELECT name FROM sqlite_master WHERE type = 'table';").Should().Equal("notes");
        Query(check, "SELECT body FROM notes;").Should().Equal("keep me");
    }

    [Fact]
    public async Task Import_LegacyWithoutReadOnlyColumn_IsAccepted()
    {
        // CSDL v1 gốc (trước migration V2) chưa có cột read_only.
        using (var database = HabitGridDatabase.CreateAtPath(LegacyPath))
        {
            new HabitGrid.Data.Migrations.MigrationRunner(database, [new HabitGrid.Data.Migrations.MigrationV1()]).ApplyMigrations();

            using var connection = database.OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO habits (id, name, icon, kind, target, schedule_mask, palette_id, start_date, reminder_mask, sort_order, created_at)
                VALUES ('11111111-1111-1111-1111-111111111111', 'Thiền', 'Heart24Regular', 0, 1, 127, 'default', '2026-10-01', 127, 0, '2026-10-01T00:00:00.0000000Z');
                """;
            command.ExecuteNonQuery();
        }

        SqliteConnection.ClearAllPools();
        var result = await LegacyImporter.ImportAsync(LegacyPath, TargetPath, _clock);

        result.Imported.Should().BeTrue();
        result.Habits.Should().Be(1);
    }

    private static List<string> Query(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        var list = new List<string>();
        while (reader.Read())
        {
            list.Add(Convert.ToString(reader.GetValue(0), System.Globalization.CultureInfo.InvariantCulture)!);
        }

        return list;
    }

    private static string Sha256(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(_directory))
        {
            try
            {
                Directory.Delete(_directory, recursive: true);
            }
            catch (IOException)
            {
                // Tệp đang bị antivirus giữ: bỏ qua, thư mục tạm sẽ được dọn sau.
            }
        }
    }
}
