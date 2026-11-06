namespace HabitGrid.Core.Domain.Insights;

/// <summary>"Hôm nào làm A thì hay làm B": xác suất xong B khi A xong so với khi A không xong.</summary>
public sealed record HabitCorrelation(Habit A, Habit B, double BGivenA, double BGivenNotA, int Days)
{
    /// <summary>Chênh lệch xác suất (0–1); càng lớn càng "đi cùng nhau".</summary>
    public double Lift => BGivenA - BGivenNotA;
}

public sealed record WeekPair(DateOnly WeekStart, double? RateA, double? RateB);

/// <summary>So sánh hai thói quen trong cùng một khoảng thời gian.</summary>
public sealed record HabitComparison(
    Habit A,
    Habit B,
    double? RateA,
    double? RateB,
    int StreakA,
    int StreakB,
    int BestA,
    int BestB,
    int DoneA,
    int DoneB,
    int BothDone,
    IReadOnlyList<WeekPair> Weeks);

public sealed record RateDelta(Guid HabitId, string Name, string Icon, string ColorHue, double? ThisMonth, double? LastMonth)
{
    /// <summary>Thay đổi (điểm phần trăm, −100…100); null khi thiếu một trong hai tháng.</summary>
    public int? Points => ThisMonth is { } a && LastMonth is { } b ? (int)Math.Round((a - b) * 100) : null;
}

/// <summary>Tháng này (đến hôm nay) so với cùng khoảng ngày của tháng trước.</summary>
public sealed record MonthComparison(
    DateOnly ThisFrom,
    DateOnly ThisTo,
    DateOnly LastFrom,
    DateOnly LastTo,
    double? OverallThis,
    double? OverallLast,
    int CheckInsThis,
    int CheckInsLast,
    IReadOnlyList<RateDelta> Habits)
{
    public int? OverallPoints => OverallThis is { } a && OverallLast is { } b ? (int)Math.Round((a - b) * 100) : null;
}

/// <summary>Các phân tích sâu cho gói Plus (P2), đều là hàm thuần trên dữ liệu.</summary>
public static class AdvancedInsights
{
    public const int CorrelationWindowDays = 90;
    public const int MinTogetherDays = 14;
    public const int MinEachGroup = 5;
    public const double MinLift = 0.25;
    public const int MaxCorrelations = 5;
    public const int CompareWeeks = 12;

    /// <summary>
    /// Cặp thói quen "đi cùng nhau". Chỉ xét ngày cả hai đều có lịch và không bị bỏ qua/đóng băng trong 90 ngày gần nhất;
    /// cần ≥ 14 ngày chung và ≥ 5 ngày ở mỗi nhóm (A xong / A không xong) để không kết luận từ ít dữ liệu.
    /// </summary>
    public static IReadOnlyList<HabitCorrelation> Correlations(
        IReadOnlyList<Habit> habits,
        IReadOnlyList<DayLog> logs,
        DateOnly today,
        IScheduleCalculator? schedule = null)
    {
        schedule ??= new ScheduleCalculator();
        var tracked = habits.Where(h => h.IsActive && h.Kind != HabitKind.Quit).ToList();
        var latest = InsightsCalculator.Latest(logs);
        var from = today.AddDays(-CorrelationWindowDays + 1);
        var result = new List<HabitCorrelation>();

        DayStatus StatusOf(Habit h, DateOnly day) => latest.TryGetValue((h.Id, day), out var log) ? log.Status : DayStatus.None;

        bool Usable(Habit h, DateOnly day)
        {
            if (day < h.StartDate || !schedule.IsScheduled(h, day))
            {
                return false;
            }

            return StatusOf(h, day) is not (DayStatus.Skipped or DayStatus.Frozen) && !(day == today && StatusOf(h, day) != DayStatus.Done);
        }

        foreach (var a in tracked)
        {
            foreach (var b in tracked.Where(x => x.Id != a.Id))
            {
                var aDone = 0;
                var aNot = 0;
                var bGivenA = 0;
                var bGivenNot = 0;

                for (var day = from; day <= today; day = day.AddDays(1))
                {
                    if (!Usable(a, day) || !Usable(b, day))
                    {
                        continue;
                    }

                    var bDone = StatusOf(b, day) == DayStatus.Done ? 1 : 0;

                    if (StatusOf(a, day) == DayStatus.Done)
                    {
                        aDone++;
                        bGivenA += bDone;
                    }
                    else
                    {
                        aNot++;
                        bGivenNot += bDone;
                    }
                }

                if (aDone + aNot < MinTogetherDays || aDone < MinEachGroup || aNot < MinEachGroup)
                {
                    continue;
                }

                var correlation = new HabitCorrelation(a, b, bGivenA / (double)aDone, bGivenNot / (double)aNot, aDone + aNot);

                if (correlation.Lift >= MinLift)
                {
                    result.Add(correlation);
                }
            }
        }

        // Mỗi cặp không thứ tự chỉ giữ chiều có chênh lệch lớn hơn (tránh "A→B" và "B→A" lặp nhau).
        return result
            .GroupBy(c => string.CompareOrdinal(c.A.Id.ToString(), c.B.Id.ToString()) < 0 ? (c.A.Id, c.B.Id) : (c.B.Id, c.A.Id))
            .Select(g => g.OrderByDescending(c => c.Lift).First())
            .OrderByDescending(c => c.Lift)
            .ThenBy(c => c.A.Name, StringComparer.CurrentCulture)
            .Take(MaxCorrelations)
            .ToList();
    }

