using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using HabitGrid.Core.Models;
using HabitGrid.Core.Services;
using HabitGrid.UI.Controls;

namespace HabitGrid.UI.Widgets;

/// <summary>
/// ViewModel của cửa sổ widget: giữ trạng thái hiển thị và phát sự kiện ra ngoài.
/// Tầng UI chỉ giao tiếp qua các sự kiện này, không gọi trực tiếp repository.
/// </summary>
public partial class WidgetViewModel : ObservableObject
{
    private readonly Dictionary<DateOnly, int> _values;
    private readonly HeatmapShades _shades;
    private readonly IStreakCalculator _streakCalculator;

    [ObservableProperty]
    private int _currentStreak;

    [ObservableProperty]
    private bool _isEditMode;

    [ObservableProperty]
    private bool _isDarkTheme;

    public WidgetViewModel(
        Habit habit,
        IReadOnlyDictionary<DateOnly, int> values,
        HeatmapShades shades,
        DateOnly today,
        WidgetSize sizePreset,
        bool isDarkTheme = false,
        IStreakCalculator? streakCalculator = null,
        DayOfWeek weekStart = DayOfWeek.Monday)
    {
        ArgumentNullException.ThrowIfNull(habit);
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(shades);

        Habit = habit;
        _values = new Dictionary<DateOnly, int>(values);
        _shades = shades;
        _streakCalculator = streakCalculator ?? new StreakCalculator(new ScheduleCalculator());
        Today = today;
        IsDarkTheme = isDarkTheme;

        Layout = new HeatmapLayout(sizePreset, today, weekStart);
        GridModel = BuildGridModel();

        CurrentStreak = _streakCalculator.Calculate(Habit, BuildEntries(), today).CurrentStreak;
    }

    /// <summary>Thói quen được hiển thị.</summary>
    public Habit Habit { get; }

    /// <summary>Ngày logic hôm nay.</summary>
    public DateOnly Today { get; }

    /// <summary>Bố trí lưới hiện tại.</summary>
    public HeatmapLayout Layout { get; }

    /// <summary>Mô hình dữ liệu cho control lưới.</summary>
    public HeatmapGridModel GridModel { get; private set; }

    /// <summary>Tên hiển thị trên header.</summary>
    public string DisplayName => Habit.Name;

    /// <summary>Chuỗi chip streak, ví dụ "12 ngày".</summary>
    public string StreakText => CurrentStreak > 0 ? $"{CurrentStreak} ngày" : "Chưa có streak";

    /// <summary>True nếu thói quen bị khoá chỉ đọc (vượt hạn mức gói Free).</summary>
    public bool IsReadOnly => Habit.IsReadOnly;

    /// <summary>Bản sao chỉ đọc của dữ liệu hiện tại, dùng để truyền ra ngoài.</summary>
    public ReadOnlyDictionary<DateOnly, int> Values => new(_values);

    /// <summary>Được gọi khi người dùng yêu cầu đánh dấu một ngày.</summary>
    public event EventHandler<DateOnly>? MarkRequested;

    /// <summary>Được gọi khi người dùng yêu cầu mở mini popup (chuột phải / Shift+click).</summary>
    public event EventHandler<DateOnly>? ContextMenuRequested;

    /// <summary>Được gọi khi người dùng yêu cầu đóng (ẩn) widget.</summary>
    public event EventHandler? CloseRequested;

    partial void OnCurrentStreakChanged(int value) => OnPropertyChanged(nameof(StreakText));

    /// <summary>Yêu cầu đánh dấu một ngày.</summary>
    public void RequestMark(DateOnly day) => MarkRequested?.Invoke(this, day);

    /// <summary>Yêu cầu mở mini popup cho một ngày.</summary>
    public void RequestContextMenu(DateOnly day) => ContextMenuRequested?.Invoke(this, day);

    /// <summary>Yêu cầu ẩn widget.</summary>
    public void RequestClose() => CloseRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Cập nhật lại dữ liệu hiển thị (sau khi người dùng đánh dấu).</summary>
    public void UpdateValues(IReadOnlyDictionary<DateOnly, int> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        _values.Clear();

        foreach (var pair in values)
        {
            _values[pair.Key] = pair.Value;
        }

        GridModel = BuildGridModel();
        CurrentStreak = _streakCalculator.Calculate(Habit, BuildEntries(), Today).CurrentStreak;

        OnPropertyChanged(nameof(Values));
    }

    private HeatmapGridModel BuildGridModel() =>
        new(Habit, new Dictionary<DateOnly, int>(_values), _shades, Today, Layout, Habit.IsReadOnly);

    private List<Entry> BuildEntries()
    {
        var entries = new List<Entry>(_values.Count);

        foreach (var pair in _values)
        {
            entries.Add(new Entry
            {
                HabitId = Habit.Id,
                Day = pair.Key,
                Value = pair.Value
            });
        }

        return entries;
    }
}
