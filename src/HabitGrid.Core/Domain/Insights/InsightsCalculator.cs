using System.Globalization;
using System.Text;

namespace HabitGrid.Core.Domain.Insights;

public sealed record WeekPoint(DateOnly WeekStart, double? Rate, double? MovingAverage);

public sealed record WeekdayRate(DayOfWeek Day, double? Rate, int Samples);

public sealed record HabitInsightRow(
    Guid HabitId,
    string Name,
    string Icon,
    string ColorHue,
    HabitKind Kind,
    string? Unit,
    double? Rate,
    int CurrentStreak,
    int BestStreak,
    StreakUnit StreakUnit,
    int DoneCount,
    long TotalValue,
    double? AveragePerActiveDay,
    int BestValue);

/// <summary>Mood: trung bình chung, ngày có ít nhất một thói quen xong và ngày không xong (thang 1–5).</summary>
public sealed record MoodSummary(double? Average, double? DoneDaysAverage, double? OtherDaysAverage, int Samples);

/// <summary>Báo cáo trang Insights cho khoảng [From, To] (SPEC-V2-07 §1).</summary>
public sealed record InsightsReport(
    DateOnly From,
    DateOnly To,
    double? OverallRate,
    int PerfectDays,
    int CheckIns,
    int BestStreak,
    IReadOnlyList<WeekPoint> Weeks,
    IReadOnlyList<WeekdayRate> Weekdays,
    IReadOnlyList<int> Hours,
    IReadOnlyList<HabitInsightRow> Habits,
    MoodSummary Mood)
{
    public bool IsEmpty => Habits.Count == 0;
}

/// <summary>
/// Tính số liệu trang Insights thuần từ dữ liệu: tỷ lệ chung (trung bình tỷ lệ các thói quen có mẫu), ngày hoàn hảo,
/// số lần check, theo thứ, theo giờ, xu hướng tuần + trung bình trượt 4 tuần, từng thói quen, mood.
/// Mọi phép duyệt dùng tra cứu theo ngày (không lọc lại cả danh sách mỗi ngày) để nhanh với nhiều năm dữ liệu.
/// </summary>
public static class InsightsCalculator
{
    public static InsightsReport Build(
        IReadOnlyList<Habit> habits,
        IReadOnlyList<DayLog> logs,
        DateOnly today,
        DateOnly from,
        DayOfWeek weekStart = DayOfWeek.Monday,
        IScheduleCalculator? schedule = null,
        IStreakCalculator? streaks = null,
        IStatsCalculator? stats = null,
        IReadOnlyDictionary<DateOnly, int>? dayMoods = null)
    {
        ArgumentNullException.ThrowIfNull(habits);
        ArgumentNullException.ThrowIfNull(logs);

        schedule ??= new ScheduleCalculator();
        streaks ??= new StreakCalculator(weekStart, schedule);
        stats ??= new StatsCalculator(weekStart, schedule);

        var to = today;
        var tracked = habits.Where(h => h.IsActive).OrderBy(h => h.SortKey, StringComparer.Ordinal).ToList();
        var byHabit = logs.GroupBy(l => l.HabitId).ToDictionary(g => g.Key, g => (IReadOnlyList<DayLog>)g.ToList());
        IReadOnlyList<DayLog> LogsOf(Habit h) => byHabit.TryGetValue(h.Id, out var l) ? l : [];

        var inRange = logs.Where(l => l.Day >= from && l.Day <= to && tracked.Any(h => h.Id == l.HabitId)).ToList();
        var latest = Latest(inRange);

        // Hôm nay chưa kết thúc: chỉ tính khi đã xong, không coi "chưa làm" là lỡ (tránh tỷ lệ tụt vô lý buổi sáng).
        DateOnly EndFor(Habit h) =>
            latest.TryGetValue((h.Id, today), out var log) && log.Status == DayStatus.Done ? today : today.AddDays(-1);

        // Từng thói quen.
        var rows = new List<HabitInsightRow>();

        foreach (var habit in tracked)
        {
            var mine = LogsOf(habit);
            var ranged = mine.Where(l => l.Day >= from && l.Day <= to).ToList();
            var streak = streaks.Calculate(habit, mine, today);
            var activeDays = Latest(ranged).Values.Where(l => l.Value > 0 && l.Status is not (DayStatus.Skipped or DayStatus.Frozen)).ToList();

            rows.Add(new HabitInsightRow(
                habit.Id,
                habit.Name,
                habit.Icon.Value,
                habit.ColorHue,
                habit.Kind,
                habit.Target.Unit,
                stats.CompletionRate(habit, mine, from, EndFor(habit)),
                streak.Current,
                streak.Best,
                streak.Unit,
                Latest(ranged).Values.Count(l => l.Status == DayStatus.Done),
                activeDays.Sum(l => (long)l.Value),
                activeDays.Count == 0 ? null : activeDays.Average(l => l.Value),
                activeDays.Count == 0 ? 0 : activeDays.Max(l => l.Value)));
        }

        var rated = rows.Where(r => r.Rate is not null && r.Kind != HabitKind.Quit).ToList();
        double? overall = rated.Count == 0 ? null : rated.Average(r => r.Rate!.Value);

        // Theo tuần + trung bình trượt 4 tuần.
        var weeks = new List<WeekPoint>();
        var firstWeek = PeriodCalendar.StartOfWeek(from, weekStart);

        for (var week = firstWeek; week <= to; week = week.AddDays(7))
        {
            var weekFrom = week < from ? from : week;
            var weekTo = week.AddDays(6) > to ? to : week.AddDays(6);
            var rates = tracked.Where(h => h.Kind != HabitKind.Quit)
                .Select(h => stats.CompletionRate(h, LogsOf(h), weekFrom, weekTo > EndFor(h) ? EndFor(h) : weekTo))
                .Where(r => r is not null)
                .Select(r => r!.Value)
                .ToList();

            weeks.Add(new WeekPoint(week, rates.Count == 0 ? null : rates.Average(), null));
        }

        for (var i = 0; i < weeks.Count; i++)
        {
            var window = weeks.Skip(Math.Max(0, i - 3)).Take(Math.Min(4, i + 1)).Where(w => w.Rate is not null).Select(w => w.Rate!.Value).ToList();
            weeks[i] = weeks[i] with { MovingAverage = window.Count == 0 ? null : window.Average() };
        }

        return new InsightsReport(
            from,
            to,
            overall,
            PerfectDays(tracked, latest, from, to, schedule).Count,
            latest.Values.Count(l => l.Status == DayStatus.Done && tracked.First(h => h.Id == l.HabitId).Kind != HabitKind.Quit),
            rows.Select(r => r.BestStreak).DefaultIfEmpty(0).Max(),
            weeks,
            Weekdays(tracked, latest, from, to, today, schedule),
            stats.CheckInsByHour(latest.Values.ToList()),
            rows,
            Mood(latest.Values.ToList(), dayMoods, from, to));
    }

