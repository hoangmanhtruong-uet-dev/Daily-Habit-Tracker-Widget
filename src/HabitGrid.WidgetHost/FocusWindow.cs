using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using HabitGrid.Core.Domain;
using HabitGrid.Core.Domain.Focus;

namespace HabitGrid.WidgetHost;

/// <summary>
/// Mini-player Focus nổi luôn trên cùng (SPEC-V2-09 §2): đồng hồ lớn, chế độ, thói quen gắn kèm, Bắt đầu/Tạm dừng/Dừng,
/// thu gọn thành viên nhỏ chỉ còn giờ. Kéo bất kỳ đâu để di chuyển. Đóng cửa sổ không dừng phiên.
/// </summary>
public sealed class FocusWindow : Window
{
    private readonly FocusController _controller;
    private readonly Func<Task<IReadOnlyList<Habit>>> _candidates;
    private readonly TextBlock _clock = new() { FontSize = 44, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly TextBlock _status = new() { FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly ComboBox _mode = new() { MinWidth = 120 };
    private readonly TextBox _minutes = new() { Width = 46, Margin = new Thickness(6, 0, 6, 0), TextAlignment = TextAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
    private readonly ComboBox _habit = new() { MinWidth = 200, Margin = new Thickness(0, 8, 0, 0) };
    private readonly Button _primary = new() { MinWidth = 96, Padding = new Thickness(12, 6, 12, 6) };
    private readonly Button _stop = new() { MinWidth = 70, Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(12, 6, 12, 6) };
    private readonly StackPanel _setup = new() { Margin = new Thickness(0, 10, 0, 0) };
    private readonly StackPanel _expanded = new();
    private readonly Border _root = new() { CornerRadius = new CornerRadius(20), Padding = new Thickness(16, 12, 16, 14) };
    private IReadOnlyList<Habit> _habits = [];
    private bool _compact;

    public FocusWindow(FocusController controller, Func<Task<IReadOnlyList<Habit>>> candidates)
    {
        _controller = controller;
        _candidates = candidates;

        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        Title = "HabitGrid Focus";

        _mode.Items.Add("Pomodoro 25/5");
        _mode.Items.Add(HostText.IsVietnamese ? "Đếm ngược" : "Countdown");
        _mode.Items.Add(HostText.IsVietnamese ? "Bấm giờ" : "Stopwatch");

        var modeRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        modeRow.Children.Add(_mode);
        modeRow.Children.Add(_minutes);
        modeRow.Children.Add(new TextBlock { Text = HostText.IsVietnamese ? "phút" : "min", VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.Gray });
        _setup.Children.Add(modeRow);
        _setup.Children.Add(_habit);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 10, 0, 0) };
        buttons.Children.Add(_primary);
        buttons.Children.Add(_stop);

        var header = new DockPanel { LastChildFill = false };
        var title = new TextBlock { Text = "⏱ " + HostText.FocusTitle, FontWeight = FontWeights.SemiBold };
        var collapse = new Button { Content = "—", Width = 28, Height = 24, Padding = new Thickness(0), ToolTip = HostText.IsVietnamese ? "Thu gọn" : "Collapse" };
        var close = new Button { Content = "✕", Width = 28, Height = 24, Padding = new Thickness(0), ToolTip = HostText.IsVietnamese ? "Ẩn (phiên vẫn chạy)" : "Hide (session keeps running)", Margin = new Thickness(4, 0, 0, 0) };
        DockPanel.SetDock(title, Dock.Left);
        DockPanel.SetDock(close, Dock.Right);
        DockPanel.SetDock(collapse, Dock.Right);
        header.Children.Add(title);
        header.Children.Add(close);
        header.Children.Add(collapse);

        _expanded.Children.Add(header);
        _expanded.Children.Add(_clock);
        _expanded.Children.Add(_status);
        _expanded.Children.Add(_setup);
        _expanded.Children.Add(buttons);

        _root.Child = _expanded;
        Content = _root;
        ApplyTheme(true);

        _mode.SelectionChanged += (_, _) => _minutes.IsEnabled = _mode.SelectedIndex != 2;
        _primary.Click += async (_, _) => await OnPrimaryAsync();
        _stop.Click += async (_, _) => await _controller.StopAsync();
        collapse.Click += (_, _) => SetCompact(true);
        close.Click += (_, _) => Hide();
        MouseLeftButtonDown += (_, e) =>
        {
            if (_compact && e.ClickCount == 2)
            {
                SetCompact(false);
                return;
            }

            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
                // Nhả chuột trước khi kéo.
            }
        };

        _controller.Changed += (_, _) => Dispatcher.Invoke(Render);
    }

