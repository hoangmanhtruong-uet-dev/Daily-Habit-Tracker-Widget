using HabitGrid.Core.Repositories;
using HabitGrid.Core.Services;
using HabitGrid.Core.Settings;
using HabitGrid.Data;
using HabitGrid.Data.Repositories;
using HabitGrid.Platform.Desktop;
using HabitGrid.Platform.Licensing;
using HabitGrid.UI.Localization;
using HabitGrid.UI.Services;
using HabitGrid.UI.Theme;
using HabitGrid.UI.ViewModels;

namespace HabitGrid.App;

/// <summary>
/// Điểm khởi tạo dependencies của ứng dụng (composition root).
/// </summary>
public sealed class AppServices : IDisposable
{
    private bool _disposed;

    private AppServices()
    {
    }

    /// <summary>Khởi tạo toàn bộ services (bất đồng bộ vì CSDL dùng API async).</summary>
    public static async Task<AppServices> CreateAsync(HabitGrid.Platform.System.ISingleInstanceService? singleInstance = null)
    {
        var services = new AppServices();
        services.SingleInstance = singleInstance ?? new HabitGrid.Platform.System.SingleInstanceService();

        services.Database = HabitGridDatabase.CreateDefault();
        await services.Database.InitializeAsync().ConfigureAwait(false);

        services.Habits = new HabitRepository(services.Database);
        services.Entries = new EntryRepository(services.Database);
        services.SettingsRepository = new SettingsRepository(services.Database);
        services.WidgetStates = new WidgetStateRepository(services.Database);

        services.AppSettings = await AppSettings.CreateAsync(services.SettingsRepository).ConfigureAwait(false);

        services.ScreenProvider = new WindowsScreenProvider();
        services.DesktopHost = new DesktopHostService(services.ScreenProvider);

        services.License = new StoreLicenseService();
        services.Dialogs = new WpfDialogService(services.License);

        services.ThemeService = new ThemeService(services.AppSettings);
        services.SystemEvents = new SystemEventMonitor();
        services.Widgets = new WidgetManager(services);

        services.Startup = new HabitGrid.Platform.System.StartupService();
        services.ToastService = new HabitGrid.Platform.System.ToastNotificationService();

        // Đồng bộ RunAtStartup sang hệ thống khi cài đặt thay đổi
        services.AppSettings.SettingChanged += async (_, key) =>
        {
            if (key == SettingsKeys.RunAtStartup)
            {
                await services.Startup.SetAutoStartEnabledAsync(services.AppSettings.RunAtStartup);
            }
        };

        return services;
    }

    public HabitGridDatabase Database { get; private set; } = null!;

    public IHabitRepository Habits { get; private set; } = null!;

    public IEntryRepository Entries { get; private set; } = null!;

    public ISettingsRepository SettingsRepository { get; private set; } = null!;

    public IWidgetStateRepository WidgetStates { get; private set; } = null!;

    /// <summary>Cài đặt ứng dụng (tự lưu khi thay đổi).</summary>
    public AppSettings AppSettings { get; private set; } = null!;

    public IScreenProvider ScreenProvider { get; private set; } = null!;

    public DesktopHostService DesktopHost { get; private set; } = null!;

    public ILicenseService License { get; private set; } = null!;

    public IDialogService Dialogs { get; private set; } = null!;

    public ThemeService ThemeService { get; private set; } = null!;

    public SystemEventMonitor SystemEvents { get; private set; } = null!;

    public WidgetManager Widgets { get; private set; } = null!;

    public HabitGrid.Platform.System.IStartupService Startup { get; private set; } = null!;

    public HabitGrid.Platform.System.IToastNotificationService ToastService { get; private set; } = null!;

    public HabitGrid.Platform.System.ISingleInstanceService SingleInstance { get; private set; } = null!;

    /// <summary>Dựng ViewModel chính và nối sự kiện với quản lý widget.</summary>
    public MainViewModel CreateMainViewModel()
    {
        var deletion = new SafeDeletionManager(Habits, new DispatcherDeletionTimer());

        var viewModel = new MainViewModel(
            Habits,
            Entries,
            WidgetStates,
            License,
            new StreakCalculator(new ScheduleCalculator()),
            new SettingsDayProvider(AppSettings),
            Dialogs,
            deletion);

        viewModel.WidgetVisibilityRequested += async (_, args) =>
            await Widgets.SetWidgetVisibleAsync(args.HabitId, args.Visible);

        return viewModel;
    }

    /// <summary>Dựng ViewModel cài đặt.</summary>
    public SettingsViewModel CreateSettingsViewModel(ISettingsHost host) =>
        new(AppSettings, ThemeService, License, host);

    /// <summary>Áp dụng ngôn ngữ đã lưu.</summary>
    public void ApplyLanguage()
    {
        LocalizationManager.Instance.SetLanguage(AppSettings.Language);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        Widgets?.Dispose();
        (SingleInstance as IDisposable)?.Dispose();
        SystemEvents?.Dispose();
        Database?.Dispose();
    }
}
