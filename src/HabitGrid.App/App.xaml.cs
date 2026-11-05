using System.IO;
using System.Windows;
using HabitGrid.Core.Models;
using HabitGrid.Core.Services;

namespace HabitGrid.App;

/// <summary>
/// Điểm khởi động ứng dụng.
/// </summary>
public partial class App : Application
{
    /// <summary>Services dùng chung cho toàn ứng dụng.</summary>
    public static AppServices Services { get; private set; } = null!;

    private SystemTrayManager? _trayManager;
    private DayTransitionManager? _dayTransitionManager;
    private bool _isExiting;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            await StartAsync(e);
        }
        catch (Exception ex)
        {
            ReportFatalStartupError(ex);
        }
    }

    private static void ReportFatalStartupError(Exception ex)
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HabitGrid");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "startup-error.log"), $"[{DateTime.Now:O}]{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Không để lỗi ghi log che mất lỗi gốc.
        }

        MessageBox.Show(
            $"HabitGrid không thể khởi động.\n\n{ex.Message}",
            "HabitGrid",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        Current.Shutdown(-1);
    }

    private async Task StartAsync(StartupEventArgs e)
    {
        // Kiểm tra single-instance TRƯỚC khi mở CSDL để instance thứ hai không đụng vào DB.
        var singleInstance = new HabitGrid.Platform.System.SingleInstanceService();

        if (!singleInstance.TryAcquire())
        {
            await singleInstance.NotifyExistingInstanceAsync(e.Args);
            Shutdown();
            return;
        }

        Services = await AppServices.CreateAsync(singleInstance);

        Services.SingleInstance.ArgumentsReceived += (_, args) =>
        {
            Dispatcher.Invoke(() =>
            {
                if (MainWindow != null)
                {
                    MainWindow.Show();
                    MainWindow.WindowState = WindowState.Normal;
                    MainWindow.Activate();

                    if (args.Contains("--settings", StringComparer.OrdinalIgnoreCase) &&
                        MainWindow is UI.Views.MainWindow win)
                    {
                        win.ShowSettingsPage();
                    }
                }
            });
        };

        // Ngôn ngữ và theme lấy từ cài đặt đã lưu.
        Services.ApplyLanguage();
        Services.ThemeService.ApplyTheme();

        // Chế độ POC: mở bảng điều khiển kiểm thử 7 tiêu chí ghim desktop.
        if (e.Args.Contains("--poc", StringComparer.OrdinalIgnoreCase))
        {
            var pocWindow = new PocWindow();
            MainWindow = pocWindow;
            pocWindow.Show();
            return;
        }

        await RunOnboardingIfNeededAsync();

        var viewModel = Services.CreateMainViewModel();
        await viewModel.LoadAsync();

        var settingsHost = new SettingsHost(Services);
        var settingsViewModel = Services.CreateSettingsViewModel(settingsHost);

        var mainWindow = new UI.Views.MainWindow(viewModel, settingsViewModel);
        MainWindow = mainWindow;

        // Thu về khay khi bấm nút X thay vì tắt toàn bộ ứng dụng
        mainWindow.Closing += (_, args) =>
        {
            if (!_isExiting)
            {
                args.Cancel = true;
                mainWindow.Hide();
            }
        };

        // Khởi tạo khay hệ thống (Tray Icon)
        _trayManager = new SystemTrayManager(
            Services.AppSettings,
            openDashboard: () =>
            {
                mainWindow.Show();
                mainWindow.WindowState = WindowState.Normal;
                mainWindow.Activate();
            },
            openSettings: () =>
            {
                mainWindow.Show();
                mainWindow.WindowState = WindowState.Normal;
                mainWindow.Activate();
                mainWindow.ShowSettingsPage();
            },
            toggleWidgets: () => Services.Widgets.ToggleAllVisibility(),
            exitApp: () =>
            {
                _isExiting = true;
                Shutdown();
            });

        // Khởi tạo bộ giám sát chuyển ngày & nhắc nhở toast
        _dayTransitionManager = new DayTransitionManager(Services, Services.ToastService);

        var isSilentStartup = e.Args.Contains("/startup", StringComparer.OrdinalIgnoreCase) ||
                              e.Args.Contains("/background", StringComparer.OrdinalIgnoreCase) ||
                              e.Args.Contains("--background", StringComparer.OrdinalIgnoreCase);

        if (!isSilentStartup)
        {
            mainWindow.Show();

            if (e.Args.Contains("--settings", StringComparer.OrdinalIgnoreCase))
            {
                mainWindow.ShowSettingsPage();
            }
        }

        // Mọi thay đổi cài đặt được áp dụng ngay lên widget đang mở.
        Services.AppSettings.SettingChanged += async (_, key) => await Services.Widgets.ApplySettingsAsync();
        Services.ThemeService.ThemeApplied += (_, _) => _ = Services.Widgets.ApplySettingsAsync();

        await Services.Widgets.LoadAsync();
    }

    /// <summary>
    /// Luồng làm quen: nếu chưa có thói quen nào thì mở hướng dẫn 3 bước.
    /// Mục tiêu: từ lúc mở app đến khi có widget trên desktop dưới 30 giây.
    /// </summary>
    private static async Task RunOnboardingIfNeededAsync()
    {
        var habits = await Services.Habits.GetAllIncludingArchivedAsync();

        if (habits.Count > 0)
        {
            return;
        }

        var choice = await Services.Dialogs.ShowOnboardingAsync();

        if (choice is null)
        {
            return;
        }

        var habit = new Habit
        {
            Name = choice.Name,
            Icon = choice.IconKey,
            Kind = choice.Kind,
            Target = choice.Target,
            StartDate = new DayProvider(TimeSpan.FromHours(Services.AppSettings.DayStartHour)).GetToday(),
            SortOrder = 0
        };

        await Services.Habits.InsertAsync(habit);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _dayTransitionManager?.Dispose();
        _trayManager?.Dispose();
        Services?.Dispose();
        base.OnExit(e);
    }
}
