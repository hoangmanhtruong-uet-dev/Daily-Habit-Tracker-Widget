namespace HabitGrid.Core.Domain.Insights;

/// <summary>
/// Một insight tự động (SPEC-V2-07 §2). Không chứa câu chữ: giao diện dịch <see cref="Key"/> + <see cref="Params"/>
/// thành câu, nên dịch được và không có nội dung tự do.
/// </summary>
public sealed record Insight(string Key, double Score, IReadOnlyDictionary<string, string> Params, Guid? HabitId = null)
{
    /// <summary>Khóa để không lặp lại: cùng luật trên cùng thói quen.</summary>
    public string Identity => HabitId is { } id ? $"{Key}:{id:N}" : Key;
}

/// <summary>
/// Bộ luật insight không dùng AI. Mỗi luật có ngưỡng mẫu tối thiểu; thiếu mẫu thì không nói gì (tránh kết luận bừa).
/// </summary>
public static class InsightEngine
{
    public const int MaxShown = 3;

    public static IReadOnlyList<Insight> Evaluate(
        IReadOnlyList<Habit> habits,
        IReadOnlyList<DayLog> logs,
        DateOnly today,
        IScheduleCalculator? schedule = null,
        IStreakCalculator? streaks = null,
        IStatsCalculator? stats = null)
    {
        ArgumentNullException.ThrowIfNull(habits);
        ArgumentNullException.ThrowIfNull(logs);

        schedule ??= new ScheduleCalculator();
        streaks ??= new StreakCalculator(DayOfWeek.Monday, schedule);
        stats ??= new StatsCalculator(DayOfWeek.Monday, schedule);

        var tracked = habits.Where(h => h.IsActive && h.Kind != HabitKind.Quit).ToList();
        var byHabit = logs.GroupBy(l => l.HabitId).ToDictionary(g => g.Key, g => (IReadOnlyList<DayLog>)g.ToList());
        IReadOnlyList<DayLog> LogsOf(Habit h) => byHabit.TryGetValue(h.Id, out var l) ? l : [];

        var results = new List<Insight>();

        foreach (var habit in tracked)
        {
            var mine = LogsOf(habit);
            results.AddRange(WeakWeekday(habit, mine, today, schedule));
            results.AddRange(Momentum(habit, mine, today, stats));
            results.AddRange(NearMilestone(habit, mine, today, streaks));
            results.AddRange(MoodLink(habit, mine, logs, today));
        }

        results.AddRange(GoldenHour(logs.Where(l => tracked.Any(h => h.Id == l.HabitId)).ToList(), today));
        results.AddRange(StackSuggestion(tracked, byHabit, today));

        return results.OrderByDescending(i => i.Score).ToList();
    }

    /// <summary>Chọn tối đa 3 insight điểm cao nhất, khác luật nhau, bỏ những cái đã hiện trong 7 ngày (trừ khi hiện lần đầu hôm nay).</summary>
    public static IReadOnlyList<Insight> Pick(IReadOnlyList<Insight> ranked, IReadOnlyDictionary<string, DateOnly> firstShown, DateOnly today)
    {
        var picked = new List<Insight>();

        foreach (var insight in ranked)
        {
            if (picked.Count >= MaxShown || picked.Any(p => p.Key == insight.Key))
            {
                continue;
            }

            if (firstShown.TryGetValue(insight.Identity, out var shown) && shown != today && today.DayNumber - shown.DayNumber < 7)
            {
                continue;
            }

            picked.Add(insight);
        }

        return picked;
    }

