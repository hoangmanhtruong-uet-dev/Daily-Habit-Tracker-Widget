using System.Globalization;
using System.Text;
using FluentAssertions;
using HabitGrid.Core.Models;
using HabitGrid.Data.Backup;
using Xunit;

namespace HabitGrid.Data.Tests;

/// <summary>
/// Bộ kiểm thử dịch vụ sao lưu & phục hồi theo SPEC-02 mục 5.3.
/// </summary>
public class BackupServiceTests : DatabaseTestBase
{
    [Fact]
    public async Task ExportJsonAsync_ShouldContainHabitsEntriesAndSettings()
    {
        var habit = await CreateHabitAsync("Đọc sách");
        await Entries.UpsertAsync(new Entry { HabitId = habit.Id, Day = new DateOnly(2026, 10, 21), Value = 1 });
        await Settings.SetAsync("weekStart", "monday");

        var json = await CreateService().ExportJsonAsync();

        json.Should().Contain("Đọc sách");
        json.Should().Contain("2026-10-21");
        json.Should().Contain("weekStart");
        json.Should().Contain("schemaVersion");
    }

    [Fact]
    public async Task ExportJsonAsync_ThenImportReplace_ShouldRestoreDatabaseExactly()
    {
        var habitA = await CreateHabitAsync("Đọc sách");
        var habitB = await CreateHabitAsync("Tập thể dục");
        await Entries.UpsertAsync(new Entry { HabitId = habitA.Id, Day = new DateOnly(2026, 10, 21), Value = 1 });
        await Entries.UpsertAsync(new Entry { HabitId = habitB.Id, Day = new DateOnly(2026, 10, 20), Value = 1 });
        await Settings.SetAsync("weekStart", "monday");

        var service = CreateService();
        var json = await service.ExportJsonAsync();

        // Làm bẩn dữ liệu hiện tại trước khi nạp lại.
        await Habits.HardDeleteAsync(habitA.Id);
        await Habits.InsertAsync(new Habit { Name = "Thói quen rác", SortOrder = 9 });

        await service.ImportJsonAsync(json, ImportMode.Replace, isProUser: true);

        var habits = await Habits.GetAllIncludingArchivedAsync();
        habits.Should().HaveCount(2);
        habits.Select(h => h.Name).Should().ContainInOrder("Đọc sách", "Tập thể dục");
        habits.Should().NotContain(h => h.Name == "Thói quen rác");

        (await Entries.GetAllForHabitAsync(habitA.Id)).Should().HaveCount(1);
        (await Entries.GetAllForHabitAsync(habitB.Id)).Should().HaveCount(1);
        (await Settings.GetAsync("weekStart")).Should().Be("monday");
    }

    [Fact]
    public async Task ImportJsonAsync_WithMergeAndDuplicateEntry_ShouldKeepMaxValue()
    {
        var habit = await CreateHabitAsync("Uống nước");
        var day = new DateOnly(2026, 10, 21);
        await Entries.UpsertAsync(new Entry { HabitId = habit.Id, Day = day, Value = 3 });

        var service = CreateService();

        // Giá trị lớn hơn -> thắng.
        await service.ImportJsonAsync(
            BuildBackupJson(habit.Id, day, value: 7),
            ImportMode.Merge,
            isProUser: true);

        (await Entries.GetAsync(habit.Id, day))!.Value.Should().Be(7);

        // Giá trị nhỏ hơn -> giữ giá trị cũ.
        await service.ImportJsonAsync(
            BuildBackupJson(habit.Id, day, value: 1),
            ImportMode.Merge,
            isProUser: true);

        (await Entries.GetAsync(habit.Id, day))!.Value.Should().Be(7);
    }

    [Fact]
    public async Task ImportJsonAsync_WithMerge_ShouldAddNewEntriesWithoutRemovingExisting()
    {
        var habit = await CreateHabitAsync("Uống nước");
        await Entries.UpsertAsync(new Entry { HabitId = habit.Id, Day = new DateOnly(2026, 10, 20), Value = 1 });

        await CreateService().ImportJsonAsync(
            BuildBackupJson(habit.Id, new DateOnly(2026, 10, 21), value: 1),
            ImportMode.Merge,
            isProUser: true);

        (await Entries.GetAllForHabitAsync(habit.Id)).Should().HaveCount(2);
    }

    [Fact]
    public async Task ImportJsonAsync_WithInvalidJson_ShouldRollbackAndKeepCurrentData()
    {
        var habit = await CreateHabitAsync("Uống nước");
        await Entries.UpsertAsync(new Entry { HabitId = habit.Id, Day = new DateOnly(2026, 10, 21), Value = 1 });

        var service = CreateService();

        var act = async () => await service.ImportJsonAsync("{ day la json hong", ImportMode.Replace, isProUser: true);

        await act.Should().ThrowAsync<BackupImportException>();

        // Dữ liệu hiện tại phải được giữ nguyên.
        (await Habits.GetAllActiveAsync()).Should().HaveCount(1);
        (await Entries.GetAllForHabitAsync(habit.Id)).Should().HaveCount(1);
    }

