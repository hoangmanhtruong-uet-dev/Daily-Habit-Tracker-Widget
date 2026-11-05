namespace HabitGrid.Core.Domain;

public interface IStatsCalculator
{
    /// <summary>Tỷ lệ hoàn thành trong [from, to] theo SPEC-V2-01 §3.3; null khi mẫu số bằng 0.</summary>
    double? CompletionRate(Habit habit, IReadOnlyList<DayLog> logs, DateOnly from, DateOnly to);

    int TotalDone(IReadOnlyList<DayLog> logs);

    /// <summary>Tổng giá trị (Count/Duration) trong khoảng.</summary>
    long TotalValue(IReadOnlyList<DayLog> logs, DateOnly from, DateOnly to);

    /// <summary>Tỷ lệ hoàn thành theo từng thứ trong tuần; null cho thứ không có mẫu.</summary>
    IReadOnlyDictionary<DayOfWeek, double?> RateByWeekday(Habit habit, IReadOnlyList<DayLog> logs, DateOnly from, DateOnly to);

    /// <summary>Phân bố số lần check theo giờ (24 phần tử) theo múi giờ cho trước.</summary>
    int[] CheckInsByHour(IReadOnlyList<DayLog> logs, TimeZoneInfo? timeZone = null);
}

public sealed class StatsCalculator : IStatsCalculator
{
    private readonly DayOfWeek _weekStart;
    private readonly IScheduleCalculator _schedule;

    public StatsCalculator(DayOfWeek weekStart = DayOfWeek.Monday, IScheduleCalculator? schedule = null)
    {
        _weekStart = weekStart;
        _schedule = schedule ?? new ScheduleCalculator();
    }

    public double? CompletionRate(Habit habit, IReadOnlyList<DayLog> logs, DateOnly from, DateOnly to)
    {
        ArgumentNullException.ThrowIfNull(habit);
        ArgumentNullException.ThrowIfNull(logs);

        var start = from < habit.StartDate ? habit.StartDate : from;
        if (start > to)
        {
            return null;
        }

        var map = logs
            .Where(l => l.Day >= start && l.Day <= to)
            .GroupBy(l => l.Day)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(l => l.UpdatedAt).First());

        if (habit.Kind == HabitKind.Quit)
        {
            var days = to.DayNumber - start.DayNumber + 1;
            var failed = map.Values.Count(l => l.Status == DayStatus.Failed || l.Value > 0);
            return Clamp((days - failed) / (double)days);
        }