    public static HabitComparison Compare(
        Habit a,
        Habit b,
        IReadOnlyList<DayLog> logsA,
        IReadOnlyList<DayLog> logsB,
        DateOnly today,
        DateOnly from,
        DayOfWeek weekStart = DayOfWeek.Monday,
        IScheduleCalculator? schedule = null)
    {
        schedule ??= new ScheduleCalculator();
        var stats = new StatsCalculator(weekStart, schedule);
        var streaks = new StreakCalculator(weekStart, schedule);
        var latestA = InsightsCalculator.Latest(logsA);
        var latestB = InsightsCalculator.Latest(logsB);

        var doneA = latestA.Values.Where(l => l.Day >= from && l.Day <= today && l.Status == DayStatus.Done).Select(l => l.Day).ToHashSet();
        var doneB = latestB.Values.Where(l => l.Day >= from && l.Day <= today && l.Status == DayStatus.Done).Select(l => l.Day).ToHashSet();

        var lastWeek = PeriodCalendar.StartOfWeek(today, weekStart);
        var weeks = new List<WeekPair>();

        for (var i = CompareWeeks - 1; i >= 0; i--)
        {
            var start = lastWeek.AddDays(-7 * i);
            var end = start.AddDays(6) > today ? today : start.AddDays(6);
            weeks.Add(new WeekPair(start, stats.CompletionRate(a, logsA, start, end), stats.CompletionRate(b, logsB, start, end)));
        }

        var streakA = streaks.Calculate(a, logsA, today);
        var streakB = streaks.Calculate(b, logsB, today);

        return new HabitComparison(
            a,
            b,
            stats.CompletionRate(a, logsA, from, today),
            stats.CompletionRate(b, logsB, from, today),
            streakA.Current,
            streakB.Current,
            streakA.Best,
            streakB.Best,
            doneA.Count,
            doneB.Count,
            doneA.Intersect(doneB).Count(),
            weeks);
    }

