using System.Reflection;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HabitGrid.Core.Settings;
using HabitGrid.Platform.Licensing;
using HabitGrid.UI.Localization;
using HabitGrid.UI.Services;
using HabitGrid.UI.Theme;

namespace HabitGrid.UI.ViewModels;

/// <summary>
/// ViewModel của trung tâm cài đặt.
/// <para>
/// Không có nút "Lưu": mọi thay đổi được ghi ngay xuống CSDL và áp dụng tức thì.
/// </para>
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    private readonly ThemeService _themeService;
    private readonly ILicenseService _license;
    private readonly ISettingsHost? _host;

    [ObservableProperty]
    private bool _isBusy;

    public SettingsViewModel(
        AppSettings settings,
        ThemeService themeService,
        ILicenseService license,
        ISettingsHost? host = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(themeService);
        ArgumentNullException.ThrowIfNull(license);

        _settings = settings;
        _themeService = themeService;
        _license = license;
        _host = host;

        _settings.SettingChanged += OnSettingChanged;

        ResetPositionsCommand = new AsyncRelayCommand(ResetPositionsAsync);
        OpenDataFolderCommand = new AsyncRelayCommand(OpenDataFolderAsync);
        ExportJsonCommand = new AsyncRelayCommand(ExportJsonAsync);
        ExportCsvCommand = new AsyncRelayCommand(ExportCsvAsync);
        ImportJsonCommand = new AsyncRelayCommand(ImportJsonAsync);
        CopyVersionCommand = new AsyncRelayCommand(CopyVersionAsync);
        UpgradeCommand = new AsyncRelayCommand(UpgradeAsync);
    }

    /// <summary>Chế độ màu.</summary>
    public AppThemeMode Theme
    {
        get => _settings.Theme;
        set
        {
            if (_settings.Theme == value)
            {
                return;
            }

            _settings.Theme = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Ngôn ngữ giao diện.</summary>
    public AppLanguage Language
    {
        get => _settings.Language;
        set
        {
            if (_settings.Language == value)
            {
                return;
            }

            _settings.Language = value;
            LocalizationManager.Instance.SetLanguage(value);
            OnPropertyChanged();
        }
    }

    /// <summary>Ngày bắt đầu tuần.</summary>
    public DayOfWeek WeekStart
    {
        get => _settings.WeekStart;
        set
        {
            if (_settings.WeekStart == value)
            {
                return;
            }

            _settings.WeekStart = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Mốc chuyển ngày mới (0 – 5 giờ).</summary>
    public int DayStartHour
    {
        get => _settings.DayStartHour;
        set
        {
            if (_settings.DayStartHour == value)
            {
                return;
            }

            _settings.DayStartHour = value;
            OnPropertyChanged();
        }
    }

    /// <summary>True nếu ứng dụng tự chạy khi khởi động Windows.</summary>
    public bool RunAtStartup
    {
        get => _settings.RunAtStartup;
        set
        {
            if (_settings.RunAtStartup == value)
            {
                return;
            }

            _settings.RunAtStartup = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Kiểu nền widget.</summary>
    public WidgetBackgroundStyle BackgroundStyle
    {
        get => _settings.BackgroundStyle;
        set
        {
            if (_settings.BackgroundStyle == value)
            {
                return;
            }

            _settings.BackgroundStyle = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Độ mờ nền widget (0 – 100).</summary>
    public double BackgroundOpacity
    {
        get => _settings.BackgroundOpacity;
        set
        {
            var clamped = Math.Clamp(value, 0, 100);

            if (Math.Abs(_settings.BackgroundOpacity - clamped) < 0.001)
            {
                return;
            }

            _settings.BackgroundOpacity = clamped;
            OnPropertyChanged();
        }
    }

    /// <summary>Bo góc widget (4 – 16 px).</summary>
    public double CornerRadius
    {
        get => _settings.CornerRadius;
        set
        {
            var clamped = Math.Clamp(value, 4, 16);

            if (Math.Abs(_settings.CornerRadius - clamped) < 0.001)
            {
                return;
            }

            _settings.CornerRadius = clamped;
            OnPropertyChanged();
        }
    }

    /// <summary>Chế độ ghim widget (0 = Desktop-level, 1 = Bottom-most, 2 = Always-on-top).</summary>
    public int PinningMode
    {
        get => _settings.PinningMode;
        set
        {
            if (_settings.PinningMode == value)
            {
                return;
            }

            _settings.PinningMode = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Cỡ widget mặc định (0 = Nhỏ, 1 = Vừa, 2 = Lớn).</summary>
    public int DefaultWidgetSize
    {
        get => _settings.DefaultWidgetSize;
        set
        {
            if (_settings.DefaultWidgetSize == value)
            {
                return;
            }

            _settings.DefaultWidgetSize = value;
            OnPropertyChanged();
        }
    }

    /// <summary>True nếu đang bật chế độ chỉnh sửa widget.</summary>
    public bool EditMode
    {
        get => _settings.EditMode;
        set
        {
            if (_settings.EditMode == value)
            {
                return;
            }

            _settings.EditMode = value;
            OnPropertyChanged();
        }
    }

    /// <summary>True nếu người dùng đã có Pro.</summary>
    public bool IsPro => _license.IsPro;

    /// <summary>Chuỗi trạng thái giấy phép.</summary>
    public string LicenseText => IsPro
        ? LocalizationManager.Translate("settings.license.pro")
        : LocalizationManager.Translate("settings.license.free");

    /// <summary>Thông tin phiên bản, ví dụ "1.0.0 (x64)".</summary>
    public string VersionText
    {
        get
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
            var architecture = RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant();

            return $"{version} ({architecture})";
        }
    }

    public IAsyncRelayCommand ResetPositionsCommand { get; }

    public IAsyncRelayCommand OpenDataFolderCommand { get; }

    public IAsyncRelayCommand ExportJsonCommand { get; }

    public IAsyncRelayCommand ExportCsvCommand { get; }

    public IAsyncRelayCommand ImportJsonCommand { get; }

    public IAsyncRelayCommand CopyVersionCommand { get; }

    public IAsyncRelayCommand UpgradeCommand { get; }

    private async Task ResetPositionsAsync() => await RunHostAsync(host => host.ResetWidgetPositionsAsync());

    private async Task OpenDataFolderAsync() => await RunHostAsync(host =>
    {
        host.OpenDataFolder();
        return Task.CompletedTask;
    });

    private async Task ExportJsonAsync() => await RunHostAsync(host => host.ExportJsonAsync());

    private async Task ExportCsvAsync() => await RunHostAsync(host => host.ExportCsvAsync());

    private async Task ImportJsonAsync() => await RunHostAsync(host => host.ImportJsonAsync());

    private async Task CopyVersionAsync() => await RunHostAsync(host =>
    {
        host.CopyVersionInfo(VersionText);
        return Task.CompletedTask;
    });

    private async Task UpgradeAsync() => await RunHostAsync(host => host.ShowPaywallAsync());

    private async Task RunHostAsync(Func<ISettingsHost, Task> action)
    {
        if (_host is null)
        {
            return;
        }

        IsBusy = true;

        try
        {
            await action(_host);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnSettingChanged(object? sender, string key)
    {
        // Cài đặt có thể được đổi từ nơi khác (ví dụ khay hệ thống) -> đồng bộ lại giao diện.
        OnPropertyChanged(nameof(Theme));
        OnPropertyChanged(nameof(Language));
        OnPropertyChanged(nameof(WeekStart));
        OnPropertyChanged(nameof(DayStartHour));
        OnPropertyChanged(nameof(RunAtStartup));
        OnPropertyChanged(nameof(BackgroundStyle));
        OnPropertyChanged(nameof(BackgroundOpacity));
        OnPropertyChanged(nameof(CornerRadius));
        OnPropertyChanged(nameof(PinningMode));
        OnPropertyChanged(nameof(DefaultWidgetSize));
        OnPropertyChanged(nameof(EditMode));
        OnPropertyChanged(nameof(LicenseText));
    }
}