    /// <summary>Ngày hoàn hảo: mọi thói quen có lịch đều Done/Skipped/Frozen và có ít nhất một Done.</summary>
    public static HashSet<DateOnly> PerfectDays(
        IReadOnlyList<Habit> habits,
        IReadOnlyDictionary<(Guid HabitId, DateOnly Day), DayLog> latest,
        DateOnly from,
        DateOnly to,
        IScheduleCalculator schedule)
    {
        var result = new HashSet<DateOnly>();
        var tracked = habits.Where(h => h.Kind != HabitKind.Quit).ToList();

        for (var day = from; day <= to; day = day.AddDays(1))
        {
            var due = tracked.Where(h => day >= h.StartDate && schedule.IsScheduled(h, day)).ToList();

            if (due.Count == 0)
            {
                continue;
            }

            var anyDone = false;
            var ok = true;

            foreach (var habit in due)
            {
                var status = latest.TryGetValue((habit.Id, day), out var log) ? log.Status : DayStatus.None;
                anyDone |= status == DayStatus.Done;
                ok &= status is DayStatus.Done or DayStatus.Skipped or DayStatus.Frozen;
            }

            if (ok && anyDone)
            {
                result.Add(day);
            }
        }

        return result;
    }

    private static IReadOnlyList<WeekdayRate> Weekdays(
        IReadOnlyList<Habit> habits,
        IReadOnlyDictionary<(Guid HabitId, DateOnly Day), DayLog> latest,
        DateOnly from,
        DateOnly to,
        DateOnly today,
        IScheduleCalculator schedule)
    {
        var done = new int[7];
        var due = new int[7];

        foreach (var habit in habits.Where(h => h.Kind != HabitKind.Quit))
        {
            for (var day = from < habit.StartDate ? habit.StartDate : from; day <= to; day = day.AddDays(1))
            {
                if (!schedule.IsScheduled(habit, day))
                {
                    continue;
                }

                var status = latest.TryGetValue((habit.Id, day), out var log) ? log.Status : DayStatus.None;

                if (status is DayStatus.Skipped or DayStatus.Frozen || (day == today && status != DayStatus.Done))
                {
                    continue;
                }

                var index = (int)day.DayOfWeek;
                due[index]++;

                if (status == DayStatus.Done)
                {
                    done[index]++;
                }
            }
        }

        return Enumerable.Range(0, 7)
            .Select(i => (DayOfWeek)(((int)DayOfWeek.Monday + i) % 7))
            .Select(d => new WeekdayRate(d, due[(int)d] == 0 ? null : done[(int)d] / (double)due[(int)d], due[(int)d]))
            .ToList();
    }

