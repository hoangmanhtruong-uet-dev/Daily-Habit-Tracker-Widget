using System.Globalization;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace HabitGrid.UI.Controls;

/// <summary>
/// Sự kiện khi người dùng tương tác với một ô của lưới.
/// </summary>
public sealed class HeatmapCellEventArgs : EventArgs
{
    public HeatmapCellEventArgs(GridCellInfo cell, MouseButton button)
    {
        Cell = cell;
        Button = button;
    }

    /// <summary>Ô được tương tác.</summary>
    public GridCellInfo Cell { get; }

    /// <summary>Nút chuột đã nhấn.</summary>
    public MouseButton Button { get; }
}

/// <summary>
/// Control lưới heatmap tự vẽ.
///
/// Toàn bộ ô vuông, nhãn tháng và nhãn thứ được vẽ trực tiếp bằng
/// <see cref="FrameworkElement.OnRender"/> thay vì sinh hàng trăm control WPF con.
/// Hit-test được suy luận bằng toán học (O(1)) nên không cần gắn event cho từng ô.
/// </summary>
public class HeatmapGridControl : FrameworkElement
{
    private const double CellCornerRadius = 3;
    private const double TodayBorderThickness = 1.5;

    private static readonly Typeface LabelTypeface = new(new FontFamily("Segoe UI Variable Text, Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

    private readonly Dictionary<int, FormattedText> _monthLabels = new();
    private readonly Dictionary<int, FormattedText> _dayLabels = new();
    private readonly Dictionary<string, SolidColorBrush> _brushCache = new();

    private GridCellInfo? _hoverCell;
    private GridCellInfo? _focusedCell;
    private DateOnly? _popDate;
    private bool _labelsDirty = true;

    #region Dependency Properties

    public static readonly DependencyProperty ModelProperty = DependencyProperty.Register(
        nameof(Model),
        typeof(HeatmapGridModel),
        typeof(HeatmapGridControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, OnModelChanged));

    public static readonly DependencyProperty AccentBrushProperty = DependencyProperty.Register(
        nameof(AccentBrush),
        typeof(Brush),
        typeof(HeatmapGridControl),
        new FrameworkPropertyMetadata(Brushes.DodgerBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ForegroundBrushProperty = DependencyProperty.Register(
        nameof(ForegroundBrush),
        typeof(Brush),
        typeof(HeatmapGridControl),
        new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LabelBrushProperty = DependencyProperty.Register(
        nameof(LabelBrush),
        typeof(Brush),
        typeof(HeatmapGridControl),
        new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Mô hình dữ liệu hiện tại của lưới.</summary>
    public HeatmapGridModel? Model
    {
        get => (HeatmapGridModel?)GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    /// <summary>Màu nhấn dùng cho viền ô hôm nay.</summary>
    public Brush AccentBrush
    {
        get => (Brush)GetValue(AccentBrushProperty);
        set => SetValue(AccentBrushProperty, value);
    }

    /// <summary>Màu chữ chính.</summary>
    public Brush ForegroundBrush
    {
        get => (Brush)GetValue(ForegroundBrushProperty);
        set => SetValue(ForegroundBrushProperty, value);
    }

    /// <summary>Màu chữ nhãn tháng/thứ.</summary>
    public Brush LabelBrush
    {
        get => (Brush)GetValue(LabelBrushProperty);
        set => SetValue(LabelBrushProperty, value);
    }

    /// <summary>Tiến trình hiệu ứng "pop" (0 → 1) của ô vừa được đánh dấu.</summary>
    private static readonly DependencyProperty PopProgressProperty = DependencyProperty.Register(
        "PopProgress",
        typeof(double),
        typeof(HeatmapGridControl),
        new PropertyMetadata(0.0, OnPopProgressChanged));

    private double PopProgress => (double)GetValue(PopProgressProperty);

    #endregion

    /// <summary>Được gọi khi người dùng bấm vào một ô.</summary>
    public event EventHandler<HeatmapCellEventArgs>? CellClicked;

    /// <summary>Được gọi khi người dùng bấm chuột phải vào một ô (mở mini popup cho thói quen đếm).</summary>
    public event EventHandler<HeatmapCellEventArgs>? CellContextRequested;

    /// <summary>Được gọi khi người dùng dùng bàn phím để di chuyển giữa các ô.</summary>
    public event EventHandler<GridCellInfo>? FocusedCellChanged;

    public HeatmapGridControl()
    {
        Focusable = true;
        FocusVisualStyle = null;
    }

    #region Kích thước

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Model is null)
        {
            return new Size(0, 0);
        }

        var layout = Model.Layout;

        return new Size(layout.TotalWidth, layout.TotalHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        return finalSize;
    }

    #endregion

    #region Render

    protected override void OnRender(DrawingContext dc)
    {
        var model = Model;

        if (model is null)
        {
            return;
        }

        var layout = model.Layout;
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        EnsureLabels(model, dpi);

        DrawMonthLabels(dc, layout);
        DrawDayLabels(dc, layout);
        DrawCells(dc, model);

        if (model.IsReadOnly)
        {
            DrawReadOnlyOverlay(dc, layout);
        }
    }

    private void DrawMonthLabels(DrawingContext dc, HeatmapLayout layout)
    {
        foreach (var label in layout.GetMonthLabels())
        {
            if (!_monthLabels.TryGetValue(label.ColumnIndex, out var text))
            {
                continue;
            }

            var x = layout.GridOriginX + (label.ColumnIndex * (layout.CellSize + layout.CellGap));
            dc.DrawText(text, new Point(x, 0));
        }
    }

    private void DrawDayLabels(DrawingContext dc, HeatmapLayout layout)
    {
        foreach (var (row, day) in layout.GetDayLabelRows())
        {
            if (!_dayLabels.TryGetValue(row, out var text))
            {
                continue;
            }

            var y = layout.GridOriginY + (row * (layout.CellSize + layout.CellGap));
            dc.DrawText(text, new Point(0, y + ((layout.CellSize - text.Height) / 2)));
        }
    }

    private void DrawCells(DrawingContext dc, HeatmapGridModel model)
    {
        var layout = model.Layout;
        var pitch = layout.CellSize + layout.CellGap;
        var highContrast = SystemParameters.HighContrast;

        for (var column = 0; column < layout.TotalWeeks; column++)
        {
            for (var row = 0; row < HeatmapLayout.DayRows; row++)
            {
                var date = layout.GetDate(column, row);
                var level = model.GetLevel(date);

                var left = layout.GridOriginX + (column * pitch);
                var top = layout.GridOriginY + (row * pitch);

                // Hiệu ứng "pop" khi vừa đánh dấu.
                if (_popDate == date && PopProgress > 0)
                {
                    var scale = 1.0 + (0.1 * Math.Sin(Math.PI * PopProgress));
                    var offset = layout.CellSize * (1 - scale) / 2;

                    left -= offset;
                    top -= offset;

                    var popRect = new Rect(left, top, layout.CellSize * scale, layout.CellSize * scale);
                    dc.DrawRoundedRectangle(GetBrush(model.GetShadeHex(level)), null, popRect, CellCornerRadius, CellCornerRadius);
                    continue;
                }

                var rect = new Rect(left, top, layout.CellSize, layout.CellSize);
                var isCompleted = level == HeatmapLevel.Complete;

                if (highContrast)
                {
                    // Chế độ tương phản cao: mọi ô đều có viền rõ, phân biệt trạng thái bằng
                    // cách tô chứ không chỉ dựa vào màu sắc.
                    var border = new Pen(SystemColors.WindowTextBrush, 1);

                    dc.DrawRoundedRectangle(
                        isCompleted ? SystemColors.HighlightBrush : SystemColors.WindowBrush,
                        border,
                        rect,
                        CellCornerRadius,
                        CellCornerRadius);

                    if (date == model.Today)
                    {
                        dc.DrawRoundedRectangle(null, new Pen(SystemColors.HighlightBrush, TodayBorderThickness), rect, CellCornerRadius, CellCornerRadius);
                    }

                    continue;
                }

                var brush = GetBrush(model.GetShadeHex(level));

                if (date > model.Today)
                {
                    // Ô tương lai mờ 30%.
                    brush = GetBrush(model.GetShadeHex(level), 0.3);
                }
                else if (_hoverCell is not null && _hoverCell.Date == date)
                {
                    brush = GetBrush(model.GetShadeHex(level), hoverOpacity: 1.15);
                }

                dc.DrawRoundedRectangle(brush, null, rect, CellCornerRadius, CellCornerRadius);

                if (date == model.Today)
                {
                    var border = new Pen(AccentBrush, TodayBorderThickness);
                    dc.DrawRoundedRectangle(null, border, rect, CellCornerRadius, CellCornerRadius);
                }

                if (_focusedCell is not null && _focusedCell.Date == date && IsKeyboardFocused)
                {
                    var focusPen = new Pen(ForegroundBrush, 1) { DashStyle = new DashStyle(new double[] { 2, 2 }, 0) };
                    dc.DrawRoundedRectangle(null, focusPen, rect, CellCornerRadius, CellCornerRadius);
                }
            }
        }
    }

    private void DrawReadOnlyOverlay(DrawingContext dc, HeatmapLayout layout)
    {
        var overlay = new Rect(0, 0, layout.TotalWidth, layout.TotalHeight);

        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(128, 128, 128, 128)), null, overlay);

        // Biểu tượng ổ khóa nhỏ ở góc phải.
        var lockText = new FormattedText(
            "🔒",
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI Emoji"),
            11,
            Brushes.White,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        dc.DrawText(lockText, new Point(layout.TotalWidth - lockText.Width - 2, layout.TotalHeight - lockText.Height - 2));
    }

    #endregion

    #region Trợ năng (Accessibility)

    /// <summary>Lớp tự động hóa cho trình đọc màn hình (Narrator).</summary>
    protected override AutomationPeer OnCreateAutomationPeer() => new HeatmapGridControlAutomationPeer(this);

    /// <summary>Lấy thông tin ô tại (cột, hàng) — dùng cho trợ năng.</summary>
    public GridCellInfo? GetCell(int column, int row)
    {
        var model = Model;

        if (model is null)
        {
            return null;
        }

        var layout = model.Layout;

        if (column < 0 || column >= layout.TotalWeeks || row < 0 || row >= HeatmapLayout.DayRows)
        {
            return null;
        }

        var date = layout.GetDate(column, row);

        return new GridCellInfo(
            column,
            row,
            date,
            layout.GetCellBounds(column, row),
            date == model.Today,
            date > model.Today,
            model.IsScheduled(date));
    }

    /// <summary>Ô đã hoàn thành hay chưa — dùng cho nhãn trợ năng.</summary>
    public bool IsCellCompleted(DateOnly day)
    {
        var model = Model;

        return model is not null && model.GetLevel(day) == HeatmapLevel.Complete;
    }

    /// <summary>Kích hoạt một ô từ trình đọc màn hình.</summary>
    internal void InvokeCell(GridCellInfo cell)
    {
        if (cell.IsFuture)
        {
            return;
        }

        CellClicked?.Invoke(this, new HeatmapCellEventArgs(cell, MouseButton.Left));
    }

    #endregion

    #region Tương tác chuột

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        var model = Model;

        if (model is null)
        {
            return;
        }

        var cell = model.Layout.HitTest(e.GetPosition(this), model.IsScheduled);

        if (cell is null)
        {
            if (_hoverCell is not null)
            {
                _hoverCell = null;
                ToolTip = null;
                InvalidateVisual();
            }

            return;
        }

        if (_hoverCell?.Date != cell.Date)
        {
            _hoverCell = cell;
            ToolTip = BuildTooltip(model, cell);
            InvalidateVisual();
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);

        if (_hoverCell is not null)
        {
            _hoverCell = null;
            ToolTip = null;
            InvalidateVisual();
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        var model = Model;

        if (model is null)
        {
            return;
        }

        var cell = model.Layout.HitTest(e.GetPosition(this), model.IsScheduled);

        if (cell is null || cell.IsFuture)
        {
            return;
        }

        Focus();

        _focusedCell = cell;
        FocusedCellChanged?.Invoke(this, cell);

        StartPop(cell.Date);
        CellClicked?.Invoke(this, new HeatmapCellEventArgs(cell, e.ChangedButton));
    }

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonDown(e);

        var model = Model;

        if (model is null)
        {
            return;
        }

        var cell = model.Layout.HitTest(e.GetPosition(this), model.IsScheduled);

        if (cell is null || cell.IsFuture)
        {
            return;
        }

        CellContextRequested?.Invoke(this, new HeatmapCellEventArgs(cell, e.ChangedButton));
        e.Handled = true;
    }

    #endregion

    #region Bàn phím

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        var model = Model;

        if (model is null)
        {
            return;
        }

        var layout = model.Layout;
        var current = _focusedCell;

        if (current is null)
        {
            // Vào lưới lần đầu: chọn ô hôm nay.
            var todayColumn = layout.TotalWeeks - 1;
            var todayRow = RowForDate(layout, model.Today);

            _focusedCell = layout.HitTest(CellCenter(layout, todayColumn, todayRow), model.IsScheduled);
            FocusedCellChanged?.Invoke(this, _focusedCell!);
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        var column = current.WeekColumnIndex;
        var row = current.DayRowIndex;

        switch (e.Key)
        {
            case Key.Left:
                column--;
                break;
            case Key.Right:
                column++;
                break;
            case Key.Up:
                row--;
                break;
            case Key.Down:
                row++;
                break;
            case Key.Space:
            case Key.Enter:
                if (!current.IsFuture)
                {
                    StartPop(current.Date);
                    CellClicked?.Invoke(this, new HeatmapCellEventArgs(current, MouseButton.Left));
                }

                e.Handled = true;
                return;
            default:
                return;
        }

        if (column < 0 || column >= layout.TotalWeeks || row < 0 || row >= HeatmapLayout.DayRows)
        {
            e.Handled = true;
            return;
        }

        var target = layout.HitTest(CellCenter(layout, column, row), model.IsScheduled);

        if (target is not null)
        {
            _focusedCell = target;
            FocusedCellChanged?.Invoke(this, target);
            InvalidateVisual();
        }

        e.Handled = true;
    }

    private static int RowForDate(HeatmapLayout layout, DateOnly date)
    {
        for (var row = 0; row < HeatmapLayout.DayRows; row++)
        {
            if (layout.GetDate(layout.TotalWeeks - 1, row) == date)
            {
                return row;
            }
        }

        return 0;
    }

    private static Point CellCenter(HeatmapLayout layout, int column, int row)
    {
        var bounds = layout.GetCellBounds(column, row);
        return new Point(bounds.X + (bounds.Width / 2), bounds.Y + (bounds.Height / 2));
    }

    #endregion

    #region Hiệu ứng & nhãn

    private void StartPop(DateOnly date)
    {
        _popDate = date;

        var animation = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        animation.Completed += (_, _) =>
        {
            _popDate = null;
            InvalidateVisual();
        };

        BeginAnimation(PopProgressProperty, animation);
    }

    private static void OnPopProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((HeatmapGridControl)d).InvalidateVisual();
    }

    private static void OnModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (HeatmapGridControl)d;

        control._labelsDirty = true;
        control._hoverCell = null;
        control._focusedCell = null;
        control.InvalidateMeasure();
        control.InvalidateVisual();
    }

    private void EnsureLabels(HeatmapGridModel model, double pixelsPerDip)
    {
        if (!_labelsDirty)
        {
            return;
        }

        _labelsDirty = false;
        _monthLabels.Clear();
        _dayLabels.Clear();

        var layout = model.Layout;
        var culture = CultureInfo.CurrentUICulture;

        foreach (var label in layout.GetMonthLabels())
        {
            var text = culture.DateTimeFormat.GetAbbreviatedMonthName(label.Month);
            _monthLabels[label.ColumnIndex] = new FormattedText(
                text,
                culture,
                FlowDirection.LeftToRight,
                LabelTypeface,
                9.5,
                LabelBrush,
                pixelsPerDip);
        }

        foreach (var (row, day) in layout.GetDayLabelRows())
        {
            var text = GetShortDayName(day, culture);
            _dayLabels[row] = new FormattedText(
                text,
                culture,
                FlowDirection.LeftToRight,
                LabelTypeface,
                9.5,
                LabelBrush,
                pixelsPerDip);
        }
    }

    private static string GetShortDayName(DayOfWeek day, CultureInfo culture)
    {
        var isVietnamese = string.Equals(culture.TwoLetterISOLanguageName, "vi", StringComparison.OrdinalIgnoreCase);

        return day switch
        {
            DayOfWeek.Monday => isVietnamese ? "T2" : "Mon",
            DayOfWeek.Tuesday => isVietnamese ? "T3" : "Tue",
            DayOfWeek.Wednesday => isVietnamese ? "T4" : "Wed",
            DayOfWeek.Thursday => isVietnamese ? "T5" : "Thu",
            DayOfWeek.Friday => isVietnamese ? "T6" : "Fri",
            DayOfWeek.Saturday => isVietnamese ? "T7" : "Sat",
            DayOfWeek.Sunday => isVietnamese ? "CN" : "Sun",
            _ => string.Empty
        };
    }

    private SolidColorBrush GetBrush(string hex, double opacity = 1.0, double hoverOpacity = 1.0)
    {
        var key = $"{hex}|{opacity:F2}|{hoverOpacity:F2}";

        if (_brushCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var color = (Color)ColorConverter.ConvertFromString(hex);

        if (hoverOpacity != 1.0)
        {
            // Hover: làm sáng nhẹ ô.
            color = Color.FromRgb(
                (byte)Math.Min(255, color.R + 28),
                (byte)Math.Min(255, color.G + 28),
                (byte)Math.Min(255, color.B + 28));
        }

        var brush = new SolidColorBrush(color) { Opacity = opacity };
        brush.Freeze();

        _brushCache[key] = brush;

        return brush;
    }

    private static string BuildTooltip(HeatmapGridModel model, GridCellInfo cell)
    {
        if (model.IsReadOnly)
        {
            return "Mở khóa Pro để tiếp tục ghi nhận thói quen này";
        }

        return model.GetTooltipText(cell.Date);
    }

    #endregion
}
