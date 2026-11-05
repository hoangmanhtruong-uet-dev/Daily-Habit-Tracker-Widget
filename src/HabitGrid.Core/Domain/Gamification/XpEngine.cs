namespace HabitGrid.Core.Domain.Gamification;

/// <summary>Một khoản XP đã tính được (trước khi áp trần theo ngày).</summary>
public sealed record XpAward(DateOnly Day, string Kind, int Amount, Guid? HabitId);

/// <summary>Kết quả tính XP: tổng, level hiện tại, tiến độ tới level kế, XP từng ngày (đã áp trần).</summary>
public sealed record XpSummary(
    long TotalXp,
    int Level,
    long XpIntoLevel,
    long XpForNextLevel,
    IReadOnlyDictionary<DateOnly, int> PerDay,
    IReadOnlyList<XpAward> Awards)
{
    public bool IsMaxLevel => Level >= LevelCurve.MaxLevel;

    /// <summary>Tiến độ 0–1 trong level hiện tại (1 khi đã đạt level tối đa).</summary>
    public double LevelProgress => IsMaxLevel || XpForNextLevel <= 0 ? 1 : Math.Clamp(XpIntoLevel / (double)XpForNextLevel, 0, 1);
}

/// <summary>Đường cong level (SPEC-V2-06 §2).</summary>
public static class LevelCurve
{
    public const int MaxLevel = 100;

    /// <summary>
    /// Tổng XP cần để ĐẠT level <paramref name="level"/>: <c>round(50 × n^1.6)</c> (level 1 = 0).
    /// Spec không nói rõ cộng dồn hay theo từng bậc; chọn tổng-để-đạt vì level 100 ≈ 79 nghìn XP (~1–3 năm), còn
    /// cách cộng dồn các bậc sẽ cần ~3 triệu XP (không thể đạt với trần 200 XP/ngày).
    /// </summary>
    public static long XpToReach(int level) => level <= 1 ? 0 : (long)Math.Round(50 * Math.Pow(Math.Min(level, MaxLevel), 1.6), MidpointRounding.AwayFromZero);

    public static int LevelFor(long totalXp)
    {
        var level = 1;

        while (level < MaxLevel && totalXp >= XpToReach(level + 1))
        {
            level++;
        }

        return level;
    }
}

/// <summary>
/// Tính XP thuần từ nhật ký (SPEC-V2-06 §1): cùng dữ liệu → cùng kết quả, nên tính lại được khi đổi quy tắc và
/// bỏ check thì XP tự thu hồi. Nguồn: hoàn thành 10, một phần 4, ngày hoàn hảo +20, mốc streak, ghi chú/mood +2
/// (tối đa 10/ngày), trần 200 XP/ngày. Bản ghi sửa muộn hơn 2 ngày so với ngày của nó không tạo XP.
/// </summary>
public static class XpEngine
{
    public const int DonePoints = 10;
    public const int PartialPoints = 4;
    public const int PerfectDayBonus = 20;
    public const int NotePoints = 2;
    public const int NoteDailyCap = 10;
    public const int DailyCap = 200;
    public const int MaxBackfillDays = 2;

    private static readonly (int Streak, int Bonus)[] Milestones = [(7, 50), (30, 150), (100, 500), (365, 2000)];

