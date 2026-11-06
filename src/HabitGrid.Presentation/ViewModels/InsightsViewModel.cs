using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using HabitGrid.Core.Domain;
using HabitGrid.Core.Domain.Insights;
using HabitGrid.Core.Domain.Monetization;
using HabitGrid.Presentation.Services;
using IDayProvider = HabitGrid.Core.Services.IDayProvider;

namespace HabitGrid.Presentation.ViewModels;

/// <summary>Số liệu nâng cao (Plus): tương quan, tháng so với tháng, lịch năm gộp.</summary>
public sealed record AdvancedData(
    IReadOnlyList<HabitCorrelation> Correlations,
    MonthComparison Months,
    HeatmapData YearHeatmap);

public enum InsightsRange
{
    Days30,
    Days90,
    All
}

/// <summary>
/// Trang Insights (SPEC-V2-07 §1–§2). Gói Free xem 30 ngày; Plus xem 90 ngày, toàn bộ lịch sử và xuất CSV (SPEC-V2-10 §1).
/// Insight tự động chọn tối đa 3, không lặp lại cùng insight trong 7 ngày.
/// </summary>
public sealed partial class InsightsViewModel : ObservableObject
{
    private const string ShownKey = "insights.shown";

    private readonly IHabitRepository _habits;
    private readonly IDayLogRepository _logs;
    private readonly IDayProvider _days;
    private readonly IEntitlements _entitlements;
    private readonly ISettingsStore _settings;
    private readonly DailyMoodService _moods;

