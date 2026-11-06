namespace HabitGrid.Core.Domain.Insights;

public sealed record WrappedHabit(Guid HabitId, string Name, string Icon, string ColorHue, int Count);

public enum WrappedSlideKind
{
    Total,
    TopHabit,
    Streak,
    GoldenHour,
    PerfectDays,
    Moment
}

/// <summary>Số liệu "Wrapped" của một tháng (P1). <see cref="To"/> dừng ở hôm nay nếu tháng chưa kết thúc.</summary>
public sealed record MonthlyWrapped(
    int Year,
    int Month,
    DateOnly From,
    DateOnly To,
    int TotalCheckIns,
    int ActiveDays,
    int PerfectDays,
    WrappedHabit? TopHabit,
    WrappedHabit? LongestStreak,
    int? GoldenHour,
    int? CheckInsDelta)
{
    public bool IsEmpty => TotalCheckIns == 0;

    public bool IsComplete(DateOnly today) => today > To || To == PeriodCalendar.EndOfMonth(From);
}

public static class WrappedSlides
{
    /// <summary>Free xem slide đầu tiên; Plus xem đủ 6 slide và xuất ảnh.</summary>
    public const int FreeSlides = 1;

    public static readonly IReadOnlyList<WrappedSlideKind> All = Enum.GetValues<WrappedSlideKind>();

    public static bool CanView(int index, bool isPlus) => isPlus || index < FreeSlides;
}

public static class MonthlyWrappedBuilder
{
    /// <summary>Tháng chứa <paramref name="month"/> (mọi ngày trong tháng đều được); ngày chưa tới không tính.</summary>
    public static MonthlyWrapped Build(
        IReadOnlyList<Habit> habits,
        IReadOnlyList<DayLog> logs,
        DateOnly month,
        DateOnly today,
        TimeZoneInfo? timeZone = null,
        IScheduleCalculator? schedule = null)
    {
        schedule ??= new ScheduleCalculator();
        var from = PeriodCalendar.StartOfMonth(month);
        var monthEnd = PeriodCalendar.EndOfMonth(month);
        var to = monthEnd < today ? monthEnd : today;
        var tracked = habits.Where(h => h.DeletedAt is null && h.Kind != HabitKind.Quit).ToList();
        var ids = tracked.ToDictionary(h => h.Id);
        var latest = InsightsCalculator.Latest(logs);

        var done = latest.Values
            .Where(l => l.Status == DayStatus.Done && l.Day >= from && l.Day <= to && ids.ContainsKey(l.HabitId))
            .ToList();

        var perHabit = done.GroupBy(l => l.HabitId)
            .Select(g => (Habit: ids[g.Key], Days: g.Select(l => l.Day).ToHashSet()))
            .ToList();

        var top = perHabit.OrderByDescending(p => p.Days.Count).ThenBy(p => p.Habit.Name, StringComparer.Ordinal).FirstOrDefault();
        WrappedHabit? topHabit = top.Habit is null ? null : Wrap(top.Habit, top.Days.Count);

        WrappedHabit? longest = null;
        var longestRun = 0;

        foreach (var (habit, days) in perHabit.OrderBy(p => p.Habit.Name, StringComparer.Ordinal))
        {
            var run = LongestRun(days);

            if (run > longestRun)
            {
                longestRun = run;
                longest = Wrap(habit, run);
            }
        }

        int? golden = null;
        var buckets = new StatsCalculator(DayOfWeek.Monday, schedule).CheckInsByHour(done, timeZone);

        if (buckets.Sum() > 0)
        {
            golden = Array.IndexOf(buckets, buckets.Max());
        }

        var perfect = InsightsCalculator.PerfectDays(tracked, latest, from, to, schedule).Count;

        // So với tháng trước, cùng số ngày đã trôi qua.
        var previousFrom = from.AddMonths(-1);
        var length = to.DayNumber - from.DayNumber;
        var previousTo = previousFrom.AddDays(Math.Min(length, DateTime.DaysInMonth(previousFrom.Year, previousFrom.Month) - 1));
        var previousDone = latest.Values.Count(l => l.Status == DayStatus.Done && l.Day >= previousFrom && l.Day <= previousTo && ids.ContainsKey(l.HabitId));
        int? delta = previousDone == 0 ? null : done.Count - previousDone;

        return new MonthlyWrapped(
            from.Year, from.Month, from, to,
            done.Count,
            done.Select(l => l.Day).Distinct().Count(),
            perfect,
            topHabit,
            longestRun >= 2 ? longest : null,
            golden,
            delta);
    }

    private static WrappedHabit Wrap(Habit habit, int count) => new(habit.Id, habit.Name, habit.Icon.Value, habit.ColorHue, count);

    private static int LongestRun(HashSet<DateOnly> days)
    {
        var best = 0;

        foreach (var day in days.Where(d => !days.Contains(d.AddDays(-1))))
        {
            var length = 1;

            while (days.Contains(day.AddDays(length)))
            {
                length++;
            }

            best = Math.Max(best, length);
        }

        return best;
    }
}
