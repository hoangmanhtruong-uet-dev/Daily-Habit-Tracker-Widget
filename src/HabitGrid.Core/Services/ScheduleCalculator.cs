using HabitGrid.Core.Models;

namespace HabitGrid.Core.Services;

/// <summary>
/// Hiện thực <see cref="IScheduleCalculator"/> dựa trên <see cref="DayOfWeekMask"/>.
/// </summary>
public sealed class ScheduleCalculator : IScheduleCalculator
{
    public bool IsScheduled(Habit habit, DateOnly date)
    {
        ArgumentNullException.ThrowIfNull(habit);

        return habit.ScheduleMask.IsInMask(date.DayOfWeek);
    }

    public int CountScheduledDays(Habit habit, DateOnly fromDate, DateOnly toDate)
    {
        ArgumentNullException.ThrowIfNull(habit);

        if (toDate < fromDate)
        {
            return 0;
        }

        var count = 0;
        for (var day = fromDate; day <= toDate; day = day.AddDays(1))
        {
            if (IsScheduled(habit, day))
            {
                count++;
            }
        }

        return count;
    }
}
