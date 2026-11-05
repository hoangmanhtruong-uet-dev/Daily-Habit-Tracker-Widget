using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using HabitGrid.Core.Domain;
using HabitGrid.Core.Domain.Insights;
using HabitGrid.Core.Domain.Monetization;
using HabitGrid.Presentation.Services;
using IDayProvider = HabitGrid.Core.Services.IDayProvider;

namespace HabitGrid.Presentation.ViewModels;

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

    public InsightsViewModel(IHabitRepository habits, IDayLogRepository logs, IDayProvider days, IEntitlements entitlements, ISettingsStore settings)
    {
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

            Report = InsightsCalculator.Build(habits, logs, today, from > today ? today : from);

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
