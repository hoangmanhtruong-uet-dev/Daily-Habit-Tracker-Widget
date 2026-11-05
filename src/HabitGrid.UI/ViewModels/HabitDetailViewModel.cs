using CommunityToolkit.Mvvm.ComponentModel;
using HabitGrid.Core.Models;
using HabitGrid.Core.Services;
using HabitGrid.Core.Repositories;
using HabitGrid.UI.Controls;

namespace HabitGrid.UI.ViewModels;

/// <summary>
/// ViewModel của trang chi tiết thói quen:
/// 4 thẻ chỉ số, lưới 52 tuần và biểu đồ cột 12 tháng.
/// </summary>
public partial class HabitDetailViewModel : ObservableObject
{
    private readonly IEntryRepository _entries;
    private readonly IStreakCalculator _streakCalculator;
    private readonly IScheduleCalculator _scheduleCalculator;
    private readonly IPaletteGenerator _paletteGenerator;
    private readonly IDayProvider _dayProvider;

    [ObservableProperty]
    private int _currentStreak;

    [ObservableProperty]
    private int _longestStreak;

    [ObservableProperty]
    private double _completionRate;

    [ObservableProperty]
    private int _totalCompleted;

    [ObservableProperty]
    private HeatmapGridModel? _gridModel;

    [ObservableProperty]
    private IReadOnlyList<MonthBar> _monthBars = Array.Empty<MonthBar>();

    [ObservableProperty]
    private bool _isDarkTheme;

    public HabitDetailViewModel(
        Habit habit,
        IEntryRepository entries,
        IStreakCalculator streakCalculator,
        IDayProvider dayProvider,
        DateOnly today,
        IScheduleCalculator? scheduleCalculator = null,
        IPaletteGenerator? paletteGenerator = null,
        bool isDarkTheme = false)
    {
        ArgumentNullException.ThrowIfNull(habit);
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(streakCalculator);
        ArgumentNullException.ThrowIfNull(dayProvider);

        Habit = habit;
        _entries = entries;
        _streakCalculator = streakCalculator;
        _dayProvider = dayProvider;
        _scheduleCalculator = scheduleCalculator ?? new ScheduleCalculator();
        _paletteGenerator = paletteGenerator ?? new PaletteGenerator();
        Today = today;
        IsDarkTheme = isDarkTheme;
    }

    /// <summary>Thói quen đang xem chi tiết.</summary>
    public Habit Habit { get; }

    public Guid Id => Habit.Id;

    public string Name => Habit.Name;

    public DateOnly Today { get; }

    /// <summary>True nếu thói quen bị khoá chỉ đọc.</summary>
    public bool IsReadOnly => Habit.IsReadOnly;

    /// <summary>Chuỗi hiện tại dạng chữ.</summary>
    public string CurrentStreakText => $"{CurrentStreak} ngày";

    /// <summary>Kỷ lục dạng chữ.</summary>
    public string LongestStreakText => $"{LongestStreak} ngày";

    /// <summary>Tỷ lệ 30 ngày dạng phần trăm.</summary>
    public string CompletionRateText => $"{CompletionRate * 100:F0}%";

    /// <summary>Tổng số lần hoàn thành dạng chữ.</summary>
    public string TotalCompletedText => $"{TotalCompleted} lần";

    /// <summary>Được gọi khi người dùng bấm vào một ô của lưới 52 tuần.</summary>
    public event EventHandler<DateOnly>? MarkRequested;

    /// <summary>Nạp lại toàn bộ số liệu và dữ liệu hiển thị.</summary>
    public async Task LoadAsync()
    {
        var entries = await _entries.GetAllForHabitAsync(Habit.Id);
        var result = _streakCalculator.Calculate(Habit, entries, Today);

        CurrentStreak = result.CurrentStreak;
        LongestStreak = result.LongestStreak;
        CompletionRate = result.CompletionRate30Days;
        TotalCompleted = result.TotalCompletedDays;

        var values = entries.ToDictionary(e => e.Day, e => e.Value);
        var shades = _paletteGenerator.GetPreset(Habit.PaletteId, IsDarkTheme);
        var layout = new HeatmapLayout(WidgetSize.Large, Today);

        GridModel = new HeatmapGridModel(Habit, values, shades, Today, layout, Habit.IsReadOnly, _scheduleCalculator);
        MonthBars = BuildMonthBars(entries);

        OnPropertyChanged(nameof(CurrentStreakText));
        OnPropertyChanged(nameof(LongestStreakText));
        OnPropertyChanged(nameof(CompletionRateText));
        OnPropertyChanged(nameof(TotalCompletedText));
    }

    /// <summary>Ghi nhận thao tác đánh dấu trên lưới chi tiết.</summary>
    public void RequestMark(DateOnly day) => MarkRequested?.Invoke(this, day);

    /// <summary>
    /// Dựng 12 cột biểu đồ cho năm hiện tại: số ngày hoàn thành trên tổng số ngày có lịch của từng tháng.
    /// </summary>
    private IReadOnlyList<MonthBar> BuildMonthBars(IReadOnlyList<Entry> entries)
    {
        var completedByDay = entries.ToDictionary(e => e.Day, e => e.Value);
        var year = Today.Year;
        var bars = new List<MonthBar>(12);

        for (var month = 1; month <= 12; month++)
        {
            var daysInMonth = DateTime.DaysInMonth(year, month);
            var scheduled = 0;
            var completed = 0;

            for (var day = 1; day <= daysInMonth; day++)
            {
                var date = new DateOnly(year, month, day);

                if (date > Today || date < Habit.StartDate)
                {
                    continue;
                }

                if (!_scheduleCalculator.IsScheduled(Habit, date))
                {
                    continue;
                }

                scheduled++;

                if (completedByDay.TryGetValue(date, out var value))
                {
                    var entry = new Entry { HabitId = Habit.Id, Day = date, Value = value };

                    if (entry.IsCompleted(Habit))
                    {
                        completed++;
                    }
                }
            }

            bars.Add(new MonthBar(month, year, completed, scheduled));
        }

        return bars;
    }
}