    public void ApplyTheme(bool dark)
    {
        var fore = dark ? Brushes.White : new SolidColorBrush(Color.FromRgb(30, 30, 40));
        _root.Background = new SolidColorBrush(dark ? Color.FromArgb(240, 26, 26, 36) : Color.FromArgb(245, 250, 250, 252));
        _root.BorderBrush = new SolidColorBrush(Color.FromArgb(70, 139, 92, 246));
        _root.BorderThickness = new Thickness(1.5);
        System.Windows.Documents.TextElement.SetForeground(_root, fore);
        _clock.Foreground = fore;
        _status.Foreground = new SolidColorBrush(dark ? Color.FromArgb(170, 255, 255, 255) : Color.FromArgb(160, 30, 30, 40));
    }

    /// <summary>Hiện mini-player ở góc dưới phải vùng làm việc (lần đầu) và nạp danh sách thói quen.</summary>
    public async Task PresentAsync()
    {
        _habits = await _candidates();
        _habit.Items.Clear();
        _habit.Items.Add(HostText.FocusNoHabit);

        foreach (var habit in _habits)
        {
            _habit.Items.Add((habit.Kind == HabitKind.Duration ? "⏱ " : string.Empty) + habit.Name);
        }

        var prefs = _controller.Prefs;
        _mode.SelectedIndex = (int)prefs.Mode;
        _minutes.Text = prefs.WorkMinutes.ToString(CultureInfo.InvariantCulture);
        var index = prefs.HabitId is { } id ? _habits.ToList().FindIndex(h => h.Id == id) : -1;
        _habit.SelectedIndex = index + 1;

        if (!IsVisible)
        {
            Show();
            var area = SystemParameters.WorkArea;
            Left = area.Right - ActualWidth - 24;
            Top = area.Bottom - ActualHeight - 24;
        }

        Render();
    }

    private async Task OnPrimaryAsync()
    {
        var timer = _controller.Timer;

        if (timer is null || timer.Phase is FocusPhase.Idle or FocusPhase.Finished or FocusPhase.Break)
        {
            var mode = (FocusMode)Math.Max(0, _mode.SelectedIndex);
            var minutes = int.TryParse(_minutes.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var m) ? Math.Clamp(m, 1, 600) : 25;
            Guid? habit = _habit.SelectedIndex > 0 ? _habits[_habit.SelectedIndex - 1].Id : null;
            await _controller.StartAsync(mode, minutes, habit, keepHabit: false);
        }
        else
        {
            await _controller.ToggleAsync();
        }
    }

    private void SetCompact(bool compact)
    {
        _compact = compact;
        _setup.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;

        foreach (UIElement child in _expanded.Children)
        {
            child.Visibility = compact && !ReferenceEquals(child, _clock) ? Visibility.Collapsed : Visibility.Visible;
        }

        _clock.FontSize = compact ? 22 : 44;
        _root.Padding = compact ? new Thickness(14, 6, 14, 6) : new Thickness(16, 12, 16, 14);
        ToolTip = compact ? (HostText.IsVietnamese ? "Nhấp đúp để mở rộng" : "Double-click to expand") : null;
        Render();
    }

    private void Render()
    {
        var now = DateTime.UtcNow;
        var timer = _controller.Timer;
        var running = timer is { Phase: FocusPhase.Work };
        var shown = timer?.Remaining(now) ?? timer?.Elapsed(now) ?? TimeSpan.FromMinutes(_controller.Prefs.WorkMinutes);

        if (timer is null || timer.Phase is FocusPhase.Idle or FocusPhase.Finished)
        {
            shown = TimeSpan.FromMinutes(_controller.Prefs.Mode == FocusMode.Stopwatch ? 0 : _controller.Prefs.WorkMinutes);
        }

        _clock.Text = shown.TotalHours >= 1 ? shown.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture) : shown.ToString(@"mm\:ss", CultureInfo.InvariantCulture);
        _status.Text = timer switch
        {
            { Phase: FocusPhase.Break } => HostText.FocusBreak,
            { IsPaused: true } when _controller.PausedBySleep => HostText.FocusPausedBySleep,
            { IsPaused: true } => HostText.FocusPause + "…",
            { Phase: FocusPhase.Work, HabitId: { } id } => _habits.FirstOrDefault(h => h.Id == id)?.Name ?? string.Empty,
            _ => string.Empty
        };

        _primary.Content = timer switch
        {
            { Phase: FocusPhase.Work, IsRunning: true } => HostText.FocusPause,
            { Phase: FocusPhase.Work } => HostText.FocusResume,
            _ => HostText.FocusStart
        };
        _stop.Content = HostText.FocusStop;
        _stop.IsEnabled = running;

        if (!_compact)
        {
            _setup.Visibility = running ? Visibility.Collapsed : Visibility.Visible;
        }
    }
}
