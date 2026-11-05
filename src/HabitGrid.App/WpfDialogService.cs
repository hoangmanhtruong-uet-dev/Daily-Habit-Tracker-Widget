using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using HabitGrid.Core.Models;
using HabitGrid.UI.Services;
using HabitGrid.UI.Views;

namespace HabitGrid.App;

/// <summary>
/// Hiện thực <see cref="IDialogService"/> bằng cửa sổ WPF thật.
/// </summary>
public sealed class WpfDialogService : IDialogService
{
    private readonly HabitGrid.Platform.Licensing.ILicenseService? _licenseService;

    public WpfDialogService(HabitGrid.Platform.Licensing.ILicenseService? licenseService = null)
    {
        _licenseService = licenseService;
    }

    public Task<HabitEditResult?> ShowEditHabitAsync(Habit? existing)
    {
        var isPro = _licenseService?.IsPro ?? false;
        var dialog = new HabitEditDialog(existing, isPro) { Owner = Application.Current?.MainWindow };

        return Task.FromResult(dialog.ShowDialog() == true ? dialog.Result : null);
    }

    public Task<bool> ShowPaywallAsync(string? reason)
    {
        var dialog = new PaywallDialog(_licenseService, reason) { Owner = Application.Current?.MainWindow };

        return Task.FromResult(dialog.ShowDialog() == true && dialog.Purchased);
    }

    public Task<OnboardingChoice?> ShowOnboardingAsync()
    {
        var dialog = new OnboardingWindow();

        return Task.FromResult(dialog.ShowDialog() == true ? dialog.Choice : null);
    }

    public void ShowToast(string message, string? actionText = null, Action? action = null)
    {
        var toast = new ToastWindow(message, actionText, action);
        toast.Show();
    }
}

/// <summary>
/// Thanh thông báo nhỏ ở đáy màn hình, tự đóng sau 5 giây (10 giây nếu có nút hành động).
/// </summary>
public sealed class ToastWindow : Window
{
    private readonly Action? _action;

    public ToastWindow(string message, string? actionText = null, Action? action = null)
    {
        ArgumentNullException.ThrowIfNull(message);

        _action = action;

        Title = "HabitGrid";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        ShowActivated = false;

        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16, 12, 16, 12),
            Margin = new Thickness(16)
        };

        var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(new TextBlock
        {
            Text = message,
            Foreground = Brushes.White,
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 420,
            TextWrapping = TextWrapping.Wrap
        });

        if (!string.IsNullOrWhiteSpace(actionText))
        {
            var button = new Button
            {
                Content = actionText,
                Margin = new Thickness(16, 0, 0, 0),
                Padding = new Thickness(12, 4, 12, 4),
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(120, 190, 255)),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };

            button.Click += OnActionClick;
            panel.Children.Add(button);
        }

        border.Child = panel;
        Content = border;

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var workArea = SystemParameters.WorkArea;

        Left = workArea.Left + ((workArea.Width - ActualWidth) / 2);
        Top = workArea.Bottom - ActualHeight - 24;

        var lifetime = _action is null ? TimeSpan.FromSeconds(5) : TimeSpan.FromSeconds(10);

        var timer = new System.Windows.Threading.DispatcherTimer { Interval = lifetime };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            Close();
        };

        timer.Start();
    }

    private void OnActionClick(object sender, RoutedEventArgs e)
    {
        _action?.Invoke();
        Close();
    }
}
