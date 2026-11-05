namespace HabitGrid.Core.Domain;

/// <summary>Kết quả streak. <see cref="Unit"/> cho biết đơn vị (ngày/tuần/tháng/cửa sổ).</summary>
public sealed record StreakResult(
    int Current,
    int Best,
    StreakUnit Unit,
    bool AtRiskToday,
    DateOnly? LastCompleted);

public interface IStreakCalculator
{
    StreakResult Calculate(Habit habit, IReadOnlyList<DayLog> logs, DateOnly today);
}

/// <summary>
/// Tính streak theo SPEC-V2-01 §3.2. Mọi tần suất quy về một danh sách "chu kỳ" (ngày, tuần, tháng hoặc cửa sổ N ngày)
/// với kết quả Success / Neutral / Fail / Pending, rồi đếm chung một thuật toán:
/// Neutral (bỏ qua, đóng băng, hoặc chu kỳ bù được) không đứt và không cộng; chu kỳ hiện tại chưa xong (Pending) không làm đứt.
/// </summary>
public sealed class StreakCalculator : IStreakCalculator
{
    private enum Outcome
    {
        Success,
        Neutral,
        Fail,
        Pending
    }

    private readonly DayOfWeek _weekStart;
    private readonly IScheduleCalculator _schedule;

    public StreakCalculator(DayOfWeek weekStart = DayOfWeek.Monday, IScheduleCalculator? schedule = null)
    {
        _weekStart = weekStart;
        _schedule = schedule ?? new ScheduleCalculator();
    }

    public StreakResult Calculate(Habit habit, IReadOnlyList<DayLog> logs, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(habit);
        ArgumentNullException.ThrowIfNull(logs);

        var map = BuildMap(habit, logs, today);
        var unit = habit.Kind == HabitKind.Quit ? StreakUnit.Day : habit.Frequency.Unit;

        var lastCompleted = map.Values
            .Where(l => l.Status == DayStatus.Done)
            .Select(l => (DateOnly?)l.Day)
            .DefaultIfEmpty(null)
            .Max();

        if (today < habit.StartDate)
        {
            return new StreakResult(0, 0, unit, false, lastCompleted);
        }

        if (habit.Kind == HabitKind.Quit)
        {
            var (curQ, bestQ) = QuitStreak(habit, map, today);
            return new StreakResult(curQ, bestQ, unit, false, lastCompleted);
        }

        var periods = habit.Frequency switch
        {
            HabitFrequency.TimesPerWeek w => WeeklyPeriods(habit, map, today, w.Times),
            HabitFrequency.TimesPerMonth m => MonthlyPeriods(habit, map, today, m.Times),
            HabitFrequency.EveryNDays e => WindowPeriods(habit, map, today, e),
            _ => DailyPeriods(habit, map, today)
        };

        var (current, best) = Accumulate(periods);
        var atRisk = current > 0 && periods.Count > 0 && periods[^1] == Outcome.Pending;

        return new StreakResult(current, best, unit, atRisk, lastCompleted);
    }

    private static (int Current, int Best) Accumulate(List<Outcome> periods)
    {
        var current = 0;

        for (var i = periods.Count - 1; i >= 0; i--)
        {
            var outcome = periods[i];

            if (outcome == Outcome.Pending || outcome == Outcome.Neutral)
            {
                continue;
            }

            if (outcome == Outcome.Success)
            {
                current++;
            }
            else
            {
                break;
            }
        }

        var best = 0;
        var run = 0;

        foreach (var outcome in periods)
        {
            switch (outcome)
            {
                case Outcome.Success:
                    run++;
                    best = Math.Max(best, run);
                    break;
                case Outcome.Fail:
                case Outcome.Pending:
                    run = 0;
                    break;
            }
        }

        return (current, best);
    }

    private List<Outcome> DailyPeriods(Habit habit, Dictionary<DateOnly, DayLog> map, DateOnly today)
    {
        var periods = new List<Outcome>();

        for (var day = habit.StartDate; day <= today; day = day.AddDays(1))
        {
            if (!_schedule.IsScheduled(habit, day))
            {
                continue;
            }

            var status = map.TryGetValue(day, out var log) ? log.Status : DayStatus.None;

            periods.Add(status switch
            {
                DayStatus.Done => Outcome.Success,
                DayStatus.Skipped or DayStatus.Frozen => Outcome.Neutral,
                _ => day == today ? Outcome.Pending : Outcome.Fail
            });
        }

        return periods;
    }