    /// <summary>weak_weekday: một thứ thấp hơn trung bình của thói quen ≥ 25 điểm, thứ đó có ≥ 8 lần đến hạn (90 ngày).</summary>
    public static IEnumerable<Insight> WeakWeekday(Habit habit, IReadOnlyList<DayLog> logs, DateOnly today, IScheduleCalculator schedule)
    {
        var from = today.AddDays(-90);
        var latest = logs.GroupBy(l => l.Day).ToDictionary(g => g.Key, g => g.OrderByDescending(l => l.UpdatedAt).First().Status);
        var due = new int[7];
        var done = new int[7];

        for (var day = from < habit.StartDate ? habit.StartDate : from; day < today; day = day.AddDays(1))
        {
            if (!schedule.IsScheduled(habit, day))
            {
                continue;
            }

            var status = latest.GetValueOrDefault(day, DayStatus.None);

            if (status is DayStatus.Skipped or DayStatus.Frozen)
            {
                continue;
            }

            due[(int)day.DayOfWeek]++;
            done[(int)day.DayOfWeek] += status == DayStatus.Done ? 1 : 0;
        }

        var totalDue = due.Sum();

        if (totalDue == 0)
        {
            yield break;
        }

        var average = done.Sum() / (double)totalDue;
        var worst = Enumerable.Range(0, 7)
            .Where(i => due[i] >= 8)
            .Select(i => (Day: (DayOfWeek)i, Rate: done[i] / (double)due[i]))
            .OrderBy(x => x.Rate)
            .FirstOrDefault();

        if (worst.Rate is var rate && due[(int)worst.Day] >= 8 && average - rate >= 0.25)
        {
            yield return new Insight("weak_weekday", 0.5 + (average - rate),
                new Dictionary<string, string> { ["habit"] = habit.Name, ["weekday"] = ((int)worst.Day).ToString(System.Globalization.CultureInfo.InvariantCulture) },
                habit.Id);
        }
    }

    /// <summary>momentum_up (+15 điểm) / slipping (−20 điểm): 14 ngày gần nhất so với 14 ngày trước đó.</summary>
    public static IEnumerable<Insight> Momentum(Habit habit, IReadOnlyList<DayLog> logs, DateOnly today, IStatsCalculator stats)
    {
        var recent = stats.CompletionRate(habit, logs, today.AddDays(-14), today.AddDays(-1));
        var before = stats.CompletionRate(habit, logs, today.AddDays(-28), today.AddDays(-15));

        // Cần thói quen đã chạy đủ 28 ngày, không thì "14 ngày trước" là khoảng trống.
        if (recent is not { } r || before is not { } b || habit.StartDate > today.AddDays(-28))
        {
            yield break;
        }

        var delta = r - b;
        var percent = ((int)Math.Round(Math.Abs(delta) * 100)).ToString(System.Globalization.CultureInfo.InvariantCulture);

        if (delta >= 0.15)
        {
            yield return new Insight("momentum_up", 0.6 + delta, new Dictionary<string, string> { ["habit"] = habit.Name, ["delta"] = percent }, habit.Id);
        }
        else if (delta <= -0.20)
        {
            var suggested = habit.Kind is HabitKind.Count or HabitKind.Duration
                ? Math.Max(1, (int)Math.Round(habit.EffectiveTarget * 0.7)).ToString(System.Globalization.CultureInfo.InvariantCulture)
                : string.Empty;
            yield return new Insight("slipping", 0.7 - delta, new Dictionary<string, string> { ["habit"] = habit.Name, ["delta"] = percent, ["suggested"] = suggested }, habit.Id);
        }
    }

