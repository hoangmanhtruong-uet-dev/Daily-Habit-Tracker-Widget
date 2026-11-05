using HabitGrid.Core.Domain;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;

namespace HabitGrid.WinUI.Controls;

/// <summary>
/// Heatmap kiểu GitHub vẽ bằng hình XAML (03-UI-REBUILD-GLOW2 §5.3): cột = tuần, hàng = thứ.
/// Độ đậm theo màu thói quen; ô bỏ qua/đóng băng có màu riêng; ô tương lai mờ; hôm nay có viền.
/// Chế độ tương tác (trang chi tiết) có nhãn tháng/thứ, tooltip và sự kiện chạm vào ô.
/// </summary>
public sealed class HeatmapGrid : UserControl
{
    public static readonly DependencyProperty DataProperty =
        DependencyProperty.Register(nameof(Data), typeof(HeatmapData), typeof(HeatmapGrid), new PropertyMetadata(null, OnChanged));

    public static readonly DependencyProperty ColorHueProperty =
        DependencyProperty.Register(nameof(ColorHue), typeof(string), typeof(HeatmapGrid), new PropertyMetadata("#8B5CF6", OnChanged));

    public static readonly DependencyProperty CellSizeProperty =
        DependencyProperty.Register(nameof(CellSize), typeof(double), typeof(HeatmapGrid), new PropertyMetadata(11.0, OnChanged));

    public static readonly DependencyProperty IsInteractiveProperty =
        DependencyProperty.Register(nameof(IsInteractive), typeof(bool), typeof(HeatmapGrid), new PropertyMetadata(false, OnChanged));

    private const double Gap = 3;
    private const double LabelWidth = 30;
    private const double MonthRowHeight = 18;

    private readonly Canvas _canvas = new();

    public HeatmapGrid()
    {
        IsTabStop = false;
        Content = _canvas;
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;
        Rebuild();
    }

    /// <summary>Người dùng chạm vào một ô (chỉ khi <see cref="IsInteractive"/>). Tham số thứ hai là ô được chạm để gắn flyout.</summary>
    public event EventHandler<HeatCellTappedEventArgs>? CellTapped;

