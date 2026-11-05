using HabitGrid.Core.Models;

namespace HabitGrid.Core.Services;

/// <summary>
/// Tính toán lịch lặp của thói quen theo bitmask các thứ trong tuần.
/// </summary>
public interface IScheduleCalculator
{
    /// <summary>
    /// Ngày có thuộc lịch lặp của thói quen hay không.
    /// Ngày không thuộc lịch được gọi là "Off-Day".
    /// </summary>
    bool IsScheduled(Habit habit, DateOnly date);

    /// <summary>Đếm số ngày có lịch trong khoảng [fromDate, toDate] (bao gồm hai đầu).</summary>
    int CountScheduledDays(Habit habit, DateOnly fromDate, DateOnly toDate);
}
