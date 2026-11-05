using HabitGrid.Core.Models;

namespace HabitGrid.UI.Controls;

/// <summary>
/// Thông số kích thước của từng cỡ widget theo đặc tả SPEC-03 mục 1.2.
/// </summary>
/// <param name="Size">Cỡ widget.</param>
/// <param name="TotalWeeks">Số tuần hiển thị.</param>
/// <param name="CellSize">Kích thước ô vuông (px).</param>
/// <param name="CellGap">Khoảng cách giữa các ô (px).</param>
/// <param name="WindowWidth">Chiều rộng cửa sổ widget xấp xỉ (px).</param>
/// <param name="WindowHeight">Chiều cao cửa sổ widget xấp xỉ (px).</param>
/// <param name="ShowDayLabels">Có hiển thị nhãn thứ (T2/T4/T6) hay không.</param>
public sealed record WidgetSizeSpec(
    WidgetSize Size,
    int TotalWeeks,
    double CellSize,
    double CellGap,
    double WindowWidth,
    double WindowHeight,
    bool ShowDayLabels)
{
    /// <summary>Chiều cao phần header của widget (px).</summary>
    public const double HeaderHeight = 44;

    /// <summary>Khoảng đệm trong của widget (px).</summary>
    public const double Padding = 10;

    /// <summary>Chiều rộng cột nhãn thứ (px).</summary>
    public const double DayLabelWidth = 26;

    /// <summary>Chiều cao hàng nhãn tháng (px).</summary>
    public const double MonthLabelHeight = 16;

    private static readonly WidgetSizeSpec SmallSpec = new(WidgetSize.Small, 13, 14, 3, 260, 200, false);
    private static readonly WidgetSizeSpec MediumSpec = new(WidgetSize.Medium, 26, 14, 3, 480, 220, true);
    private static readonly WidgetSizeSpec LargeSpec = new(WidgetSize.Large, 52, 12, 3, 820, 240, true);

    /// <summary>Lấy thông số của một cỡ widget.</summary>
    public static WidgetSizeSpec Get(WidgetSize size) => size switch
    {
        WidgetSize.Small => SmallSpec,
        WidgetSize.Large => LargeSpec,
        _ => MediumSpec
    };

    /// <summary>Tất cả các cỡ, theo thứ tự từ nhỏ đến lớn.</summary>
    public static IReadOnlyList<WidgetSizeSpec> All { get; } = new[] { SmallSpec, MediumSpec, LargeSpec };
}
