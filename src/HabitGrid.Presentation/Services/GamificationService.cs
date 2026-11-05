using System.Globalization;
using HabitGrid.Core.Domain;
using HabitGrid.Core.Domain.Gamification;
using IDayProvider = HabitGrid.Core.Services.IDayProvider;

namespace HabitGrid.Presentation.Services;

/// <summary>Ảnh chụp trạng thái game hóa tại một thời điểm.</summary>
public sealed record GamificationSnapshot(
    XpSummary Xp,
    IReadOnlyList<AchievementResult> Achievements,
    int FreezeTokens,
    int OverallStreak,
    IReadOnlyList<string> NewlyUnlocked,
    int? LeveledUpTo);

/// <summary>
/// Gắn các engine thuần (XP, huy hiệu, freeze) vào dữ liệu thật: tính lại từ nhật ký, lưu hồ sơ/huy hiệu, thưởng token
/// freeze (mỗi 7 ngày giữ chuỗi tổng, mỗi lần lên level) và áp freeze cho ngày lỡ. Lần chạy đầu trên dữ liệu có sẵn
/// không ăn mừng (chỉ ghi nhận) để người dùng cũ không bị dội hàng loạt thông báo.
/// </summary>
public sealed class GamificationService
{
    public const string AutoFreezeKey = "freeze.auto";
    private const string StreakBlocksKey = "freeze.streakBlocks";
    private const string InitializedKey = "gamification.initialized";
    private const string FlagPrefix = "flag.";

    private readonly IHabitRepository _habits;
    private readonly IDayLogRepository _logs;
    private readonly IProfileRepository _profile;
    private readonly IAchievementRepository _achievements;
    private readonly ISettingsStore _settings;
    private readonly IDayProvider _days;
    private readonly IEntitlements _entitlements;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public GamificationService(
        IHabitRepository habits,
        IDayLogRepository logs,
        IProfileRepository profile,
        IAchievementRepository achievements,
        ISettingsStore settings,
        IDayProvider days,
        IEntitlements? entitlements = null)
    {
        _habits = habits ?? throw new ArgumentNullException(nameof(habits));
        _logs = logs ?? throw new ArgumentNullException(nameof(logs));
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        _achievements = achievements ?? throw new ArgumentNullException(nameof(achievements));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _days = days ?? throw new ArgumentNullException(nameof(days));
        _entitlements = entitlements ?? new FreeEntitlements();
    }

    /// <summary>Có mở khóa mới hoặc lên level (để giao diện ăn mừng).</summary>
    public event EventHandler<GamificationSnapshot>? Celebrated;

    private bool IsPlus => _entitlements.MaxActiveHabits > FreeEntitlements.FreeMaxActiveHabits;

    /// <summary>Ghi cờ khám phá (widget_pinned, quick_checkin_used, theme_changed, shared_card) cho huy hiệu cùng tên.</summary>
    public Task SetFlagAsync(string flag, CancellationToken ct = default) =>
        _settings.SetAsync(FlagPrefix + flag, "1", ct);

    public async Task<bool> GetAutoFreezeAsync(CancellationToken ct = default) =>
        await _settings.GetAsync(AutoFreezeKey, ct) != "0";

    public Task SetAutoFreezeAsync(bool enabled, CancellationToken ct = default) =>
        _settings.SetAsync(AutoFreezeKey, enabled ? "1" : "0", ct);

