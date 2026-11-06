namespace HabitGrid.Core.Domain.Insights;

public sealed record HabitWeek(Guid HabitId, string Name, string Icon, double Rate, int Done);

public enum RecapTipKind
{
    /// <summary>Tuần trước chưa có check-in nào.</summary>
    NoData,

    /// <summary>Tuần trước rất tốt (≥ 90%).</summary>
    Praise,

    /// <summary>Có một thói quen yếu rõ rệt: gợi ý chọn nó làm ý định tuần này.</summary>
    FocusWeakest,

    /// <summary>Cao hơn tuần trước đó.</summary>
    Improving,

    /// <summary>Giữ nhịp.</summary>
    Steady
}

/// <summary>Tổng kết một tuần (thứ Hai → Chủ nhật): tỷ lệ, so với tuần trước đó, ngày tốt nhất, thói quen nổi bật/yếu và một gợi ý.</summary>
public sealed record WeeklyRecap(
    DateOnly WeekStart,
    DateOnly WeekEnd,
    double? Rate,
    double? PreviousRate,
    int CheckIns,
    int PerfectDays,
    DateOnly? BestDay,
    HabitWeek? BestHabit,
    HabitWeek? WeakestHabit,
    RecapTipKind Tip)
{
    /// <summary>Chênh lệch điểm phần trăm so với tuần trước đó; null nếu thiếu một trong hai.</summary>
    public int? Points => Rate is { } r && PreviousRate is { } p ? (int)Math.Round((r - p) * 100) : null;
}

public static class WeeklyRecapBuilder
{
    public const double PraiseRate = 0.9;
    public const double WeakRate = 0.5;
    public const int MinDueForWeak = 3;

    /// <summary>Tổng kết tuần đầy đủ gần nhất trước <paramref name="today"/> (không tính tuần đang chạy).</summary>
    public static WeeklyRecap Build(IReadOnlyList<Habit> habits, IReadOnlyList<DayLog> logs, DateOnly today, IScheduleCalculator? schedule = null)
    {
        schedule ??= new ScheduleCalculator();
        var thisWeek = PeriodCalendar.StartOfWeek(today, DayOfWeek.Monday);
        var start = thisWeek.AddDays(-7);
        var end = thisWeek.AddDays(-1);
        var tracked = habits.Where(h => h.IsActive && h.Kind != HabitKind.Quit && h.StartDate <= end).ToList();
        var stats = new StatsCalculator(DayOfWeek.Monday, schedule);
        var latest = InsightsCalculator.Latest(logs);

        double? Overall(DateOnly from, DateOnly to)
        {
            var rates = tracked.Where(h => h.StartDate <= to)
                .Select(h => stats.CompletionRate(h, logs.Where(l => l.HabitId == h.Id).ToList(), from, to))
                .Where(r => r is not null)
                .Select(r => r!.Value)
                .ToList();
            return rates.Count == 0 ? null : rates.Average();
        }

        var rows = new List<HabitWeek>();

        foreach (var habit in tracked)
        {
            var habitLogs = logs.Where(l => l.HabitId == habit.Id).ToList();

            if (stats.CompletionRate(habit, habitLogs, start, end) is { } rate)
            {
                var done = latest.Values.Count(l => l.HabitId == habit.Id && l.Day >= start && l.Day <= end && l.Status == DayStatus.Done);
                rows.Add(new HabitWeek(habit.Id, habit.Name, habit.Icon.Value, rate, done));
            }
        }

        var checkIns = rows.Sum(r => r.Done);
        var perfect = InsightsCalculator.PerfectDays(tracked, latest, start, end, schedule).Count;

        DateOnly? bestDay = null;
        var bestCount = 0;

        for (var day = start; day <= end; day = day.AddDays(1))
        {
            var count = latest.Values.Count(l => l.Day == day && l.Status == DayStatus.Done && tracked.Any(h => h.Id == l.HabitId));

            if (count > bestCount)
            {
                bestCount = count;
                bestDay = day;
            }
        }

        var best = rows.Where(r => r.Done > 0).OrderByDescending(r => r.Rate).ThenByDescending(r => r.Done).FirstOrDefault();
        var weakest = rows.Count >= 2
            ? rows.Where(r => r.Rate < WeakRate && DueCount(tracked.First(h => h.Id == r.HabitId), start, end, schedule) >= MinDueForWeak)
                .OrderBy(r => r.Rate).FirstOrDefault()
            : null;
        var rate0 = Overall(start, end);
        var previous = Overall(start.AddDays(-7), start.AddDays(-1));

        var tip = checkIns == 0 ? RecapTipKind.NoData
            : rate0 >= PraiseRate ? RecapTipKind.Praise
            : weakest is not null ? RecapTipKind.FocusWeakest
            : rate0 > previous ? RecapTipKind.Improving
            : RecapTipKind.Steady;

        return new WeeklyRecap(start, end, rate0, previous, checkIns, perfect, bestDay, best, tip == RecapTipKind.FocusWeakest ? weakest : null, tip);
    }

    private static int DueCount(Habit habit, DateOnly from, DateOnly to, IScheduleCalculator schedule)
    {
        var count = 0;

        for (var day = from < habit.StartDate ? habit.StartDate : from; day <= to; day = day.AddDays(1))
        {
            if (schedule.IsScheduled(habit, day))
            {
                count++;
            }
        }

        return count;
    }
}
