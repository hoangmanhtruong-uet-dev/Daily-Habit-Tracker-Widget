using System.Reflection;
using System.Text.Json;

namespace HabitGrid.Core.Domain.Gamification;

/// <summary>Định nghĩa một huy hiệu, đọc từ <c>achievements.json</c> (thêm huy hiệu không cần đổi mã logic nếu dùng lại luật có sẵn).</summary>
public sealed record AchievementDefinition(
    string Id,
    string Group,
    string Rule,
    IReadOnlyList<int> Tiers,
    bool Secret = false,
    string? Tag = null,
    string? Flag = null,
    int Hour = 0);

/// <summary>Dữ liệu đầu vào để xét huy hiệu.</summary>
public sealed record AchievementContext(
    IReadOnlyList<Habit> Habits,
    IReadOnlyList<DayLog> Logs,
    DateOnly Today,
    IReadOnlySet<string> Flags,
    int FocusMinutes = 0);

/// <summary>Kết quả một huy hiệu: tiến độ, bậc đạt được (0 = chưa, 1–3 = đồng/bạc/vàng).</summary>
public sealed record AchievementResult(AchievementDefinition Definition, long Progress, int Tier)
{
    public string Id => Definition.Id;

    public bool Unlocked => Tier > 0;

    public long Target => Definition.Tiers[Math.Min(Math.Max(Tier, 1), Definition.Tiers.Count) - 1];

    /// <summary>Huy hiệu bí mật chưa đạt thì UI không được lộ điều kiện.</summary>
    public bool HideCondition => Definition.Secret && !Unlocked;
}

public static class AchievementCatalog
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Danh mục mặc định nhúng trong assembly.</summary>
    public static IReadOnlyList<AchievementDefinition> Default { get; } = LoadEmbedded();

    public static IReadOnlyList<AchievementDefinition> FromJson(string json) =>
        JsonSerializer.Deserialize<List<AchievementDefinition>>(json, Options) ?? [];

    private static IReadOnlyList<AchievementDefinition> LoadEmbedded()
    {
        var assembly = typeof(AchievementCatalog).Assembly;
        var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith("achievements.json", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return FromJson(reader.ReadToEnd());
    }
}

/// <summary>Xét huy hiệu thuần từ dữ liệu (SPEC-V2-06 §5): cùng dữ liệu → cùng kết quả.</summary>
public sealed class AchievementEngine
{
    private readonly IReadOnlyList<AchievementDefinition> _catalog;
    private readonly IScheduleCalculator _schedule;
    private readonly IStreakCalculator _streaks;

    public AchievementEngine(IReadOnlyList<AchievementDefinition>? catalog = null, IScheduleCalculator? schedule = null, IStreakCalculator? streaks = null)
    {
        _catalog = catalog ?? AchievementCatalog.Default;
        _schedule = schedule ?? new ScheduleCalculator();
        _streaks = streaks ?? new StreakCalculator(DayOfWeek.Monday, _schedule);
    }

    public IReadOnlyList<AchievementResult> Evaluate(AchievementContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var facts = new Facts(context, _schedule, _streaks);
        return _catalog.Select(d => Evaluate(d, facts)).ToList();
    }

    private static AchievementResult Evaluate(AchievementDefinition definition, Facts facts)
    {
        var progress = definition.Rule switch
        {
            "totalCheckins" => facts.TotalCheckins,
            "perfectDays" => facts.PerfectDays.Count,
            "activeHabits" => facts.ActiveHabits,
            "notes" => facts.Notes,
            "bestStreak" => facts.BestStreak,
            "comeback" => facts.HasComeback ? 1 : 0,
            "focusMinutes" => facts.Context.FocusMinutes,
            "tagValue" => facts.TagValue(definition.Tag ?? string.Empty),
            "checkinsBeforeHour" => facts.CheckinsBeforeHour(definition.Hour),
            "checkinsFromHour" => facts.CheckinsFromHour(definition.Hour),
            "weekendWarrior" => facts.HasPerfectWeekend ? 1 : 0,
            "flag" => facts.Context.Flags.Contains(definition.Flag ?? string.Empty) ? 1 : 0,
            "midnightCheckin" => facts.HasMidnightCheckin ? 1 : 0,
            "perfectStreak" => facts.LongestPerfectRun,
            "maxCheckinsInDay" => facts.MaxCheckinsInDay,
            _ => 0
        };

        var tier = definition.Tiers.Count(t => progress >= t);
        return new AchievementResult(definition, progress, tier);
    }

    private sealed class Facts
    {
        private readonly List<DayLog> _done;
        private readonly Dictionary<Guid, Habit> _habits;

