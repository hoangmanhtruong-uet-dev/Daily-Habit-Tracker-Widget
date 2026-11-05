using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using HabitGrid.Presentation.Services;

namespace HabitGrid.WidgetHost;

/// <summary>
/// Cửa sổ Quick check-in (SPEC-V2-05 §4): hiện giữa màn hình đang có con trỏ, gõ để lọc không dấu,
/// Enter check, +/− tăng giảm, ↑/↓ chọn, Esc hoặc mất focus thì đóng. Tạo sẵn và ẩn để mở nhanh.
/// </summary>
public sealed class QuickCheckInWindow : Window
{
    private readonly QuickCheckInModel _model;
    private readonly Func<DateOnly, Task> _reload;
    private readonly Func<QuickItem, int, Task> _act;
    private readonly TextBox _filter = new();
    private readonly TextBlock _placeholder = new() { IsHitTestVisible = false };
    private readonly StackPanel _list = new();
    private readonly TextBlock _hint = new() { FontSize = 11 };
    private readonly Border _root = new() { CornerRadius = new CornerRadius(20), Padding = new Thickness(14) };
    private IReadOnlyList<QuickItem> _shown = [];
    private int _selected;
    private bool _dark = true;

    public QuickCheckInWindow(QuickCheckInModel model, Func<DateOnly, Task> reload, Func<QuickItem, int, Task> act)
    {
        _model = model;
        _reload = reload;
        _act = act;

        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.Height;
        Width = 420;
        MaxHeight = 460;
        Title = "HabitGrid Quick check-in";
        ShowActivated = true;

        _filter.FontSize = 16;
        _filter.BorderThickness = new Thickness(0);
        _filter.Background = Brushes.Transparent;
        _filter.Padding = new Thickness(4, 6, 4, 6);
        _filter.TextChanged += (_, _) =>
        {
            _selected = 0;
            Render();
        };

        var inputHost = new Grid();
        inputHost.Children.Add(_filter);
        inputHost.Children.Add(_placeholder);
        _placeholder.Margin = new Thickness(8, 0, 0, 0);
        _placeholder.VerticalAlignment = VerticalAlignment.Center;
        _placeholder.FontSize = 16;

        var stack = new StackPanel();
        stack.Children.Add(inputHost);
        stack.Children.Add(new Border { Height = 1, Margin = new Thickness(0, 6, 0, 6), Background = new SolidColorBrush(Color.FromArgb(40, 128, 128, 128)) });
        stack.Children.Add(new ScrollViewer { Content = _list, MaxHeight = 330, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        _hint.Margin = new Thickness(4, 8, 0, 0);
        stack.Children.Add(_hint);

        _root.Child = stack;
        Content = _root;

        PreviewKeyDown += OnPreviewKeyDown;
        Deactivated += (_, _) =>
        {
            if (IsVisible)
            {
                Hide();
            }
        };
    }

    /// <summary>Tạo sẵn cửa sổ (handle, bố cục) rồi ẩn để lần mở đầu tiên nhanh.</summary>
    public void Prewarm()
    {
        var wasTopmost = Topmost;
        Opacity = 0;
        Show();
        Hide();
        Opacity = 1;
        Topmost = wasTopmost;
    }

    public void ApplyTheme(bool dark) => _dark = dark;

    /// <summary>Nạp dữ liệu mới nhất rồi hiện cửa sổ giữa màn hình có con trỏ và đặt focus vào ô lọc.</summary>
    public async Task PresentAsync(DateOnly today)
    {
        await _reload(today);
        _filter.Text = string.Empty;
        _selected = 0;
        Render();

        Show();
        PositionOnCursorScreen();
        Activate();
        ForceForeground();
        _filter.Focus();
        Keyboard.Focus(_filter);
    }

    /// <summary>Làm mới danh sách khi dữ liệu đổi lúc cửa sổ đang mở.</summary>
    public void Refresh()
    {
        if (IsVisible)
        {
            Render();
        }
    }

    /// <summary>Dùng cho tự kiểm tra: gán chuỗi lọc và trả về các dòng đang hiển thị.</summary>
    public IReadOnlyList<QuickItem> SetFilterForTest(string text)
    {
        _filter.Text = text;
        Render();
        return _shown;
    }

    public Task EnterForTestAsync() => ActOnSelectedAsync(0);

    private void Render()
    {
        var fore = _dark ? Brushes.White : new SolidColorBrush(Color.FromRgb(30, 30, 40));
        var sub = new SolidColorBrush(_dark ? Color.FromArgb(160, 255, 255, 255) : Color.FromArgb(150, 30, 30, 40));
        _root.Background = new SolidColorBrush(_dark ? Color.FromArgb(246, 26, 26, 36) : Color.FromArgb(248, 250, 250, 252));
        _root.BorderBrush = new SolidColorBrush(_dark ? Color.FromArgb(50, 255, 255, 255) : Color.FromArgb(40, 0, 0, 0));
        _root.BorderThickness = new Thickness(1);
        _filter.Foreground = fore;
        _filter.CaretBrush = fore;
        _placeholder.Text = HostText.QuickPlaceholder;
        _placeholder.Foreground = sub;
        _placeholder.Visibility = string.IsNullOrEmpty(_filter.Text) ? Visibility.Visible : Visibility.Collapsed;
        _hint.Text = HostText.QuickHint;
        _hint.Foreground = sub;

        _shown = _model.Filter(_filter.Text);
        _selected = Math.Clamp(_selected, 0, Math.Max(0, _shown.Count - 1));
        _list.Children.Clear();

        if (_shown.Count == 0)
        {
            _list.Children.Add(new TextBlock
            {
                Text = _model.Items.Count == 0 ? HostText.NothingDue : HostText.NoMatch,
                Foreground = sub,
                Margin = new Thickness(6, 10, 6, 10)
            });
            return;
        }

        for (var i = 0; i < _shown.Count; i++)
        {
            _list.Children.Add(BuildRow(_shown[i], i == _selected, fore, sub, i));
        }
    }

    private UIElement BuildRow(QuickItem item, bool selected, Brush fore, Brush sub, int index)
    {
        var accent = (Color)ColorConverter.ConvertFromString(item.Habit.ColorHue);
        var dot = new System.Windows.Shapes.Ellipse
        {
            Width = 20,
            Height = 20,
            StrokeThickness = 2,
            Stroke = new SolidColorBrush(accent),
            Fill = item.IsDone ? new SolidColorBrush(accent) : Brushes.Transparent
        };

        var name = new TextBlock
        {
            Text = item.Name,
            FontSize = 15,
            Foreground = item.IsDone || item.IsSkipped ? sub : fore,
            TextDecorations = item.IsDone ? TextDecorations.Strikethrough : null,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 8, 0),
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        var row = new Grid { Margin = new Thickness(0, 1, 0, 1) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(dot);
        Grid.SetColumn(name, 1);
        row.Children.Add(name);

        if (item.IsNumeric)
        {
            var count = new TextBlock { Text = $"{item.Value}/{item.Target}", Foreground = sub, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(count, 2);
            row.Children.Add(count);
        }

        var border = new Border
        {
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8, 7, 8, 7),
            Child = row,
            Cursor = Cursors.Hand,
            Background = selected
                ? new SolidColorBrush(Color.FromArgb(_dark ? (byte)45 : (byte)30, accent.R, accent.G, accent.B))
                : Brushes.Transparent
        };

        border.MouseLeftButtonUp += async (_, _) =>
        {
            _selected = index;
            await ActOnSelectedAsync(0);
        };
        return border;
    }

    private async void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        try
        {
            switch (e.Key)
            {
                case Key.Escape:
                    e.Handled = true;
                    Hide();
                    break;
                case Key.Down:
                    e.Handled = true;
                    _selected = Math.Min(_selected + 1, Math.Max(0, _shown.Count - 1));
                    Render();
                    break;
                case Key.Up:
                    e.Handled = true;
                    _selected = Math.Max(_selected - 1, 0);
                    Render();
                    break;
                case Key.Enter:
                    e.Handled = true;
                    await ActOnSelectedAsync(0);
                    break;
                case Key.Add or Key.OemPlus:
                    e.Handled = true;
                    await ActOnSelectedAsync(+1, adjust: true);
                    break;
                case Key.Subtract or Key.OemMinus:
                    e.Handled = true;
                    await ActOnSelectedAsync(-1, adjust: true);
                    break;
            }
        }
        catch (Exception)
        {
            // Lỗi ghi dữ liệu không được làm sập tiến trình nền; danh sách sẽ nạp lại ở lần mở sau.
        }
    }

    private async Task ActOnSelectedAsync(int delta, bool adjust = false)
    {
        if (_shown.Count == 0)
        {
            return;
        }

        var item = _shown[Math.Clamp(_selected, 0, _shown.Count - 1)];

        // Enter trên thói quen đếm cũng là +1; +/- chỉ chỉnh bước, Enter có/không là bật-tắt.
        await _act(item, adjust ? delta : 0);
        _filter.Text = string.Empty;
        Render();
    }

    private void PositionOnCursorScreen()
    {
        if (!GetCursorPos(out var cursor))
        {
            return;
        }

        var monitor = MonitorFromPoint(cursor, 2);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };

        if (!GetMonitorInfo(monitor, ref info))
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        var widthPx = (int)Math.Round(ActualWidth * dpi.DpiScaleX);
        var heightPx = (int)Math.Round(ActualHeight * dpi.DpiScaleY);
        var work = info.Work;
        var x = work.Left + ((work.Right - work.Left) - widthPx) / 2;
        var y = work.Top + (int)(((work.Bottom - work.Top) - heightPx) * 0.32);
        SetWindowPos(new WindowInteropHelper(this).EnsureHandle(), new IntPtr(-1), x, y, 0, 0, 0x0001 | 0x0010);
    }

    private void ForceForeground()
    {
        var handle = new WindowInteropHelper(this).Handle;
        SetForegroundWindow(handle);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public Rect Monitor;
        public Rect Work;
        public uint Flags;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(Point point, uint flags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
