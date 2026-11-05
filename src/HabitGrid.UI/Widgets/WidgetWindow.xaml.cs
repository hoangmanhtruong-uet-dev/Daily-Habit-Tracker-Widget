using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using HabitGrid.Core.Settings;
using HabitGrid.Platform.Desktop;
using HabitGrid.UI.Controls;
using HabitGrid.UI.Theme;

namespace HabitGrid.UI.Widgets;

/// <summary>
/// Thông tin vị trí mới của widget sau khi người dùng kéo.
/// </summary>
public sealed class WidgetPositionChangedEventArgs : EventArgs
{
    public WidgetPositionChangedEventArgs(int left, int top, int width, int height)
    {
        Left = left;
        Top = top;
        Width = width;
        Height = height;
    }

    /// <summary>Toạ độ X trên màn hình (pixel vật lý).</summary>
    public int Left { get; }

    /// <summary>Toạ độ Y trên màn hình (pixel vật lý).</summary>
    public int Top { get; }

    public int Width { get; }

    public int Height { get; }
}

/// <summary>
/// Cửa sổ widget hiển thị trên desktop.
/// - Trong suốt, không viền, không hiện trên taskbar.
/// - Kéo được bằng vùng header, tự bám lưới 8px.
/// - Hỗ trợ 3 chế độ ghim thông qua <see cref="DesktopHostService"/>.
/// </summary>
public partial class WidgetWindow : Window
{
    private readonly IScreenProvider _screenProvider;

    private DesktopHostService? _desktopHost;
    private bool _isDragging;
    private int _dragStartCursorX;
    private int _dragStartCursorY;
    private int _dragStartLeft;
    private int _dragStartTop;

    public WidgetWindow() : this(new WindowsScreenProvider())
    {
    }

    public WidgetWindow(IScreenProvider screenProvider)
    {
        ArgumentNullException.ThrowIfNull(screenProvider);

        _screenProvider = screenProvider;

        InitializeComponent();

        GridControl.CellClicked += OnCellClicked;
        GridControl.CellContextRequested += OnCellContextRequested;
    }

    /// <summary>Áp dụng ViewModel cho cửa sổ.</summary>
    public void Bind(WidgetViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        ViewModel = viewModel;

        var spec = WidgetSizeSpec.Get(viewModel.Layout.Size);
        Width = spec.WindowWidth;
        Height = spec.WindowHeight;

        HabitNameText.Text = viewModel.DisplayName;
        StreakText.Text = viewModel.StreakText;
        IconText.Text = GetIconGlyph(viewModel.Habit.Icon);

        GridControl.Model = viewModel.GridModel;

        ApplyTheme(viewModel.IsDarkTheme);
    }

    /// <summary>ViewModel đang hiển thị.</summary>
    public WidgetViewModel? ViewModel { get; private set; }

    /// <summary>Được gọi sau khi người dùng kéo widget đến vị trí mới.</summary>
    public event EventHandler<WidgetPositionChangedEventArgs>? PositionChanged;

    /// <summary>Gắn dịch vụ ghim desktop.</summary>
    public void UseDesktopHost(DesktopHostService desktopHost)
    {
        ArgumentNullException.ThrowIfNull(desktopHost);
        _desktopHost = desktopHost;
    }

    /// <summary>Áp dụng kiểu nền, độ mờ và bo góc cho widget theo cài đặt.</summary>
    public void ApplyWidgetStyle(WidgetBackgroundStyle style, double opacity, double cornerRadius)
    {
        _backgroundStyle = style;
        _backgroundOpacity = opacity;
        _cornerRadius = cornerRadius;

        var radius = Math.Clamp(cornerRadius, 4, 16);
        var alpha = (byte)Math.Clamp(opacity, 0, 100);

        RootBorder.CornerRadius = new CornerRadius(radius);

        switch (style)
        {
            case WidgetBackgroundStyle.None:
                RootBorder.Background = Brushes.Transparent;
                RootBorder.BorderThickness = new Thickness(0);
                break;

            case WidgetBackgroundStyle.Solid:
                RootBorder.Background = new SolidColorBrush(ApplyThemeColor(ThemeService.GetWidgetBackgroundColor(_isDarkTheme), 255));
                RootBorder.BorderThickness = new Thickness(1);
                break;

            default:
                // Acrylic: nền mờ trong suốt (mô phỏng hiệu ứng mica của Windows 11).
                RootBorder.Background = new SolidColorBrush(ApplyThemeColor(ThemeService.GetWidgetBackgroundColor(_isDarkTheme), alpha));
                RootBorder.BorderThickness = new Thickness(1);
                break;
        }
    }

    private static Color ApplyThemeColor(Color color, byte alpha) =>
        Color.FromArgb(alpha, color.R, color.G, color.B);

    /// <summary>Áp dụng màu sáng/tối cho widget.</summary>
    public void ApplyThemeColors(bool isDarkTheme)
    {
        _isDarkTheme = isDarkTheme;

        var foreground = isDarkTheme ? Brushes.White : new SolidColorBrush(Color.FromRgb(32, 32, 32));
        var label = isDarkTheme ? new SolidColorBrush(Color.FromRgb(200, 200, 200)) : new SolidColorBrush(Color.FromRgb(110, 110, 110));

        HabitNameText.Foreground = foreground;
        StreakText.Foreground = foreground;
        IconText.Foreground = foreground;
        GridControl.ForegroundBrush = foreground;
        GridControl.LabelBrush = label;

        ApplyWidgetStyle(_backgroundStyle, _backgroundOpacity, _cornerRadius);
    }

    private bool _isDarkTheme;
    private WidgetBackgroundStyle _backgroundStyle = WidgetBackgroundStyle.Acrylic;
    private double _backgroundOpacity = 85;
    private double _cornerRadius = 12;