    /// <summary>
    /// Mood theo ngày: nếu người dùng đã ghi tâm trạng cả ngày (F4) thì dùng nó; không thì trung bình mood gắn với các thói quen ngày đó.
    /// </summary>
    private static MoodSummary Mood(IReadOnlyList<DayLog> logs, IReadOnlyDictionary<DateOnly, int>? dayMoods, DateOnly from, DateOnly to)
    {
        var byDay = logs.Where(l => l.Mood is >= 1 and <= 5)
            .GroupBy(l => l.Day)
            .ToDictionary(g => g.Key, g => g.Average(l => l.Mood!.Value));

        foreach (var (day, mood) in dayMoods ?? new Dictionary<DateOnly, int>())
        {
            if (day >= from && day <= to && mood is >= 1 and <= 5)
            {
                byDay[day] = mood;
            }
        }

        if (byDay.Count == 0)
        {
            return new MoodSummary(null, null, null, 0);
        }

        var doneDays = logs.Where(l => l.Status == DayStatus.Done).Select(l => l.Day).ToHashSet();
        var onDone = byDay.Where(p => doneDays.Contains(p.Key)).Select(p => p.Value).ToList();
        var onOther = byDay.Where(p => !doneDays.Contains(p.Key)).Select(p => p.Value).ToList();

        return new MoodSummary(
            byDay.Values.Average(),
            onDone.Count == 0 ? null : onDone.Average(),
            onOther.Count == 0 ? null : onOther.Average(),
            byDay.Count);
    }

    /// <summary>Bản ghi mới nhất cho mỗi (thói quen, ngày).</summary>
    public static Dictionary<(Guid HabitId, DateOnly Day), DayLog> Latest(IEnumerable<DayLog> logs) =>
        logs.GroupBy(l => (l.HabitId, l.Day)).ToDictionary(g => g.Key, g => g.OrderByDescending(l => l.UpdatedAt).First());
}

/// <summary>Xuất CSV (RFC 4180, UTF-8 có BOM để Excel đọc đúng tiếng Việt). Dữ liệu là của người dùng nên không bỏ cột nào.</summary>
public static class CsvExporter
{
    public static string Export(IReadOnlyList<Habit> habits, IReadOnlyList<DayLog> logs)
    {
        ArgumentNullException.ThrowIfNull(habits);
        ArgumentNullException.ThrowIfNull(logs);

        var byId = habits.ToDictionary(h => h.Id);
        var order = habits.OrderBy(h => h.SortKey, StringComparer.Ordinal).Select((h, i) => (h.Id, i)).ToDictionary(x => x.Id, x => x.i);
        var builder = new StringBuilder();
        builder.Append("date,habit,kind,value,target,unit,status,note,mood\r\n");

        foreach (var log in InsightsCalculator.Latest(logs).Values
                     .Where(l => byId.ContainsKey(l.HabitId))
                     .OrderBy(l => l.Day)
                     .ThenBy(l => order.GetValueOrDefault(l.HabitId)))
        {
            var habit = byId[log.HabitId];
            builder
                .Append(log.Day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(',')
                .Append(Field(habit.Name)).Append(',')
                .Append(habit.Kind.ToString().ToLowerInvariant()).Append(',')
                .Append(log.Value.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(habit.EffectiveTarget.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(Field(habit.Target.Unit ?? string.Empty)).Append(',')
                .Append(log.Status.ToString().ToLowerInvariant()).Append(',')
                .Append(Field(log.Note ?? string.Empty)).Append(',')
                .Append(log.Mood?.ToString(CultureInfo.InvariantCulture) ?? string.Empty)
                .Append("\r\n");
        }

        return builder.ToString();
    }

    /// <summary>Bọc ngoặc kép khi có dấu phẩy, ngoặc kép hoặc xuống dòng; nhân đôi ngoặc kép bên trong. Chặn công thức Excel (=,+,-,@).</summary>
    public static string Field(string value)
    {
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@')
        {
            value = "'" + value;
        }

        return value.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : value;
    }
}