        return habit.Frequency switch
        {
            HabitFrequency.TimesPerWeek w => FlexibleRate(map, start, to, (ws, days) => w.Times * days / 7.0, week: true),
            HabitFrequency.TimesPerMonth m => FlexibleRate(map, start, to, (ms, days) => m.Times * days / (double)DateTime.DaysInMonth(ms.Year, ms.Month), week: false),
            HabitFrequency.EveryNDays e => WindowRate(map, start, to, e),
            _ => DailyRate(habit, map, start, to)
        };
    }

    public int TotalDone(IReadOnlyList<DayLog> logs)
    {
        ArgumentNullException.ThrowIfNull(logs);
        return logs.GroupBy(l => l.Day).Count(g => g.OrderByDescending(l => l.UpdatedAt).First().Status == DayStatus.Done);
    }

    public long TotalValue(IReadOnlyList<DayLog> logs, DateOnly from, DateOnly to)
    {
        ArgumentNullException.ThrowIfNull(logs);
        return logs.Where(l => l.Day >= from && l.Day <= to).Sum(l => (long)l.Value);
    }

    public IReadOnlyDictionary<DayOfWeek, double?> RateByWeekday(
        Habit habit,
        IReadOnlyList<DayLog> logs,
        DateOnly from,
        DateOnly to)
    {
        ArgumentNullException.ThrowIfNull(habit);
        ArgumentNullException.ThrowIfNull(logs);

        var result = new Dictionary<DayOfWeek, double?>();

        foreach (var weekday in Enum.GetValues<DayOfWeek>())
        {
            var scheduled = 0;
            var done = 0;
            var excluded = 0;

            var start = from < habit.StartDate ? habit.StartDate : from;
            for (var day = start; day <= to; day = day.AddDays(1))
            {
                if (day.DayOfWeek != weekday || !_schedule.IsScheduled(habit, day))
                {
                    continue;
                }

                scheduled++;
                var status = logs.Where(l => l.Day == day).OrderByDescending(l => l.UpdatedAt).FirstOrDefault()?.Status
                             ?? DayStatus.None;

                if (status == DayStatus.Done)
                {
                    done++;
                }
                else if (status is DayStatus.Skipped or DayStatus.Frozen)
                {
                    excluded++;
                }
            }

            var denominator = scheduled - excluded;
            result[weekday] = denominator <= 0 ? null : Clamp(done / (double)denominator);
        }

        return result;
    }

    public int[] CheckInsByHour(IReadOnlyList<DayLog> logs, TimeZoneInfo? timeZone = null)
    {
        ArgumentNullException.ThrowIfNull(logs);

        var zone = timeZone ?? TimeZoneInfo.Local;
        var buckets = new int[24];

        foreach (var log in logs)
        {
            if (log.CheckedAt is not { } checkedAt || log.Status != DayStatus.Done)
            {
                continue;
            }

            var utc = DateTime.SpecifyKind(checkedAt, DateTimeKind.Utc);
            buckets[TimeZoneInfo.ConvertTimeFromUtc(utc, zone).Hour]++;
        }

        return buckets;
    }

    private double? DailyRate(Habit habit, Dictionary<DateOnly, DayLog> map, DateOnly start, DateOnly to)
    {
        var scheduled = 0;
        var excluded = 0;
        var done = 0;

        for (var day = start; day <= to; day = day.AddDays(1))
        {
            if (!_schedule.IsScheduled(habit, day))
            {
                continue;
            }

            scheduled++;

            if (!map.TryGetValue(day, out var log))
            {
                continue;
            }

            if (log.Status == DayStatus.Done)
            {
                done++;
            }
            else if (log.Status is DayStatus.Skipped or DayStatus.Frozen)
            {
                excluded++;
            }
        }

        var denominator = scheduled - excluded;
        return denominator <= 0 ? null : Clamp(done / (double)denominator);
    }

    /// <summary>Tần suất theo tuần/tháng: mẫu số = Times × (số ngày đã qua của chu kỳ / độ dài chu kỳ).</summary>
    private double? FlexibleRate(
        Dictionary<DateOnly, DayLog> map,
        DateOnly start,
        DateOnly to,
        Func<DateOnly, int, double> expectedFor,
        bool week)
    {
        var expected = 0.0;
        var done = map.Values.Count(l => l.Status == DayStatus.Done);

        var cursor = start;
        while (cursor <= to)
        {
            var periodStart = week ? PeriodCalendar.StartOfWeek(cursor, _weekStart) : PeriodCalendar.StartOfMonth(cursor);
            var periodEnd = week ? periodStart.AddDays(6) : PeriodCalendar.EndOfMonth(periodStart);
            var overlapEnd = periodEnd < to ? periodEnd : to;
            var days = overlapEnd.DayNumber - cursor.DayNumber + 1;

            expected += expectedFor(periodStart, days);
            cursor = overlapEnd.AddDays(1);
        }

        return expected <= 0 ? null : Clamp(done / expected);
    }

    private static double? WindowRate(
        Dictionary<DateOnly, DayLog> map,
        DateOnly start,
        DateOnly to,
        HabitFrequency.EveryNDays frequency)
    {
        var first = PeriodCalendar.WindowIndex(start, frequency.Anchor, frequency.N);
        var last = PeriodCalendar.WindowIndex(to, frequency.Anchor, frequency.N);
        var windows = last - first + 1;

        var doneWindows = map.Values
            .Where(l => l.Status == DayStatus.Done)
            .Select(l => PeriodCalendar.WindowIndex(l.Day, frequency.Anchor, frequency.N))
            .Distinct()
            .Count();

        return windows <= 0 ? null : Clamp(doneWindows / (double)windows);
    }

    private static double Clamp(double value) => Math.Clamp(value, 0.0, 1.0);
}