    private List<Outcome> WeeklyPeriods(Habit habit, Dictionary<DateOnly, DayLog> map, DateOnly today, int times)
    {
        var periods = new List<Outcome>();
        var currentWeek = PeriodCalendar.StartOfWeek(today, _weekStart);

        for (var ws = PeriodCalendar.StartOfWeek(habit.StartDate, _weekStart); ws <= currentWeek; ws = ws.AddDays(7))
        {
            var from = ws < habit.StartDate ? habit.StartDate : ws;
            var available = ws.AddDays(6).DayNumber - from.DayNumber + 1;
            var required = Math.Min(times, available);

            periods.Add(Judge(map, from, ws.AddDays(6), required, isCurrent: ws == currentWeek));
        }

        return periods;
    }

    private static List<Outcome> MonthlyPeriods(Habit habit, Dictionary<DateOnly, DayLog> map, DateOnly today, int times)
    {
        var periods = new List<Outcome>();
        var currentMonth = PeriodCalendar.StartOfMonth(today);

        for (var ms = PeriodCalendar.StartOfMonth(habit.StartDate); ms <= currentMonth; ms = ms.AddMonths(1))
        {
            var from = ms < habit.StartDate ? habit.StartDate : ms;
            var end = PeriodCalendar.EndOfMonth(ms);
            var available = end.DayNumber - from.DayNumber + 1;
            var required = Math.Min(times, available);

            periods.Add(Judge(map, from, end, required, isCurrent: ms == currentMonth));
        }

        return periods;
    }

    private static List<Outcome> WindowPeriods(
        Habit habit,
        Dictionary<DateOnly, DayLog> map,
        DateOnly today,
        HabitFrequency.EveryNDays frequency)
    {
        var periods = new List<Outcome>();
        var first = PeriodCalendar.WindowIndex(habit.StartDate, frequency.Anchor, frequency.N);
        var last = PeriodCalendar.WindowIndex(today, frequency.Anchor, frequency.N);

        for (var index = first; index <= last; index++)
        {
            var start = PeriodCalendar.WindowStart(index, frequency.Anchor, frequency.N);
            var end = start.AddDays(frequency.N - 1);
            var from = start < habit.StartDate ? habit.StartDate : start;

            periods.Add(Judge(map, from, end, required: 1, isCurrent: index == last));
        }

        return periods;
    }

    /// <summary>Đánh giá một chu kỳ: đủ số lần Done là Success; bù bằng Skipped/Frozen thì Neutral.</summary>
    private static Outcome Judge(
        Dictionary<DateOnly, DayLog> map,
        DateOnly from,
        DateOnly to,
        int required,
        bool isCurrent)
    {
        var done = 0;
        var neutral = 0;

        for (var day = from; day <= to; day = day.AddDays(1))
        {
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
                neutral++;
            }
        }

        if (done >= required)
        {
            return Outcome.Success;
        }

        if (neutral > 0 && done + neutral >= required)
        {
            return Outcome.Neutral;
        }

        return isCurrent ? Outcome.Pending : Outcome.Fail;
    }

    private static (int Current, int Best) QuitStreak(Habit habit, Dictionary<DateOnly, DayLog> map, DateOnly today)
    {
        var failures = map.Values
            .Where(l => l.Day <= today && (l.Status == DayStatus.Failed || l.Value > 0))
            .Select(l => l.Day)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        var previous = habit.StartDate.AddDays(-1);
        var best = 0;

        foreach (var failure in failures)
        {
            best = Math.Max(best, failure.DayNumber - previous.DayNumber - 1);
            previous = failure;
        }

        var current = today.DayNumber - previous.DayNumber;
        best = Math.Max(best, current);

        return (Math.Max(0, current), Math.Max(0, best));
    }

    private static Dictionary<DateOnly, DayLog> BuildMap(Habit habit, IReadOnlyList<DayLog> logs, DateOnly today)
    {
        var map = new Dictionary<DateOnly, DayLog>();

        foreach (var log in logs)
        {
            if (log.Day < habit.StartDate || log.Day > today)
            {
                continue;
            }

            if (!map.TryGetValue(log.Day, out var existing) || log.UpdatedAt >= existing.UpdatedAt)
            {
                map[log.Day] = log;
            }
        }

        return map;
    }
}