    /// <summary>near_milestone: còn ≤ 2 ngày tới mốc 7/30/100/365 và hôm nay chưa xong.</summary>
    public static IEnumerable<Insight> NearMilestone(Habit habit, IReadOnlyList<DayLog> logs, DateOnly today, IStreakCalculator streaks)
    {
        var streak = streaks.Calculate(habit, logs, today);

        if (streak.Unit != StreakUnit.Day || logs.Any(l => l.Day == today && l.Status == DayStatus.Done))
        {
            yield break;
        }

        foreach (var milestone in new[] { 7, 30, 100, 365 })
        {
            var left = milestone - streak.Current;

            if (left is >= 1 and <= 2 && streak.Current > 0)
            {
                yield return new Insight("near_milestone", 1.2 + milestone / 1000.0,
                    new Dictionary<string, string> { ["habit"] = habit.Name, ["n"] = left.ToString(System.Globalization.CultureInfo.InvariantCulture), ["milestone"] = milestone.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                    habit.Id);
                yield break;
            }
        }
    }

    /// <summary>mood_link: mood ngày xong thói quen cao hơn ngày không xong ≥ 0,7; cần ≥ 14 ngày có mood (≥ 5 mỗi nhóm), 60 ngày gần nhất.</summary>
    public static IEnumerable<Insight> MoodLink(Habit habit, IReadOnlyList<DayLog> mine, IReadOnlyList<DayLog> all, DateOnly today)
    {
        var from = today.AddDays(-60);
        var moodByDay = all.Where(l => l.Day >= from && l.Day <= today && l.Mood is >= 1 and <= 5)
            .GroupBy(l => l.Day)
            .ToDictionary(g => g.Key, g => g.Average(l => l.Mood!.Value));

        if (moodByDay.Count < 14)
        {
            yield break;
        }

        var doneDays = mine.Where(l => l.Status == DayStatus.Done).Select(l => l.Day).ToHashSet();
        var onDone = moodByDay.Where(p => doneDays.Contains(p.Key)).Select(p => p.Value).ToList();
        var onOther = moodByDay.Where(p => !doneDays.Contains(p.Key)).Select(p => p.Value).ToList();

        if (onDone.Count < 5 || onOther.Count < 5)
        {
            yield break;
        }

        var gap = onDone.Average() - onOther.Average();

        if (gap >= 0.7)
        {
            yield return new Insight("mood_link", 0.8 + gap / 5, new Dictionary<string, string> { ["habit"] = habit.Name }, habit.Id);
        }
    }

    /// <summary>golden_hour: ≥ 60% lần check (≥ 10 lần, 30 ngày) rơi vào cùng một khung 2 giờ.</summary>
    public static IEnumerable<Insight> GoldenHour(IReadOnlyList<DayLog> logs, DateOnly today)
    {
        var from = today.AddDays(-30);
        var hours = logs
            .Where(l => l.Day >= from && l.Day <= today && l.Status == DayStatus.Done && l.CheckedAt is not null)
            .Select(l => (l.CheckedAt!.Value.Kind == DateTimeKind.Utc ? l.CheckedAt.Value.ToLocalTime() : l.CheckedAt.Value).Hour)
            .ToList();

        if (hours.Count < 10)
        {
            yield break;
        }

        var best = Enumerable.Range(0, 24)
            .Select(h => (Start: h, Count: hours.Count(x => x == h || x == (h + 1) % 24)))
            .OrderByDescending(x => x.Count)
            .First();

        var share = best.Count / (double)hours.Count;

        if (share >= 0.6)
        {
            yield return new Insight("golden_hour", 0.4 + share / 2,
                new Dictionary<string, string>
                {
                    ["start"] = $"{best.Start:00}:00",
                    ["end"] = $"{(best.Start + 2) % 24:00}:00"
                });
        }
    }

    /// <summary>stack_suggestion: hai thói quen được check cách nhau &lt; 15 phút ở ≥ 5 ngày và ≥ 60% số ngày cùng làm (30 ngày).</summary>
    public static IEnumerable<Insight> StackSuggestion(IReadOnlyList<Habit> habits, IReadOnlyDictionary<Guid, IReadOnlyList<DayLog>> byHabit, DateOnly today)
    {
        var from = today.AddDays(-30);
        Dictionary<DateOnly, DateTime> Times(Habit h) =>
            (byHabit.TryGetValue(h.Id, out var l) ? l : [])
            .Where(x => x.Day >= from && x.Status == DayStatus.Done && x.CheckedAt is not null)
            .GroupBy(x => x.Day)
            .ToDictionary(g => g.Key, g => g.Max(x => x.CheckedAt!.Value));

        var times = habits.ToDictionary(h => h.Id, Times);
        Insight? best = null;

        for (var i = 0; i < habits.Count; i++)
        {
            for (var j = i + 1; j < habits.Count; j++)
            {
                var a = times[habits[i].Id];
                var b = times[habits[j].Id];
                var shared = a.Keys.Where(b.ContainsKey).ToList();

                if (shared.Count < 5)
                {
                    continue;
                }

                var close = shared.Count(d => Math.Abs((a[d] - b[d]).TotalMinutes) < 15);
                var ratio = close / (double)shared.Count;

                if (close >= 5 && ratio >= 0.6 && (best is null || ratio + 0.3 > best.Score))
                {
                    best = new Insight("stack_suggestion", 0.3 + ratio, new Dictionary<string, string> { ["a"] = habits[i].Name, ["b"] = habits[j].Name });
                }
            }
        }

        if (best is not null)
        {
            yield return best;
        }
    }
}