    public InsightsViewModel(IHabitRepository habits, IDayLogRepository logs, IDayProvider days, IEntitlements entitlements, ISettingsStore settings)
    {
        _moods = new DailyMoodService(settings ?? throw new ArgumentNullException(nameof(settings)), days ?? throw new ArgumentNullException(nameof(days)));
        _habits = habits ?? throw new ArgumentNullException(nameof(habits));
        _logs = logs ?? throw new ArgumentNullException(nameof(logs));
        _days = days ?? throw new ArgumentNullException(nameof(days));
        _entitlements = entitlements ?? throw new ArgumentNullException(nameof(entitlements));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    [ObservableProperty]
    private InsightsRange _range = InsightsRange.Days30;

    [ObservableProperty]
    private InsightsReport? _report;

    [ObservableProperty]
    private string? _errorMessage;

    public IReadOnlyList<Insight> Insights { get; private set; } = [];

    /// <summary>Số liệu nâng cao; null khi chưa có Plus (giao diện hiện thẻ giới thiệu + paywall).</summary>
    public AdvancedData? Advanced { get; private set; }

    public IReadOnlyList<Habit> Habits { get; private set; } = [];

    /// <summary>Quyền xem Insights nâng cao (P2): chỉ Plus.</summary>
    public bool CanUseAdvanced => IsPlus;

    private List<DayLog> _allLogs = [];
    private IReadOnlyList<Habit> _allHabits = [];

    public bool IsPlus => _entitlements is LiveEntitlements live ? live.IsPlus : _entitlements.MaxActiveHabits == int.MaxValue;

    public bool CanUse(InsightsRange range) => range == InsightsRange.Days30 || IsPlus;

    public bool CanExport => IsPlus;

    /// <summary>Đổi khoảng xem. Khoảng cần Plus mà chưa có thì không đổi và trả về false (giao diện mở paywall).</summary>
    public async Task<bool> SetRangeAsync(InsightsRange range)
    {
        if (!CanUse(range))
        {
            return false;
        }

        Range = range;
        await LoadAsync();
        return true;
    }

    public async Task LoadAsync()
    {
        try
        {
            ErrorMessage = null;

            if (!CanUse(Range))
            {
                Range = InsightsRange.Days30;
            }

            var today = _days.GetToday();
            var habits = await _habits.GetAllAsync();
            var logs = await LoadLogsAsync(habits);
            var from = Range switch
            {
                InsightsRange.Days90 => today.AddDays(-89),
                InsightsRange.All => logs.Count == 0 ? today.AddDays(-29) : Min(logs.Min(l => l.Day), habits.Where(h => h.IsActive).Select(h => h.StartDate).DefaultIfEmpty(today).Min()),
                _ => today.AddDays(-29)
            };

            _allLogs = logs;
            _allHabits = habits;
            Habits = habits.Where(h => h.IsActive && h.Kind != HabitKind.Quit).OrderBy(h => h.SortKey, StringComparer.Ordinal).ToList();
            Advanced = IsPlus
                ? new AdvancedData(
                    AdvancedInsights.Correlations(habits, logs, today),
                    AdvancedInsights.MonthOverMonth(habits, logs, today),
                    AdvancedInsights.YearHeatmap(habits, logs, today))
                : null;
            OnPropertyChanged(nameof(Advanced));
            OnPropertyChanged(nameof(Habits));
            OnPropertyChanged(nameof(CanUseAdvanced));
            var dayMoods = (await _moods.AllAsync()).ToDictionary(p => p.Key, p => p.Value.Mood);
            Report = InsightsCalculator.Build(habits, logs, today, from > today ? today : from, dayMoods: dayMoods);

            var shown = await LoadShownAsync();
            var ranked = InsightEngine.Evaluate(habits, logs, today);
            Insights = InsightEngine.Pick(ranked, shown, today);
            await SaveShownAsync(shown, Insights, today);
            OnPropertyChanged(nameof(Insights));
            OnPropertyChanged(nameof(IsPlus));
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    /// <summary>So sánh hai thói quen (Plus) trong khoảng đang xem; null nếu chưa có quyền hoặc chọn trùng/không tồn tại.</summary>
    public HabitComparison? Compare(Guid a, Guid b)
    {
        if (!CanUseAdvanced || a == b || Report is not { } report)
        {
            return null;
        }

        var habitA = _allHabits.FirstOrDefault(h => h.Id == a);
        var habitB = _allHabits.FirstOrDefault(h => h.Id == b);

        if (habitA is null || habitB is null)
        {
            return null;
        }

        return AdvancedInsights.Compare(
            habitA,
            habitB,
            _allLogs.Where(l => l.HabitId == a).ToList(),
            _allLogs.Where(l => l.HabitId == b).ToList(),
            _days.GetToday(),
            report.From);
    }

    /// <summary>Nội dung CSV toàn bộ dữ liệu (chỉ Plus); null nếu chưa có quyền.</summary>
    public async Task<string?> ExportCsvAsync()
    {
        if (!CanExport)
        {
            return null;
        }

        var habits = await _habits.GetAllAsync();
        return CsvExporter.Export(habits, await LoadLogsAsync(habits));
    }

    private async Task<List<DayLog>> LoadLogsAsync(IReadOnlyList<Habit> habits)
    {
        var logs = new List<DayLog>();

        foreach (var habit in habits)
        {
            logs.AddRange(await _logs.GetAllForHabitAsync(habit.Id));
        }

        return logs;
    }

    private async Task<Dictionary<string, DateOnly>> LoadShownAsync()
    {
        var raw = await _settings.GetAsync(ShownKey) ?? string.Empty;
        var result = new Dictionary<string, DateOnly>();

        foreach (var pair in raw.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=');

            if (parts.Length == 2 && DateOnly.TryParseExact(parts[1], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
            {
                result[parts[0]] = day;
            }
        }

        return result;
    }

    private async Task SaveShownAsync(Dictionary<string, DateOnly> shown, IReadOnlyList<Insight> picked, DateOnly today)
    {
        foreach (var insight in picked)
        {
            if (!shown.TryGetValue(insight.Identity, out var first) || today.DayNumber - first.DayNumber >= 7)
            {
                shown[insight.Identity] = today;
            }
        }

        // Giữ gọn: bỏ mục cũ hơn 30 ngày.
        var kept = shown.Where(p => today.DayNumber - p.Value.DayNumber <= 30)
            .Select(p => $"{p.Key}={p.Value:yyyy-MM-dd}");
        await _settings.SetAsync(ShownKey, string.Join(';', kept));
    }

    private static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;
}
