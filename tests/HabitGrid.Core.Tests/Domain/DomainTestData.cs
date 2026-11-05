using HabitGrid.Core.Domain;
using DayOfWeekMask = HabitGrid.Core.Models.DayOfWeekMask;
using DayOfWeekMaskExtensions = HabitGrid.Core.Models.DayOfWeekMaskExtensions;

namespace HabitGrid.Core.Tests.Domain;

/// <summary>Dữ liệu dựng sẵn cho test domain v2. Ngày tham chiếu 2026-10-22 là thứ Năm.</summary>
internal static class DomainTestData
{
    public static readonly DateOnly Today = new(2026, 10, 22);

    public static Habit NewHabit(
        HabitKind kind = HabitKind.Boolean,
        HabitFrequency? frequency = null,
        int target = 1,
        DateOnly? start = null) => new()
    {
        Name = "Test",
        Kind = kind,
        Target = new HabitTarget(target),
        Frequency = frequency ?? new HabitFrequency.Daily(),
        StartDate = start ?? Today.AddDays(-60)
    };

    public static DayLog Done(Habit habit, DateOnly day) => new()
    {
        HabitId = habit.Id,
        Day = day,
        Value = habit.EffectiveTarget,
        Status = DayStatus.Done
    };

    public static DayLog WithStatus(Habit habit, DateOnly day, DayStatus status, int value = 0) => new()
    {
        HabitId = habit.Id,
        Day = day,
        Value = value,
        Status = status
    };

    public static List<DayLog> DoneRange(Habit habit, DateOnly from, DateOnly to)
    {
        var logs = new List<DayLog>();
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            logs.Add(Done(habit, d));
        }

        return logs;
    }

    public static DayOfWeekMask Mask(params DayOfWeek[] days) =>
        days.Aggregate(DayOfWeekMask.None, (m, d) => m | DayOfWeekMaskExtensions.ToMask(d));
}