    /// <summary>Tính lại mọi thứ từ dữ liệu, lưu kết quả, và báo ăn mừng nếu có gì mới.</summary>
    public async Task<GamificationSnapshot> RefreshAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);

        try
        {
            var today = _days.GetToday();
            var habits = await _habits.GetAllAsync(ct);
            var logs = new List<DayLog>();

            foreach (var habit in habits)
            {
                logs.AddRange(await _logs.GetAllForHabitAsync(habit.Id, ct));
            }

            var xp = XpEngine.Compute(habits, logs, today);
            var flags = await LoadFlagsAsync(ct);
            var results = new AchievementEngine().Evaluate(new AchievementContext(habits, logs, today, flags));
            var overall = OverallStreak(logs, today);

            var profile = await _profile.GetAsync(ct);
            var known = await _achievements.GetAllAsync(ct);
            // Lần tính đầu tiên (trên dữ liệu có sẵn) chỉ ghi nhận, không ăn mừng; cờ lưu riêng để người dùng mới tinh cũng chỉ bị coi là "lần đầu" một lần.
            var firstRun = await _settings.GetAsync(InitializedKey, ct) != "1";
            if (firstRun)
            {
                await _settings.SetAsync(InitializedKey, "1", ct);
            }

            // Huy hiệu: mở khóa vĩnh viễn; chỉ báo những cái thật sự mới.
            var fresh = new List<string>();

            foreach (var result in results.Where(r => r.Unlocked))
            {
                if (await _achievements.UnlockAsync(result.Id, DateTime.UtcNow, ct))
                {
                    fresh.Add(result.Id);
                }
            }

            // Token freeze: +1 mỗi bậc 7 ngày chuỗi tổng mới đạt, +1 mỗi level lên.
            var tokens = profile.FreezeTokens;
            var storedBlocks = int.TryParse(await _settings.GetAsync(StreakBlocksKey, ct), NumberStyles.Integer, CultureInfo.InvariantCulture, out var b) ? b : 0;
            var blocks = FreezeEngine.EarnedFromStreak(overall);
            int? leveledUp = null;

            if (!firstRun)
            {
                if (blocks > storedBlocks)
                {
                    tokens = FreezeEngine.Add(tokens, blocks - storedBlocks, IsPlus);
                }

                if (xp.Level > profile.Level)
                {
                    tokens = FreezeEngine.Add(tokens, xp.Level - profile.Level, IsPlus);
                    leveledUp = xp.Level;
                }
            }

            // Chuỗi đứt thì nạp lại bộ đếm để lần chuỗi sau lại kiếm được token.
            if (blocks != storedBlocks)
            {
                await _settings.SetAsync(StreakBlocksKey, blocks.ToString(CultureInfo.InvariantCulture), ct);
            }

            if (profile.Xp != xp.TotalXp || profile.Level != xp.Level || profile.FreezeTokens != tokens)
            {
                await _profile.SaveAsync(profile with { Xp = xp.TotalXp, Level = xp.Level, FreezeTokens = tokens }, ct);
            }

            var snapshot = new GamificationSnapshot(xp, results, tokens, overall, firstRun ? [] : fresh, leveledUp);

            if (snapshot.NewlyUnlocked.Count > 0 || snapshot.LeveledUpTo is not null)
            {
                Celebrated?.Invoke(this, snapshot);
            }

            return snapshot;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Kiểm tra ngày vừa lỡ: nếu cứu được chuỗi tổng bằng token thì lập kế hoạch; tự áp khi "Tự dùng freeze" bật,
    /// ngược lại trả kế hoạch để giao diện hỏi. Trả <see cref="FreezePlan.None"/> khi không cần.
    /// </summary>
    public async Task<FreezePlan> CheckFreezeAsync(CancellationToken ct = default)
    {
        var today = _days.GetToday();
        var habits = (await _habits.GetAllActiveAsync(ct)).Where(h => h.Kind != HabitKind.Quit).ToList();
        var logs = new List<DayLog>();

        foreach (var habit in habits)
        {
            logs.AddRange(await _logs.GetAllForHabitAsync(habit.Id, ct));
        }

        var active = logs.Where(l => l.Status == DayStatus.Done).Select(l => l.Day).ToHashSet();
        var frozen = logs.Where(l => l.Status == DayStatus.Frozen).Select(l => l.Day).ToHashSet();
        var profile = await _profile.GetAsync(ct);
        var auto = await GetAutoFreezeAsync(ct);
        var plan = FreezeEngine.Plan(today, active, frozen, profile.FreezeTokens, auto);

        if (plan.HasPlan && !plan.NeedsConfirmation)
        {
            await ApplyFreezeAsync(plan, ct);
        }

        return plan;
    }

    /// <summary>Áp kế hoạch: đánh dấu Frozen cho mọi thói quen có lịch ngày đó (một token/ngày) và trừ token.</summary>
    public async Task ApplyFreezeAsync(FreezePlan plan, CancellationToken ct = default)
    {
        if (!plan.HasPlan)
        {
            return;
        }

        var schedule = new ScheduleCalculator();
        var habits = (await _habits.GetAllActiveAsync(ct)).Where(h => h.Kind != HabitKind.Quit && h.AllowFreeze).ToList();

        foreach (var day in plan.Days)
        {
            foreach (var habit in habits.Where(h => day >= h.StartDate && schedule.IsScheduled(h, day)))
            {
                if (await _logs.GetAsync(habit.Id, day, ct) is not null)
                {
                    continue;
                }

                await _logs.UpsertAsync(new DayLog
                {
                    HabitId = habit.Id,
                    Day = day,
                    Status = DayStatus.Frozen,
                    UpdatedAt = DateTime.UtcNow
                }, ct);
            }
        }

        var profile = await _profile.GetAsync(ct);
        await _profile.SaveAsync(profile with { FreezeTokens = Math.Max(0, profile.FreezeTokens - plan.TokensNeeded) }, ct);
    }

    private async Task<HashSet<string>> LoadFlagsAsync(CancellationToken ct)
    {
        var flags = new HashSet<string>();

        foreach (var definition in AchievementCatalog.Default.Where(d => d.Rule == "flag" && d.Flag is not null))
        {
            if (await _settings.GetAsync(FlagPrefix + definition.Flag, ct) == "1")
            {
                flags.Add(definition.Flag!);
            }
        }

        return flags;
    }

    /// <summary>Chuỗi tổng: số ngày liên tiếp có ≥ 1 thói quen xong; ngày đóng băng nối chuỗi nhưng không được đếm.</summary>
    public static int OverallStreak(IReadOnlyList<DayLog> logs, DateOnly today)
    {
        var done = logs.Where(l => l.Status == DayStatus.Done).Select(l => l.Day).ToHashSet();
        var frozen = logs.Where(l => l.Status == DayStatus.Frozen).Select(l => l.Day).ToHashSet();
        var day = done.Contains(today) ? today : today.AddDays(-1);
        var count = 0;

        for (var guard = 0; guard < 3700; guard++)
        {
            if (done.Contains(day))
            {
                count++;
            }
            else if (!frozen.Contains(day))
            {
                break;
            }

            day = day.AddDays(-1);
        }

        return count;
    }
}
