using HabitGrid.Core.Models;

namespace HabitGrid.Core.Services;

/// <summary>
/// Hiện thực <see cref="IStreakCalculator"/>.
///
/// Quy tắc bất biến:
/// 1. Chỉ xét các ngày >= habit.StartDate và thuộc lịch lặp.
/// 2. Hôm nay thuộc lịch nhưng chưa đánh dấu CHƯA làm gãy chuỗi — chuỗi được tính từ ngày có lịch liền kề trước đó.
/// 3. Ngày không thuộc lịch (Off-Day) bị bỏ qua, không làm gãy chuỗi.
/// 4. Đánh dấu ngoài lịch vẫn được tính vào TotalCompletedDays nhưng không ảnh hưởng chuỗi.
/// </summary>
public sealed class StreakCalculator : IStreakCalculator
{
    /// <summary>Số ngày của cửa sổ tính tỷ lệ hoàn thành (kể cả hôm nay).</summary>
    public const int CompletionWindowDays = 30;

    private readonly IScheduleCalculator _scheduleCalculator;

    public StreakCalculator(IScheduleCalculator scheduleCalculator)
    {
        ArgumentNullException.ThrowIfNull(scheduleCalculator);
        _scheduleCalculator = scheduleCalculator;
    }

    public StreakResult Calculate(Habit habit, IReadOnlyList<Entry> entries, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(habit);
        ArgumentNullException.ThrowIfNull(entries);

        var byDay = BuildEntryMap(habit, entries);

        bool IsCompletedOn(DateOnly day) =>
            byDay.TryGetValue(day, out var entry) && entry.IsCompleted(habit);

        var currentStreak = CalculateCurrentStreak(habit, today, IsCompletedOn);
        var longestStreak = CalculateLongestStreak(habit, today, IsCompletedOn);
        var completionRate = CalculateCompletionRate30Days(habit, today, IsCompletedOn);
        var totalCompletedDays = byDay.Values.Count(entry => entry.IsCompleted(habit));

        return new StreakResult(currentStreak, longestStreak, completionRate, totalCompletedDays);
    }

    /// <summary>
    /// Chuỗi hiện tại: lùi dần từ hôm nay qua các ngày có lịch.
    /// - Hôm nay có lịch và đã hoàn thành: tính cả hôm nay.
    /// - Hôm nay có lịch nhưng chưa hoàn thành: bỏ qua hôm nay, tiếp tục lùi về ngày có lịch liền kề trước đó.
    /// - Hôm nay không có lịch: lùi về ngày có lịch gần nhất.
    /// </summary>
    private int CalculateCurrentStreak(Habit habit, DateOnly today, Func<DateOnly, bool> isCompletedOn)
    {
        var streak = 0;

        if (today >= habit.StartDate &&
            _scheduleCalculator.IsScheduled(habit, today) &&
            isCompletedOn(today))
        {
            streak = 1;
        }

        // Ngày không thuộc lịch được bỏ qua (continue), ngày thuộc lịch nhưng chưa hoàn thành thì chuỗi dừng lại.
        for (var day = today.AddDays(-1); day >= habit.StartDate; day = day.AddDays(-1))
        {
            if (!_scheduleCalculator.IsScheduled(habit, day))
            {
                continue;
            }

            if (isCompletedOn(day))
            {
                streak++;
            }
            else
            {
                break;
            }
        }

        return streak;
    }

    /// <summary>
    /// Chuỗi dài nhất trong toàn bộ lịch sử: chuỗi các ngày có lịch liên tiếp được hoàn thành.
    /// </summary>
    private int CalculateLongestStreak(Habit habit, DateOnly today, Func<DateOnly, bool> isCompletedOn)
    {
        var longest = 0;
        var run = 0;

        for (var day = habit.StartDate; day <= today; day = day.AddDays(1))
        {
            if (!_scheduleCalculator.IsScheduled(habit, day))
            {
                // Off-Day không phá vỡ chuỗi.
                continue;
            }

            if (isCompletedOn(day))
            {
                run++;
                if (run > longest)
                {
                    longest = run;
                }
            }
            else
            {
                run = 0;
            }
        }

        return longest;
    }

    /// <summary>
    /// Tỷ lệ hoàn thành trong 30 ngày gần nhất.
    /// Mẫu số chỉ tính các ngày có lịch và không trước ngày bắt đầu của thói quen.
    /// </summary>
    private double CalculateCompletionRate30Days(Habit habit, DateOnly today, Func<DateOnly, bool> isCompletedOn)
    {
        var windowStart = today.AddDays(-(CompletionWindowDays - 1));
        if (windowStart < habit.StartDate)
        {
            windowStart = habit.StartDate;
        }

        if (windowStart > today)
        {
            return 0.0;
        }

        var scheduledCount = 0;
        var completedCount = 0;

        for (var day = windowStart; day <= today; day = day.AddDays(1))
        {
            if (!_scheduleCalculator.IsScheduled(habit, day))
            {
                continue;
            }

            scheduledCount++;
            if (isCompletedOn(day))
            {
                completedCount++;
            }
        }

        return scheduledCount == 0 ? 0.0 : (double)completedCount / scheduledCount;
    }

    /// <summary>
    /// Gom các bản ghi theo ngày, bỏ qua những ngày trước ngày bắt đầu của thói quen.
    /// Khi có nhiều bản ghi cùng ngày, bản ghi cập nhật sau cùng được giữ lại.
    /// </summary>
    private static Dictionary<DateOnly, Entry> BuildEntryMap(Habit habit, IReadOnlyList<Entry> entries)
    {
        var byDay = new Dictionary<DateOnly, Entry>();

        foreach (var entry in entries)
        {
            if (entry.Day < habit.StartDate)
            {
                continue;
            }

            if (byDay.TryGetValue(entry.Day, out var existing))
            {
                if (entry.UpdatedAt >= existing.UpdatedAt)
                {
                    byDay[entry.Day] = entry;
                }
            }
            else
            {
                byDay[entry.Day] = entry;
            }
        }

        return byDay;
    }
}
