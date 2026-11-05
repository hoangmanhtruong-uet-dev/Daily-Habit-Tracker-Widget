using HabitGrid.Core.Models;
using HabitGrid.Core.Services;

namespace HabitGrid.UI.Controls;

/// <summary>
/// Trạng thái hiển thị của một ô trong lưới.
/// </summary>
public enum HeatmapLevel
{
    /// <summary>Ô rỗng (0).</summary>
    Empty = 0,

    /// <summary>Mức 1: dưới 25% mục tiêu.</summary>
    Low = 1,

    /// <summary>Mức 2: từ 25% đến dưới 50% mục tiêu.</summary>
    Medium = 2,

    /// <summary>Mức 3: từ 50% đến dưới 100% mục tiêu.</summary>
    High = 3,

    /// <summary>Mức 4: đạt hoặc vượt mục tiêu (với Boolean là đã đánh dấu).</summary>
    Complete = 4
}

/// <summary>
/// Mô hình dữ liệu cho lưới heatmap: ánh xạ ngày -&gt; giá trị và quy tắc tính mức màu.
/// Phần logic thuần nên kiểm thử được độc lập với WPF.
/// </summary>
public sealed class HeatmapGridModel
{
    private readonly IReadOnlyDictionary<DateOnly, int> _values;
    private readonly IScheduleCalculator _scheduleCalculator;

    public HeatmapGridModel(
        Habit habit,
        IReadOnlyDictionary<DateOnly, int> values,
        HeatmapShades shades,
        DateOnly today,
        HeatmapLayout layout,
        bool isReadOnly = false,
        IScheduleCalculator? scheduleCalculator = null)
    {
        ArgumentNullException.ThrowIfNull(habit);
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(shades);
        ArgumentNullException.ThrowIfNull(layout);

        Habit = habit;
        _values = values;
        Shades = shades;
        Today = today;
        Layout = layout;
        IsReadOnly = isReadOnly;
        _scheduleCalculator = scheduleCalculator ?? new ScheduleCalculator();
    }

    public Habit Habit { get; }

    public HeatmapShades Shades { get; }

    public DateOnly Today { get; }

    public HeatmapLayout Layout { get; }

    /// <summary>True nếu thói quen bị khoá chỉ đọc do vượt hạn mức gói Free.</summary>
    public bool IsReadOnly { get; }

    /// <summary>Giá trị ghi nhận của một ngày (0 nếu chưa có).</summary>
    public int GetValue(DateOnly day) => _values.TryGetValue(day, out var value) ? value : 0;

    /// <summary>Ngày có thuộc lịch lặp của thói quen hay không.</summary>
    public bool IsScheduled(DateOnly day) => _scheduleCalculator.IsScheduled(Habit, day);

    /// <summary>Ngày tương lai không được đánh dấu.</summary>
    public bool IsFuture(DateOnly day) => day > Today;

    /// <summary>
    /// Tính mức màu của ô theo đặc tả:
    /// Boolean: 0 = chưa làm, 4 = đã làm.
    /// Count: 0 = rỗng, 1 = (0%,25%), 2 = [25%,50%), 3 = [50%,100%), 4 = &gt;=100%.
    /// </summary>
    public HeatmapLevel GetLevel(DateOnly day)
    {
        var value = GetValue(day);

        if (value <= 0)
        {
            return HeatmapLevel.Empty;
        }

        if (Habit.Kind == HabitKind.Boolean)
        {
            return HeatmapLevel.Complete;
        }

        var target = Math.Max(1, Habit.Target);
        var ratio = value / (double)target;

        if (ratio >= 1.0)
        {
            return HeatmapLevel.Complete;
        }

        if (ratio >= 0.5)
        {
            return HeatmapLevel.High;
        }

        if (ratio >= 0.25)
        {
            return HeatmapLevel.Medium;
        }

        return HeatmapLevel.Low;
    }

    /// <summary>Màu hex tương ứng với mức của ô.</summary>
    public string GetShadeHex(DateOnly day) => GetShadeHex(GetLevel(day));

    /// <summary>Màu hex tương ứng với một mức màu.</summary>
    public string GetShadeHex(HeatmapLevel level) => level switch
    {
        HeatmapLevel.Empty => Shades.Level0Empty,
        HeatmapLevel.Low => Shades.Level1,
        HeatmapLevel.Medium => Shades.Level2,
        HeatmapLevel.High => Shades.Level3,
        HeatmapLevel.Complete => Shades.Level4,
        _ => Shades.Level0Empty
    };

    /// <summary>
    /// Chuỗi mô tả trạng thái ô dùng cho tooltip.
    /// </summary>
    public string GetTooltipText(DateOnly day)
    {
        var value = GetValue(day);

        if (Habit.Kind == HabitKind.Count)
        {
            var target = Math.Max(1, Habit.Target);
            var percent = (int)Math.Round(Math.Min(1.0, value / (double)target) * 100);

            return value >= target
                ? $"{day:dd/MM/yyyy} • {value}/{target} ({percent}%) • Đã đạt mục tiêu"
                : $"{day:dd/MM/yyyy} • {value}/{target} ({percent}%) • Nhấp để thêm";
        }

        return value >= 1
            ? $"{day:dd/MM/yyyy} • Đã xong"
            : $"{day:dd/MM/yyyy} • Chưa hoàn thành";
    }
}
