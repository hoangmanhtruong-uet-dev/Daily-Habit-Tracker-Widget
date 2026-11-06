using HabitGrid.Core.Domain;
using HabitGrid.Core.Domain.Moments;
using HabitGrid.Presentation.ViewModels;
using IDayProvider = HabitGrid.Core.Services.IDayProvider;

namespace HabitGrid.Presentation.Services;

/// <summary>Dữ liệu một thẻ chia sẻ kiểu photobooth (dải dọc 4 ô: ảnh, chuỗi, lưới 12 tuần, Buddy).</summary>
public sealed record ShareCard(
    string HabitName,
    string Icon,
    string ColorHue,
    int Streak,
    int BestStreak,
    StreakUnit StreakUnit,
    int? Rate30,
    HeatmapData Heatmap,
    int Level,
    BuddyStage Stage,
    string BuddyName,
    string? PhotoPath,
    DateOnly Day,
    ShareFrame Frame,
    bool ShowWatermark,
    ShareLayout Layout = ShareLayout.Single,
    IReadOnlyList<string>? StripPhotos = null);

/// <summary>
/// Dựng thẻ chia sẻ. Không đăng lên mạng xã hội nào: người dùng tự lưu/sao chép ảnh rồi đăng (không cần mật khẩu tài khoản khác).
/// Free: 2 khung và có logo HabitGrid nhỏ; Plus: mọi khung và tắt được logo.
/// </summary>
public sealed class ShareCardService
{
    public const int HeatmapWeeks = 12;

    private readonly IHabitRepository _habits;
    private readonly IDayLogRepository _logs;
    private readonly IProfileRepository _profile;
    private readonly IEntitlements _entitlements;
    private readonly IDayProvider _days;
    private readonly MomentStore? _moments;
    private readonly IStreakCalculator _streaks;
    private readonly IStatsCalculator _stats;

    public ShareCardService(
        IHabitRepository habits,
        IDayLogRepository logs,
        IProfileRepository profile,
        IEntitlements entitlements,
        IDayProvider days,
        MomentStore? moments = null,
        IStreakCalculator? streaks = null,
        IStatsCalculator? stats = null)
    {
        _habits = habits ?? throw new ArgumentNullException(nameof(habits));
        _logs = logs ?? throw new ArgumentNullException(nameof(logs));
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        _entitlements = entitlements ?? throw new ArgumentNullException(nameof(entitlements));
        _days = days ?? throw new ArgumentNullException(nameof(days));
        _moments = moments;
        _streaks = streaks ?? new StreakCalculator();
        _stats = stats ?? new StatsCalculator();
    }

    public bool IsPlus => _entitlements.IsPlus;

    public bool CanUse(ShareFrame frame) => ShareFrames.CanUse(frame, IsPlus);

    /// <param name="frameId">Khung muốn dùng; khung của Plus khi đang Free thì dùng khung mặc định.</param>
    /// <param name="hideWatermark">Chỉ có tác dụng với Plus.</param>
    /// <param name="photoPath">Ảnh chọn riêng; null thì lấy ảnh khoảnh khắc mới nhất của thói quen (nếu có).</param>
    /// <param name="layout">Dải 4 ảnh chỉ cho Plus và cần ≥ 2 ảnh ở các ngày khác nhau; nếu không đủ điều kiện thì tự dùng một ảnh.</param>
    public async Task<ShareCard?> BuildAsync(Guid habitId, string? frameId = null, bool hideWatermark = false, string? photoPath = null, ShareLayout layout = ShareLayout.Single)
    {
        if (await _habits.GetByIdAsync(habitId) is not { DeletedAt: null } habit)
        {
            return null;
        }

        var today = _days.GetToday();
        var logs = await _logs.GetAllForHabitAsync(habitId);
        var streak = _streaks.Calculate(habit, logs, today);
        var rate = _stats.CompletionRate(habit, logs, today.AddDays(-29), today);
        var profile = await _profile.GetAsync();
        var frame = ShareFrames.Find(frameId);

        if (!CanUse(frame))
        {
            frame = ShareFrames.Default;
        }

        var photo = photoPath ?? _moments?.ForHabit(habitId).FirstOrDefault()?.Path;
        IReadOnlyList<string>? strip = null;

        if (layout == ShareLayout.Strip4 && ShareLayouts.CanUse(layout, IsPlus) && _moments is not null)
        {
            var picked = ShareLayouts.PickStrip(_moments.ForHabit(habitId).Where(m => File.Exists(m.Path)));

            if (picked.Count >= ShareLayouts.StripMinPhotos)
            {
                strip = picked.Select(m => m.Path).ToList();
            }
        }

        return new ShareCard(
            habit.Name,
            habit.Icon.Value,
            habit.ColorHue,
            streak.Current,
            streak.Best,
            streak.Unit,
            rate is { } r ? (int)Math.Round(r * 100) : null,
            HeatmapBuilder.Build(habit, logs, today, HeatmapWeeks),
            profile.Level,
            BuddyViewModel.StageFor(profile.Level),
            profile.BuddyName,
            photo is not null && File.Exists(photo) ? photo : null,
            today,
            frame,
            ShowWatermark: !(IsPlus && hideWatermark),
            Layout: strip is null ? ShareLayout.Single : ShareLayout.Strip4,
            StripPhotos: strip);
    }
}