    public static XpSummary Compute(
        IReadOnlyList<Habit> habits,
        IReadOnlyList<DayLog> logs,
        DateOnly today,
        IScheduleCalculator? schedule = null,
        IStreakCalculator? streaks = null)
    {
        ArgumentNullException.ThrowIfNull(habits);
        ArgumentNullException.ThrowIfNull(logs);

        schedule ??= new ScheduleCalculator();
        streaks ??= new StreakCalculator(DayOfWeek.Monday, schedule);

        var byId = habits.Where(h => h.DeletedAt is null).ToDictionary(h => h.Id);
        var eligible = logs
            .Where(l => byId.ContainsKey(l.HabitId) && l.Day <= today && IsLegitimate(l))
            .ToList();

        var awards = new List<XpAward>();

        foreach (var log in eligible)
        {
            var habit = byId[log.HabitId];

            if (habit.Kind == HabitKind.Quit)
            {
                continue;
            }

            if (log.Status == DayStatus.Done)
            {
                awards.Add(new XpAward(log.Day, "done", DonePoints, log.HabitId));
            }
            else if (log.Status == DayStatus.Partial)
            {
                awards.Add(new XpAward(log.Day, "partial", PartialPoints, log.HabitId));
            }
        }

        // Ghi chú/mood: +2 mỗi bản ghi, tối đa 10 XP mỗi ngày từ nguồn này.
        foreach (var group in eligible.Where(l => !string.IsNullOrWhiteSpace(l.Note) || l.Mood is not null).GroupBy(l => l.Day))
        {
            var points = Math.Min(group.Count() * NotePoints, NoteDailyCap);

            if (points > 0)
            {
                awards.Add(new XpAward(group.Key, "note", points, null));
            }
        }

        awards.AddRange(PerfectDays(byId.Values, eligible, today, schedule));
        awards.AddRange(StreakMilestones(byId.Values, eligible, today, streaks));

        var perDay = awards
            .GroupBy(a => a.Day)
            .ToDictionary(g => g.Key, g => Math.Min(g.Sum(a => a.Amount), DailyCap));

        var total = perDay.Values.Sum(v => (long)v);
        var level = LevelCurve.LevelFor(total);
        var floor = LevelCurve.XpToReach(level);
        var next = level >= LevelCurve.MaxLevel ? 0 : LevelCurve.XpToReach(level + 1) - floor;

        return new XpSummary(total, level, total - floor, next, perDay, awards);
    }

    /// <summary>Bản ghi hợp lệ để tính XP: không bị sửa muộn quá 2 ngày so với ngày của nó (dữ liệu cũ không có CheckedAt thì chấp nhận).</summary>
    public static bool IsLegitimate(DayLog log)
    {
        if (log.CheckedAt is not { } checkedAt)
        {
            return true;
        }

        var checkedDay = DateOnly.FromDateTime(checkedAt.Kind == DateTimeKind.Utc ? checkedAt.ToLocalTime() : checkedAt);
        return checkedDay.DayNumber - log.Day.DayNumber <= MaxBackfillDays;
    }

    private static IEnumerable<XpAward> PerfectDays(IEnumerable<Habit> habits, IReadOnlyList<DayLog> logs, DateOnly today, IScheduleCalculator schedule)
    {
        var tracked = habits.Where(h => h.Kind != HabitKind.Quit).ToList();
        var logsByDay = logs.ToLookup(l => l.Day);

        foreach (var day in logsByDay.Select(g => g.Key).Where(d => d <= today).OrderBy(d => d))
        {
            var statusByHabit = logsByDay[day].GroupBy(l => l.HabitId).ToDictionary(g => g.Key, g => g.Last().Status);
            var counted = 0;
            var allDone = true;

            foreach (var habit in tracked)
            {
                if (day < habit.StartDate || (habit.ArchivedAt is { } archived && DateOnly.FromDateTime(archived) <= day) || !schedule.IsScheduled(habit, day))
                {
                    continue;
                }

                var status = statusByHabit.GetValueOrDefault(habit.Id, DayStatus.None);

                if (status is DayStatus.Skipped or DayStatus.Frozen)
                {
                    continue;
                }

                counted++;
                allDone &= status == DayStatus.Done;
            }

            if (counted > 0 && allDone)
            {
                yield return new XpAward(day, "perfect", PerfectDayBonus, null);
            }
        }
    }

    private static IEnumerable<XpAward> StreakMilestones(IEnumerable<Habit> habits, IReadOnlyList<DayLog> logs, DateOnly today, IStreakCalculator streaks)
    {
        foreach (var habit in habits.Where(h => h.Kind != HabitKind.Quit))
        {
            var mine = logs.Where(l => l.HabitId == habit.Id).OrderBy(l => l.Day).ToList();
            var doneDays = mine.Where(l => l.Status == DayStatus.Done).Select(l => l.Day).Distinct().ToList();
            var next = 0;

            for (var i = 0; i < doneDays.Count && next < Milestones.Length; i++)
            {
                // Chưa đủ số lần hoàn thành thì chắc chắn chưa chạm mốc kế tiếp (tránh tính streak vô ích).
                if (i + 1 < Milestones[next].Streak)
                {
                    continue;
                }

                var day = doneDays[i];
                var current = streaks.Calculate(habit, mine.Where(l => l.Day <= day).ToList(), day).Current;

                while (next < Milestones.Length && current >= Milestones[next].Streak)
                {
                    yield return new XpAward(day, "streak" + Milestones[next].Streak, Milestones[next].Bonus, habit.Id);
                    next++;
                }
            }
        }
    }
}