        public Facts(AchievementContext context, IScheduleCalculator schedule, IStreakCalculator streaks)
        {
            Context = context;
            _habits = context.Habits.Where(h => h.DeletedAt is null).ToDictionary(h => h.Id);

            var legit = context.Logs.Where(l => _habits.ContainsKey(l.HabitId) && l.Day <= context.Today).ToList();
            _done = legit.Where(l => l.Status == DayStatus.Done && _habits[l.HabitId].Kind != HabitKind.Quit).ToList();

            TotalCheckins = _done.Count;
            Notes = legit.Count(l => !string.IsNullOrWhiteSpace(l.Note));
            ActiveHabits = _habits.Values.Count(h => h.IsActive);
            PerfectDays = ComputePerfectDays(legit, schedule);
            BestStreak = _habits.Values.Where(h => h.Kind != HabitKind.Quit)
                .Select(h => streaks.Calculate(h, legit.Where(l => l.HabitId == h.Id).ToList(), context.Today).Best)
                .DefaultIfEmpty(0).Max();
            HasComeback = ComputeComeback();
            MaxCheckinsInDay = _done.GroupBy(l => l.Day).Select(g => g.Count()).DefaultIfEmpty(0).Max();
            LongestPerfectRun = LongestRun(PerfectDays);
            HasPerfectWeekend = PerfectDays.Any(d => d.DayOfWeek == DayOfWeek.Saturday && PerfectDays.Contains(d.AddDays(1)));
            HasMidnightCheckin = _done.Any(l => l.CheckedAt is { } at && ToLocal(at) is { Hour: 0, Minute: 0 });
        }

        public AchievementContext Context { get; }

        public long TotalCheckins { get; }

        public long Notes { get; }

        public long ActiveHabits { get; }

        public HashSet<DateOnly> PerfectDays { get; }

        public long BestStreak { get; }

        public bool HasComeback { get; }

        public long MaxCheckinsInDay { get; }

        public long LongestPerfectRun { get; }

        public bool HasPerfectWeekend { get; }

        public bool HasMidnightCheckin { get; }

        public long TagValue(string tag) =>
            _done.Concat(Context.Logs.Where(l => l.Status == DayStatus.Partial && _habits.ContainsKey(l.HabitId)))
                .Where(l => _habits[l.HabitId].Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
                .Sum(l => (long)l.Value);

        public long CheckinsBeforeHour(int hour) =>
            _done.Count(l => l.CheckedAt is { } at && ToLocal(at).Hour < hour);

        public long CheckinsFromHour(int hour) =>
            _done.Count(l => l.CheckedAt is { } at && ToLocal(at).Hour >= hour);

        private static DateTime ToLocal(DateTime value) => value.Kind == DateTimeKind.Utc ? value.ToLocalTime() : value;

        private HashSet<DateOnly> ComputePerfectDays(List<DayLog> legit, IScheduleCalculator schedule)
        {
            var result = new HashSet<DateOnly>();
            var tracked = _habits.Values.Where(h => h.Kind != HabitKind.Quit).ToList();

            foreach (var group in legit.GroupBy(l => l.Day))
            {
                var status = group.GroupBy(l => l.HabitId).ToDictionary(g => g.Key, g => g.Last().Status);
                var counted = 0;
                var allDone = true;

                foreach (var habit in tracked)
                {
                    if (group.Key < habit.StartDate || !schedule.IsScheduled(habit, group.Key))
                    {
                        continue;
                    }

                    var s = status.GetValueOrDefault(habit.Id, DayStatus.None);

                    if (s is DayStatus.Skipped or DayStatus.Frozen)
                    {
                        continue;
                    }

                    counted++;
                    allDone &= s == DayStatus.Done;
                }

                if (counted > 0 && allDone)
                {
                    result.Add(group.Key);
                }
            }

            return result;
        }

        /// <summary>Quay lại: một habit từng có chuỗi ≥ 7 ngày liên tiếp, bị đứt, rồi sau đó lại đạt ≥ 7 ngày liên tiếp.</summary>
        private bool ComputeComeback()
        {
            foreach (var group in _done.GroupBy(l => l.HabitId))
            {
                var days = group.Select(l => l.Day).Distinct().OrderBy(d => d).ToList();
                var longRuns = 0;
                var run = 1;

                for (var i = 1; i <= days.Count; i++)
                {
                    if (i < days.Count && days[i].DayNumber - days[i - 1].DayNumber == 1)
                    {
                        run++;
                        continue;
                    }

                    if (days.Count > 0 && run >= 7)
                    {
                        longRuns++;
                    }

                    run = 1;
                }

                if (longRuns >= 2)
                {
                    return true;
                }
            }

            return false;
        }

        private static long LongestRun(HashSet<DateOnly> days)
        {
            long best = 0;

            foreach (var day in days)
            {
                if (days.Contains(day.AddDays(-1)))
                {
                    continue;
                }

                long length = 0;

                for (var d = day; days.Contains(d); d = d.AddDays(1))
                {
                    length++;
                }

                best = Math.Max(best, length);
            }

            return best;
        }
    }
}
