using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace HabitGrid.UI.Controls;

/// <summary>
/// Một cột trong biểu đồ 12 tháng.
/// </summary>
/// <param name="Month">Tháng (1–12).</param>
/// <param name="Year">Năm.</param>
/// <param name="CompletedDays">Số ngày có lịch đã hoàn thành trong tháng.</param>
/// <param name="ScheduledDays">Tổng số ngày có lịch trong tháng.</param>
public sealed record MonthBar(int Month, int Year, int CompletedDays, int ScheduledDays)
{
    /// <summary>Tỷ lệ hoàn thành (0.0 – 1.0).</summary>
    public double Ratio => ScheduledDays <= 0 ? 0 : (double)CompletedDays / ScheduledDays;

    /// <summary>Nhãn tháng, ví dụ "Thg 10".</summary>
    public string Label => $"Thg {Month}";

    /// <summary>Chuỗi tooltip mô tả chi tiết tháng.</summary>
    public string Tooltip => $"Tháng {Month}/{Year}: {CompletedDays}/{ScheduledDays} ngày ({Ratio * 100:F1}%)";
}

/// <summary>
/// Biểu đồ cột nhỏ 12 tháng, tự vẽ bằng <see cref="FrameworkElement.OnRender"/>.
/// </summary>
public sealed class MonthBarChart : FrameworkElement
{
    private const double BottomLabelHeight = 18;
    private const double BarGap = 6;

    private MonthBar? _hoverBar;

    public MonthBarChart()
    {
        ToolTip = null;
    }

    /// <summary>Danh sách cột hiển thị.</summary>
    public IReadOnlyList<MonthBar> Bars
    {
        get => (IReadOnlyList<MonthBar>)GetValue(BarsProperty);
        set => SetValue(BarsProperty, value);
    }

    public static readonly DependencyProperty BarsProperty = DependencyProperty.Register(
        nameof(Bars),
        typeof(IReadOnlyList<MonthBar>),
        typeof(MonthBarChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Màu cột.</summary>
    public Brush BarBrush
    {
        get => (Brush)GetValue(BarBrushProperty);
        set => SetValue(BarBrushProperty, value);
    }

    public static readonly DependencyProperty BarBrushProperty = DependencyProperty.Register(
        nameof(BarBrush),
        typeof(Brush),
        typeof(MonthBarChart),
        new FrameworkPropertyMetadata(Brushes.SteelBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Màu nhãn tháng.</summary>
    public Brush LabelBrush
    {
        get => (Brush)GetValue(LabelBrushProperty);
        set => SetValue(LabelBrushProperty, value);
    }

    public static readonly DependencyProperty LabelBrushProperty = DependencyProperty.Register(
        nameof(LabelBrush),
        typeof(Brush),
        typeof(MonthBarChart),
        new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    protected override void OnRender(DrawingContext dc)
    {
        var bars = Bars;

        if (bars is null || bars.Count == 0)
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var chartHeight = Math.Max(0, ActualHeight - BottomLabelHeight);
        var slotWidth = ActualWidth / bars.Count;
        var barWidth = Math.Max(4, slotWidth - BarGap);

        for (var index = 0; index < bars.Count; index++)
        {
            var bar = bars[index];
            var left = (index * slotWidth) + (BarGap / 2);
            var height = chartHeight * bar.Ratio;
            var top = chartHeight - height;

            var rect = new Rect(left, top, barWidth, Math.Max(2, height));
            var brush = _hoverBar == bar ? Lighten(BarBrush) : BarBrush;

            dc.DrawRoundedRectangle(brush, null, rect, 3, 3);

            var label = new FormattedText(
                bar.Label,
                System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                9.5,
                LabelBrush,
                dpi);

            dc.DrawText(label, new Point(left + ((barWidth - label.Width) / 2), chartHeight + 2));
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        var bars = Bars;

        if (bars is null || bars.Count == 0)
        {
            return;
        }

        var slotWidth = ActualWidth / bars.Count;
        var index = (int)(e.GetPosition(this).X / slotWidth);

        if (index < 0 || index >= bars.Count)
        {
            index = -1;
        }

        var hovered = index >= 0 ? bars[index] : null;

        if (!ReferenceEquals(hovered, _hoverBar))
        {
            _hoverBar = hovered;
            ToolTip = hovered?.Tooltip;
            InvalidateVisual();
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);

        if (_hoverBar is not null)
        {
            _hoverBar = null;
            ToolTip = null;
            InvalidateVisual();
        }
    }

    private static Brush Lighten(Brush brush)
    {
        if (brush is SolidColorBrush solid)
        {
            var color = solid.Color;
            var lighter = Color.FromRgb(
                (byte)Math.Min(255, color.R + 30),
                (byte)Math.Min(255, color.G + 30),
                (byte)Math.Min(255, color.B + 30));

            var result = new SolidColorBrush(lighter);
            result.Freeze();
            return result;
        }

        return brush;
    }
}