    public HeatmapData? Data
    {
        get => (HeatmapData?)GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public string ColorHue
    {
        get => (string)GetValue(ColorHueProperty);
        set => SetValue(ColorHueProperty, value);
    }

    public double CellSize
    {
        get => (double)GetValue(CellSizeProperty);
        set => SetValue(CellSizeProperty, value);
    }

    public bool IsInteractive
    {
        get => (bool)GetValue(IsInteractiveProperty);
        set => SetValue(IsInteractiveProperty, value);
    }

    /// <summary>Chiều rộng cần thiết (để trang chọn cỡ ô vừa với khung).</summary>
    public static double WidthFor(int weeks, double cellSize, bool interactive) =>
        (interactive ? LabelWidth : 0) + weeks * (cellSize + Gap) - Gap;

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((HeatmapGrid)d).Rebuild();

    private void Rebuild()
    {
        _canvas.Children.Clear();

        if (Data is not { } data || data.Weeks.Count == 0)
        {
            _canvas.Width = 0;
            _canvas.Height = 0;
            return;
        }

        var size = Math.Max(4, CellSize);
        var step = size + Gap;
        var left = IsInteractive ? LabelWidth : 0;
        var top = IsInteractive ? MonthRowHeight : 0;
        var color = Ui.ParseColor(ColorHue, Microsoft.UI.Colors.Gray);

        _canvas.Width = WidthFor(data.Weeks.Count, size, IsInteractive);
        _canvas.Height = top + 7 * step - Gap;

        if (IsInteractive)
        {
            AddLabels(data, left, step);
        }

        var radius = Math.Max(2, size * 0.28);

        for (var w = 0; w < data.Weeks.Count; w++)
        {
            for (var row = 0; row < 7; row++)
            {
                var cell = data.Weeks[w][row];

                if (cell.IsBeforeStart && !IsInteractive)
                {
                    continue; // thẻ nhỏ: không vẽ ô trước ngày bắt đầu cho gọn
                }

                var rect = new Rectangle
                {
                    Width = size,
                    Height = size,
                    RadiusX = radius,
                    RadiusY = radius,
                    Fill = FillFor(cell, color),
                    Stroke = cell.IsToday ? (Brush)Application.Current.Resources["TextPrimaryBrush"] : null,
                    StrokeThickness = cell.IsToday ? 1.5 : 0
                };

                Canvas.SetLeft(rect, left + w * step);
                Canvas.SetTop(rect, top + row * step);

                if (IsInteractive && !cell.IsFuture && !cell.IsBeforeStart)
                {
                    var captured = cell;
                    var text = Describe(cell);
                    ToolTipService.SetToolTip(rect, text);
                    AutomationProperties.SetName(rect, text);
                    rect.Tapped += (_, args) =>
                    {
                        args.Handled = true;
                        CellTapped?.Invoke(this, new HeatCellTappedEventArgs(captured, rect));
                    };
                }

                _canvas.Children.Add(rect);
            }
        }
    }

    private void AddLabels(HeatmapData data, double left, double step)
    {
        var muted = (Brush)Application.Current.Resources["TextMutedBrush"];
        var culture = Ui.CurrentCulture;

        foreach (var (column, month, _) in HeatmapBuilder.MonthLabels(data))
        {
            // Bỏ nhãn quá sát mép phải (không đủ chỗ).
            if (data.Weeks.Count - column < 2)
            {
                continue;
            }

            var label = new TextBlock { Text = culture.DateTimeFormat.GetAbbreviatedMonthName(month), FontSize = 11, Foreground = muted };
            Canvas.SetLeft(label, left + column * step);
            Canvas.SetTop(label, 0);
            _canvas.Children.Add(label);
        }

        // Nhãn thứ cho hàng 1, 3, 5 (thứ Hai/Tư/Sáu khi tuần bắt đầu thứ Hai).
        foreach (var row in new[] { 0, 2, 4 })
        {
            var day = data.Weeks[0][row].Day.DayOfWeek;
            var label = new TextBlock { Text = culture.DateTimeFormat.GetAbbreviatedDayName(day), FontSize = 10, Foreground = muted };
            Canvas.SetLeft(label, 0);
            Canvas.SetTop(label, MonthRowHeight + row * step - 1);
            _canvas.Children.Add(label);
        }
    }

    private static Brush FillFor(HeatCell cell, Color color)
    {
        if (cell.IsBeforeStart)
        {
            return new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        }

        if (cell.IsFuture)
        {
            return new SolidColorBrush(Color.FromArgb(14, 255, 255, 255));
        }

        return cell.Status switch
        {
            DayStatus.Frozen => (Brush)Application.Current.Resources["FreezeBrush"],
            DayStatus.Skipped => new SolidColorBrush(Color.FromArgb(90, 150, 150, 165)),
            _ => cell.Level switch
            {
                4 => new SolidColorBrush(color),
                3 => new SolidColorBrush(Color.FromArgb(200, color.R, color.G, color.B)),
                2 => new SolidColorBrush(Color.FromArgb(140, color.R, color.G, color.B)),
                1 => new SolidColorBrush(Color.FromArgb(85, color.R, color.G, color.B)),
                _ => (Brush)Application.Current.Resources["HeatEmptyBrush"]
            }
        };
    }

    private static string Describe(HeatCell cell)
    {
        var date = Ui.LongDate(cell.Day);
        var state = cell.Status switch
        {
            DayStatus.Done => Ui.T("today.state.done"),
            DayStatus.Skipped => Ui.T("today.state.skipped"),
            _ when cell.Value > 0 => cell.Value.ToString(Ui.CurrentCulture),
            _ => string.Empty
        };

        return string.IsNullOrEmpty(state) ? date : $"{date} · {state}";
    }
}

public sealed class HeatCellTappedEventArgs(HeatCell cell, FrameworkElement anchor) : EventArgs
{
    public HeatCell Cell { get; } = cell;

    public FrameworkElement Anchor { get; } = anchor;
}
