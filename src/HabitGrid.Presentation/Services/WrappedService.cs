using HabitGrid.Core.Domain;
using HabitGrid.Core.Domain.Insights;
using IDayProvider = HabitGrid.Core.Services.IDayProvider;

namespace HabitGrid.Presentation.Services;

/// <summary>Wrapped tháng (P1): gom thói quen + nhật ký của tháng và tháng trước để dựng số liệu. Xem đủ 6 slide và xuất ảnh là quyền Plus (kiểm ở UI).</summary>
public sealed class WrappedService
{
    private readonly IHabitRepository _habits;
    private readonly IDayLogRepository _logs;
    private readonly IDayProvider _days;
    private readonly IEntitlements _entitlements;
    private readonly ISettingsStore? _settings;

    /// <summary>Khóa lưu tháng đã ẩn banner ("yyyy-MM" của tháng đang xem banner).</summary>
    public const string BannerDismissedKey = "wrapped.banner.dismissed";

    /// <summary>Banner nhắc Wrapped hiện trong 7 ngày đầu mỗi tháng.</summary>
    public const int BannerDays = 7;

    public WrappedService(IHabitRepository habits, IDayLogRepository logs, IDayProvider days, IEntitlements entitlements, ISettingsStore? settings = null)
    {
        _settings = settings;
        _habits = habits ?? throw new ArgumentNullException(nameof(habits));
        _logs = logs ?? throw new ArgumentNullException(nameof(logs));
        _days = days ?? throw new ArgumentNullException(nameof(days));
        _entitlements = entitlements ?? throw new ArgumentNullException(nameof(entitlements));
    }

    public bool IsPlus => _entitlements.IsPlus;

    public DateOnly Today => _days.GetToday();

    /// <summary>Tháng hiện tại (tính đến hôm nay) hoặc tháng trước (<paramref name="monthsAgo"/> = 1).</summary>
    public async Task<MonthlyWrapped> BuildAsync(int monthsAgo = 1, CancellationToken ct = default)
    {
        var today = _days.GetToday();
        var month = PeriodCalendar.StartOfMonth(today).AddMonths(-Math.Max(0, monthsAgo));
        var habits = await _habits.GetAllAsync(ct);
        var logs = new List<DayLog>();
        var from = month.AddMonths(-1);
        var to = PeriodCalendar.EndOfMonth(month);

        foreach (var habit in habits.Where(h => h.DeletedAt is null))
        {
            logs.AddRange(await _logs.GetRangeAsync(habit.Id, from, to, ct));
        }

        return MonthlyWrappedBuilder.Build(habits, logs, month, today);
    }

    /// <summary>
    /// Wrapped của tháng trước nếu nên nhắc: trong 7 ngày đầu tháng, tháng trước có check-in và người dùng chưa ẩn banner tháng này.
    /// Banner chỉ là lời nhắc; xem đủ hay không vẫn theo quyền Plus khi mở.
    /// </summary>
    public async Task<MonthlyWrapped?> BannerAsync(CancellationToken ct = default)
    {
        var today = _days.GetToday();

        if (today.Day > BannerDays)
        {
            return null;
        }

        if (_settings is not null && await _settings.GetAsync(BannerDismissedKey, ct) == MonthKey(today))
        {
            return null;
        }

        var wrapped = await BuildAsync(1, ct);
        return wrapped.IsEmpty ? null : wrapped;
    }

    /// <summary>Ẩn banner cho đến đầu tháng sau.</summary>
    public Task DismissBannerAsync(CancellationToken ct = default) =>
        _settings is null ? Task.CompletedTask : _settings.SetAsync(BannerDismissedKey, MonthKey(_days.GetToday()), ct);

    private static string MonthKey(DateOnly day) => $"{day.Year}-{day.Month:00}";
}
