using System.Windows;
using System.Windows.Media;
using HabitGrid.Core.Settings;
using Microsoft.Win32;

namespace HabitGrid.UI.Theme;

/// <summary>
/// Áp dụng theme sáng/tối cho toàn ứng dụng bằng cách hoán đổi ResourceDictionary.
/// <para>
/// Các view dùng <c>{DynamicResource ...}</c> nên tự cập nhật ngay khi theme đổi,
/// không cần mở lại cửa sổ.
/// </para>
/// </summary>
public sealed class ThemeService
{
    private const string LightDictionaryUri = "pack://application:,,,/HabitGrid.UI;component/Themes/Theme.Light.xaml";
    private const string DarkDictionaryUri = "pack://application:,,,/HabitGrid.UI;component/Themes/Theme.Dark.xaml";

    private readonly AppSettings _settings;

    public ThemeService(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;

        _settings.SettingChanged += OnSettingChanged;
    }

    /// <summary>True nếu đang dùng theme tối.</summary>
    public bool IsDarkTheme { get; private set; }

    /// <summary>Được gọi khi theme đã được áp dụng lại (để widget cập nhật màu bảng màu heatmap).</summary>
    public event EventHandler<bool>? ThemeApplied;

    /// <summary>Áp dụng theme theo cài đặt hiện tại.</summary>
    public void ApplyTheme()
    {
        var isDark = _settings.Theme switch
        {
            AppThemeMode.Dark => true,
            AppThemeMode.Light => false,
            _ => IsSystemDark()
        };

        ApplyTheme(isDark);
    }

    /// <summary>Áp dụng theme tối/sáng cụ thể.</summary>
    public void ApplyTheme(bool isDark)
    {
        IsDarkTheme = isDark;

        var application = Application.Current;

        if (application is not null)
        {
            var uri = new Uri(isDark ? DarkDictionaryUri : LightDictionaryUri);
            var dictionary = new ResourceDictionary { Source = uri };

            // Thay thế dictionary theme đang có, giữ nguyên các tài nguyên khác.
            var existing = application.Resources.MergedDictionaries
                .FirstOrDefault(d => d.Source?.OriginalString.Contains("Theme.", StringComparison.OrdinalIgnoreCase) == true);

            if (existing is not null)
            {
                application.Resources.MergedDictionaries.Remove(existing);
            }

            application.Resources.MergedDictionaries.Add(dictionary);
        }

        ThemeApplied?.Invoke(this, isDark);
    }

    /// <summary>Đọc cài đặt màu của Windows để hỗ trợ chế độ "Theo hệ thống".</summary>
    public static bool IsSystemDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");

            if (key?.GetValue("AppsUseLightTheme") is int lightTheme)
            {
                return lightTheme == 0;
            }
        }
        catch (Exception)
        {
            // Không đọc được registry (ví dụ môi trường bị hạn chế) -> dùng nền sáng.
        }

        return false;
    }

    /// <summary>Màu nền widget theo theme.</summary>
    public static Color GetWidgetBackgroundColor(bool isDarkTheme) => isDarkTheme
        ? Color.FromRgb(32, 32, 32)
        : Color.FromRgb(245, 245, 245);

    private void OnSettingChanged(object? sender, string key)
    {
        if (key == SettingsKeys.Theme)
        {
            ApplyTheme();
        }
    }
}