    [Fact]
    public async Task ImportJsonAsync_WithNewerSchemaVersion_ShouldThrow()
    {
        var json = BuildBackupJson(Guid.NewGuid(), new DateOnly(2026, 10, 21), value: 1, schemaVersion: 999);

        var act = async () => await CreateService().ImportJsonAsync(json, ImportMode.Merge, isProUser: true);

        await act.Should().ThrowAsync<UnsupportedSchemaVersionException>()
            .Where(e => e.FileSchemaVersion == 999 && e.CurrentSchemaVersion == HabitGridDatabase.CurrentSchemaVersion);
    }

    [Fact]
    public async Task ImportJsonAsync_AsFreeUserWithMoreThanTwoHabits_ShouldKeepAllDataButLockExtras()
    {
        var habitIds = new List<Guid>();

        for (var i = 0; i < 3; i++)
        {
            var habit = new Habit { Name = $"Thói quen {i + 1}", SortOrder = i, StartDate = new DateOnly(2026, 10, 1) };
            habitIds.Add(habit.Id);
        }

        var json = BuildBackupJson(habitIds, new DateOnly(2026, 10, 21), value: 1);

        var act = async () => await CreateService().ImportJsonAsync(json, ImportMode.Replace, isProUser: false);

        await act.Should().NotThrowAsync();

        var habits = await Habits.GetAllIncludingArchivedAsync();
        habits.Should().HaveCount(3, "dữ liệu người dùng không được bị từ chối hay mất");

        var sorted = habits.OrderBy(h => h.SortOrder).ToList();
        sorted[0].IsReadOnly.Should().BeFalse();
        sorted[1].IsReadOnly.Should().BeFalse();
        sorted[2].IsReadOnly.Should().BeTrue("thói quen vượt hạn mức gói Free chỉ được xem");
    }

    [Fact]
    public async Task ImportJsonAsync_AsProUser_ShouldNotLockAnyHabit()
    {
        var habitIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

        await CreateService().ImportJsonAsync(
            BuildBackupJson(habitIds, new DateOnly(2026, 10, 21), value: 1),
            ImportMode.Replace,
            isProUser: true);

        var habits = await Habits.GetAllIncludingArchivedAsync();
        habits.Should().HaveCount(3);
        habits.Should().OnlyContain(h => !h.IsReadOnly);
    }

    [Fact]
    public async Task ExportCsvForHabitAsync_ShouldProduceExpectedFormat()
    {
        var habit = await CreateHabitAsync("Uống nước");
        await Entries.UpsertAsync(new Entry { HabitId = habit.Id, Day = new DateOnly(2026, 10, 20), Value = 1 });
        await Entries.UpsertAsync(new Entry { HabitId = habit.Id, Day = new DateOnly(2026, 10, 21), Value = 0 });

        var bytes = await CreateService().ExportCsvForHabitAsync(habit.Id);
        var text = Encoding.UTF8.GetString(bytes);

        text.Should().Contain("Date,Value,Status");
        text.Should().Contain("2026-10-20,1,Done");
        text.Should().Contain("2026-10-21,0,NotDone");
    }

    [Fact]
    public async Task ExportCsvForHabitAsync_WithUnknownHabit_ShouldThrow()
    {
        var act = async () => await CreateService().ExportCsvForHabitAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<HabitNotFoundException>();
    }

    private BackupService CreateService() => new(Database, Habits, Entries);

    private async Task<Habit> CreateHabitAsync(string name)
    {
        var habit = new Habit { Name = name, SortOrder = 0, StartDate = new DateOnly(2026, 10, 1) };
        await Habits.InsertAsync(habit);
        return habit;
    }

    private static string BuildBackupJson(Guid habitId, DateOnly day, int value, int schemaVersion = HabitGridDatabase.CurrentSchemaVersion)
    {
        return BuildBackupJson(new List<Guid> { habitId }, day, value, schemaVersion);
    }

    private static string BuildBackupJson(IReadOnlyList<Guid> habitIds, DateOnly day, int value, int schemaVersion = HabitGridDatabase.CurrentSchemaVersion)
    {
        var habits = new StringBuilder();
        var entries = new StringBuilder();

        for (var i = 0; i < habitIds.Count; i++)
        {
            if (i > 0)
            {
                habits.Append(',');
                entries.Append(',');
            }

            habits.Append($$"""
                {"id":"{{habitIds[i]}}","name":"Thói quen {{i + 1}}","icon":"Checkmark24Regular","kind":0,
                 "target":1,"scheduleMask":127,"paletteId":"default","startDate":"2026-10-01",
                 "reminderMask":127,"sortOrder":{{i}},"createdAt":"2026-10-01T00:00:00Z"}
                """.ReplaceLineEndings(" ").Trim());

            entries.Append($$"""
                {"habitId":"{{habitIds[i]}}","day":"{{day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}}",
                 "value":{{value}},"updatedAt":"2026-10-21T00:00:00Z"}
                """.ReplaceLineEndings(" ").Trim());
        }

        return $$"""
            {
              "app": "HabitGrid",
              "schemaVersion": {{schemaVersion}},
              "exportedAt": "2026-10-22T00:00:00Z",
              "habits": [{{habits}}],
              "entries": [{{entries}}],
              "settings": {}
            }
            """;
    }
}