    /// <summary>Đặt cửa sổ theo chế độ ghim và trả về kết quả.</summary>
    public DesktopHostResult ApplyPinning(DesktopPinningMode mode)
    {
        if (_desktopHost is null)
        {
            _desktopHost = new DesktopHostService(_screenProvider);
        }

        var handle = new WindowInteropHelper(this).Handle;
        var result = _desktopHost.Attach(handle, mode);

        Topmost = mode == DesktopPinningMode.AlwaysOnTop;

        return result;
    }

    /// <summary>Đưa cửa sổ về một vị trí tuyệt đối trên màn hình.</summary>
    public void SetScreenPosition(int left, int top)
    {
        var handle = new WindowInteropHelper(this).Handle;
        var bounds = NativeWindow.GetScreenBounds(handle);

        NativeWindow.MoveToScreenPosition(handle, left, top, bounds.Width, bounds.Height);
    }

    /// <summary>Bật/tắt chế độ chỉnh sửa (viền đứt, nút đóng, khoá tương tác ô).</summary>
    public void SetEditMode(bool isEnabled)
    {
        EditModeLayer.Visibility = isEnabled ? Visibility.Visible : Visibility.Collapsed;
        GridControl.IsHitTestVisible = !isEnabled;
        TodayButton.IsEnabled = !isEnabled;
        HeaderGrid.Cursor = isEnabled ? Cursors.Arrow : Cursors.SizeAll;
    }

    /// <summary>Áp dụng màu sáng/tối cho widget.</summary>
    public void ApplyTheme(bool isDarkTheme)
    {
        RootBorder.Background = isDarkTheme
            ? new SolidColorBrush(Color.FromRgb(32, 32, 32))
            : new SolidColorBrush(Color.FromRgb(245, 245, 245));

        var foreground = isDarkTheme ? Brushes.White : new SolidColorBrush(Color.FromRgb(32, 32, 32));
        var label = isDarkTheme ? new SolidColorBrush(Color.FromRgb(200, 200, 200)) : new SolidColorBrush(Color.FromRgb(110, 110, 110));

        HabitNameText.Foreground = foreground;
        StreakText.Foreground = foreground;
        IconText.Foreground = foreground;
        GridControl.ForegroundBrush = foreground;
        GridControl.LabelBrush = label;
    }

    private void OnHeaderMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        if (ViewModel?.IsEditMode == true)
        {
            return;
        }

        var handle = new WindowInteropHelper(this).Handle;
        var bounds = NativeWindow.GetScreenBounds(handle);
        var cursor = NativeWindow.GetCursorPosition();

        _dragStartCursorX = cursor.X;
        _dragStartCursorY = cursor.Y;
        _dragStartLeft = bounds.Left;
        _dragStartTop = bounds.Top;

        _isDragging = true;

        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (!_isDragging)
        {
            return;
        }

        var handle = new WindowInteropHelper(this).Handle;
        var bounds = NativeWindow.GetScreenBounds(handle);
        var cursor = NativeWindow.GetCursorPosition();

        var targetLeft = _dragStartLeft + (cursor.X - _dragStartCursorX);
        var targetTop = _dragStartTop + (cursor.Y - _dragStartCursorY);

        var screen = _screenProvider.GetScreenAt(targetLeft + (bounds.Width / 2), targetTop + (bounds.Height / 2))
                     ?? _screenProvider.GetPrimaryScreen();

        var (snappedLeft, snappedTop) = WidgetPlacement.Snap(targetLeft, targetTop, screen, bounds.Width, bounds.Height);

        NativeWindow.MoveToScreenPosition(handle, snappedLeft, snappedTop, bounds.Width, bounds.Height);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);

        if (!_isDragging)
        {
            return;
        }

        _isDragging = false;
        ReleaseMouseCapture();

        var handle = new WindowInteropHelper(this).Handle;
        var bounds = NativeWindow.GetScreenBounds(handle);

        PositionChanged?.Invoke(this, new WidgetPositionChangedEventArgs(bounds.Left, bounds.Top, bounds.Width, bounds.Height));
    }

    private void OnTodayButtonClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        ViewModel.RequestMark(ViewModel.Today);
    }

    private void OnCellClicked(object? sender, HeatmapCellEventArgs e)
    {
        if (ViewModel is null || ViewModel.IsEditMode)
        {
            return;
        }

        // Shift+click hoặc chuột phải dùng cho thói quen đếm số lượng.
        if (e.Button == MouseButton.Right || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            ViewModel.RequestContextMenu(e.Cell.Date);
            return;
        }

        ViewModel.RequestMark(e.Cell.Date);
    }

    private void OnCellContextRequested(object? sender, HeatmapCellEventArgs e)
    {
        ViewModel?.RequestContextMenu(e.Cell.Date);
    }

    private void OnCloseWidgetClick(object sender, RoutedEventArgs e)
    {
        ViewModel?.RequestClose();
    }

    /// <summary>
    /// Chuyển khóa icon Fluent sang ký tự trong font Segoe Fluent Icons / Segoe MDL2 Assets.
    /// Bộ icon đầy đủ sẽ được thay bằng WPF-UI ở SPEC-05.
    /// </summary>
    private static string GetIconGlyph(string iconKey)
    {
        return iconKey switch
        {
            "Book24Regular" => "\uE82A",
            "WaterDrop24Regular" => "\uE706",
            "Bicycle24Regular" => "\uE7F4",
            "Food24Regular" => "\uE719",
            "Heart24Regular" => "\uE734",
            "MusicNote2-24Regular" => "\uE95E",
            _ => "\uE7C3"
        };
    }
}
