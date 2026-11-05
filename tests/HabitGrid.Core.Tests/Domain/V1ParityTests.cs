using FluentAssertions;
using HabitGrid.Core.Domain;
using HabitGrid.Core.Models;
using HabitGrid.Core.Services;
using Xunit;
using V1Streak = HabitGrid.Core.Services.StreakCalculator;
using V2Streak = HabitGrid.Core.Domain.StreakCalculator;

namespace HabitGrid.Core.Tests.Domain;

/// <summary>
/// Tiêu chí nghiệm thu 3 của SPEC-V2-01: habit v1 ánh xạ sang v2 phải cho streak bằng v1 với cùng dữ liệu.
/// </summary>
public class V1ParityTests
{
    private static readonly DateOnly Today = new(2026, 10, 22);

    [Fact]
    public void MappedHabits_ProduceSameStreakAsV1()
    {
        var random = new Random(1234);
        var v1Calc = new V1Streak(new HabitGrid.Core.Services.ScheduleCalculator());
        var v2Calc = new V2Streak();

        for (var i = 0; i < 500; i++)
        {
            var kind = random.Next(3) == 0 ? HabitGrid.Core.Models.HabitKind.Count : HabitGrid.Core.Models.HabitKind.Boolean;
            var target = kind == HabitGrid.Core.Models.HabitKind.Count ? random.Next(1, 9) : 1;
            var mask = (DayOfWeekMask)random.Next(1, 128);

            var v1Habit = new HabitGrid.Core.Models.Habit
            {
                Name = $"H{i}",
                Kind = kind,
                Target = target,
                ScheduleMask = mask,
                StartDate = Today.AddDays(-random.Next(1, 300))
            };

            var v1Entries = new List<Entry>();
            for (var d = v1Habit.StartDate; d <= Today; d = d.AddDays(1))
            {
                if (random.NextDouble() < 0.7)
                {
                    v1Entries.Add(new Entry
                    {
                        HabitId = v1Habit.Id,
                        Day = d,
                        Value = kind == HabitGrid.Core.Models.HabitKind.Boolean ? 1 : random.Next(0, target + 3)
                    });
                }
            }

            var expected = v1Calc.Calculate(v1Habit, v1Entries, Today);

            var v2Habit = V1Mapper.ToV2(v1Habit, "V");
            var v2Logs = v1Entries.Select(e => V1Mapper.ToV2(e, v2Habit)).ToList();
            var actual = v2Calc.Calculate(v2Habit, v2Logs, Today);

            actual.Current.Should().Be(expected.CurrentStreak, $"case {i}: current, mask {mask}, target {target}");
            actual.Best.Should().Be(expected.LongestStreak, $"case {i}: best, mask {mask}, target {target}");
        }
    }

    [Fact]
    public void MapAll_PreservesOrderUsingFractionalKeys()
    {
        var habits = new[]
        {
            new HabitGrid.Core.Models.Habit { Name = "C", SortOrder = 2 },
            new HabitGrid.Core.Models.Habit { Name = "A", SortOrder = 0 },
            new HabitGrid.Core.Models.Habit { Name = "B", SortOrder = 1 }
        };

        var mapped = V1Mapper.ToV2(habits);

        mapped.Select(h => h.Name).Should().Equal("A", "B", "C");
        mapped.Select(h => h.SortKey).Should().BeInAscendingOrder(StringComparer.Ordinal);
        mapped.Select(h => h.SortKey).Distinct().Should().HaveCount(3);
    }

    [Fact]
    public void ToV2_MapsFieldsWithoutLoss()
    {
        var reminder = new TimeOnly(7, 30);
        var v1 = new HabitGrid.Core.Models.Habit
        {
            Name = "Uống nước",
            Icon = "Drop24Regular",
            Kind = HabitGrid.Core.Models.HabitKind.Count,
            Target = 8,
            ScheduleMask = DayOfWeekMask.Monday | DayOfWeekMask.Friday,
            PaletteId = "blue",
            ReminderTime = reminder,
            ReminderMask = DayOfWeekMask.Monday,
            StartDate = new DateOnly(2026, 1, 5),
            ArchivedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        var v2 = V1Mapper.ToV2(v1, "V");

        v2.Id.Should().Be(v1.Id);
        v2.Icon.Value.Should().Be("fluent:Drop24Regular");
        v2.Kind.Should().Be(HabitGrid.Core.Domain.HabitKind.Count);
        v2.Target.Value.Should().Be(8);
        v2.Frequency.Should().Be(new HabitFrequency.WeekDays(DayOfWeekMask.Monday | DayOfWeekMask.Friday));
        v2.ColorHue.Should().Be("#5AB0F5");
        v2.Reminders.Should().ContainSingle().Which.Should().Be(new Reminder(reminder, DayOfWeekMask.Monday));
        v2.StartDate.Should().Be(v1.StartDate);
        v2.ArchivedAt.Should().Be(v1.ArchivedAt);
    }

    [Fact]
    public void ToV2_CustomColorOverridesPreset()
    {
        var v1 = new HabitGrid.Core.Models.Habit { PaletteId = "pink", CustomColorHex = "#abcdef" };

        V1Mapper.ToV2(v1, "V").ColorHue.Should().Be("#ABCDEF");
    }
}
