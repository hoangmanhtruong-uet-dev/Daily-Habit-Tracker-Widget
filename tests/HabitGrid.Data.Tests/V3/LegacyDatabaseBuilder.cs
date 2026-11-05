using HabitGrid.Core.Models;
using HabitGrid.Data;
using HabitGrid.Data.Repositories;

namespace HabitGrid.Data.Tests.V3;

/// <summary>Dựng một file CSDL v1/v2 thật bằng chính repository v1 để kiểm thử migration.</summary>
internal static class LegacyDatabaseBuilder
{
    public static readonly DateOnly Today = new(2026, 10, 22);

    public sealed record Result(
        IReadOnlyList<Habit> Habits,
        IReadOnlyList<Entry> Entries);

    public static async Task<Result> BuildAsync(string path)
    {
        using var database = HabitGridDatabase.CreateAtPath(path);
        await database.InitializeAsync();

        var habits = new HabitRepository(database);
        var entries = new EntryRepository(database);
        var widgets = new WidgetStateRepository(database);
        var settings = new SettingsRepository(database);

        var random = new Random(77);
        var created = new List<Habit>
        {
            new()
            {
                Name = "Uống nước", Icon = "Drop24Regular", Kind = HabitKind.Count, Target = 8,
                ScheduleMask = DayOfWeekMask.AllWeek, PaletteId = "blue", StartDate = Today.AddDays(-90), SortOrder = 0,
                ReminderTime = new TimeOnly(8, 30), ReminderMask = DayOfWeekMask.Monday | DayOfWeekMask.Friday
            },
            new()
            {
                Name = "Tập gym", Icon = "Dumbbell24Regular", Kind = HabitKind.Boolean,
                ScheduleMask = DayOfWeekMask.Monday | DayOfWeekMask.Wednesday | DayOfWeekMask.Friday,
                PaletteId = "orange", CustomColorHex = "#aa3355", StartDate = Today.AddDays(-120), SortOrder = 1
            },
            new()
            {
                Name = "Đọc sách", Icon = "Book24Regular", Kind = HabitKind.Boolean,
                ScheduleMask = DayOfWeekMask.AllWeek, StartDate = Today.AddDays(-30), SortOrder = 2,
                ArchivedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        };

        foreach (var habit in created)
        {
            await habits.InsertAsync(habit);
        }

        var allEntries = new List<Entry>();
        foreach (var habit in created)
        {
            for (var day = habit.StartDate; day <= Today; day = day.AddDays(1))
            {
                if (random.NextDouble() < 0.75)
                {
                    var entry = new Entry
                    {
                        HabitId = habit.Id,
                        Day = day,
                        Value = habit.Kind == HabitKind.Boolean ? 1 : random.Next(0, 11)
                    };
                    await entries.UpsertAsync(entry);
                    allEntries.Add(entry);
                }
            }
        }

        await widgets.UpsertAsync(new WidgetState
        {
            HabitId = created[0].Id,
            Monitor = @"\\.\DISPLAY1",
            X = 0.25,
            Y = 0.5,
            Size = WidgetSize.Large,
            Visible = true
        });

        await settings.SetAsync("theme", "dark");
        await settings.SetAsync("dayStartHour", 3);

        return new Result(created, allEntries);
    }
}