    /// <summary>Tháng này (từ mùng 1 đến hôm nay) so với cùng số ngày đầu tháng trước.</summary>
    public static MonthComparison MonthOverMonth(
        IReadOnlyList<Habit> habits,
        IReadOnlyList<DayLog> logs,
        DateOnly today,
        IScheduleCalculator? schedule = null)
    {
        schedule ??= new ScheduleCalculator();
        var stats = new StatsCalculator(DayOfWeek.Monday, schedule);
        var thisFrom = new DateOnly(today.Year, today.Month, 1);
        var lastFrom = thisFrom.AddMonths(-1);
        var days = today.Day;
        var lastTo = lastFrom.AddDays(Math.Min(days, DateTime.DaysInMonth(lastFrom.Year, lastFrom.Month)) - 1);
        var byHabit = logs.GroupBy(l => l.HabitId).ToDictionary(g => g.Key, g => (IReadOnlyList<DayLog>)g.ToList());
        var tracked = habits.Where(h => h.IsActive && h.Kind != HabitKind.Quit).OrderBy(h => h.SortKey, StringComparer.Ordinal).ToList();

        // Hôm nay chưa kết thúc: chỉ tính khi đã xong, giống trang Insights.
        DateOnly EndFor(Habit h, IReadOnlyList<DayLog> l) =>
            l.Any(x => x.Day == today && x.Status == DayStatus.Done) ? today : today.AddDays(-1);

        var deltas = new List<RateDelta>();

        foreach (var habit in tracked)
        {
            var mine = byHabit.TryGetValue(habit.Id, out var l) ? l : [];
            var end = EndFor(habit, mine);
            deltas.Add(new RateDelta(
                habit.Id,
                habit.Name,
                habit.Icon.Value,
                habit.ColorHue,
                end >= thisFrom ? stats.CompletionRate(habit, mine, thisFrom, end) : null,
                stats.CompletionRate(habit, mine, lastFrom, lastTo)));
        }

        double? Average(IEnumerable<double?> values)
        {
            var list = values.Where(v => v is not null).Select(v => v!.Value).ToList();
            return list.Count == 0 ? null : list.Average();
        }

        int CheckIns(DateOnly from, DateOnly to) =>
            InsightsCalculator.Latest(logs).Values.Count(l => l.Day >= from && l.Day <= to && l.Status == DayStatus.Done && tracked.Any(h => h.Id == l.HabitId));

        return new MonthComparison(
            thisFrom,
            today,
            lastFrom,
            lastTo,
            Average(deltas.Select(d => d.ThisMonth)),
            Average(deltas.Select(d => d.LastMonth)),
            CheckIns(thisFrom, today),
            CheckIns(lastFrom, lastTo),
            deltas);
    }

    /// <summary>
    /// Lịch năm gộp mọi thói quen: mỗi ngày tô theo tỷ lệ thói quen đến hạn đã xong (0 / ≤ 33% / ≤ 66% / &lt; 100% / 100%).
    /// Dùng lại <see cref="HeatmapData"/> nên vẽ bằng cùng control heatmap.
    /// </summary>
    public static HeatmapData YearHeatmap(
        IReadOnlyList<Habit> habits,
        IReadOnlyList<DayLog> logs,
        DateOnly today,
        int weeks = 52,
        DayOfWeek weekStart = DayOfWeek.Monday,
        IScheduleCalculator? schedule = null)
    {
        schedule ??= new ScheduleCalculator();
        var tracked = habits.Where(h => h.IsActive && h.Kind != HabitKind.Quit).ToList();
        var latest = InsightsCalculator.Latest(logs);
        var lastWeek = PeriodCalendar.StartOfWeek(today, weekStart);
        var first = lastWeek.AddDays(-7 * (weeks - 1));
        var firstEver = tracked.Select(h => h.StartDate).DefaultIfEmpty(today).Min();
        var columns = new List<IReadOnlyList<HeatCell>>(weeks);

        for (var w = 0; w < weeks; w++)
        {
            var cells = new List<HeatCell>(7);

            for (var d = 0; d < 7; d++)
            {
                var day = first.AddDays(w * 7 + d);
                var due = 0;
                var done = 0;

                if (day <= today)
                {
                    foreach (var habit in tracked)
                    {
                        if (day < habit.StartDate || !schedule.IsScheduled(habit, day))
                        {
                            continue;
                        }

                        var status = latest.TryGetValue((habit.Id, day), out var log) ? log.Status : DayStatus.None;

                        if (status is DayStatus.Skipped or DayStatus.Frozen)
                        {
                            continue;
                        }

                        due++;
                        done += status == DayStatus.Done ? 1 : 0;
                    }
                }

                var fraction = due == 0 ? 0 : done / (double)due;
                var level = done == 0 ? 0 : fraction >= 1 ? 4 : fraction >= 0.67 ? 3 : fraction >= 0.34 ? 2 : 1;
                cells.Add(new HeatCell(day, level, level == 4 ? DayStatus.Done : DayStatus.None, done, day > today, day < firstEver, day == today, due > 0));
            }

            columns.Add(cells);
        }

        return new HeatmapData(columns, first, lastWeek.AddDays(6), weekStart);
    }
}
