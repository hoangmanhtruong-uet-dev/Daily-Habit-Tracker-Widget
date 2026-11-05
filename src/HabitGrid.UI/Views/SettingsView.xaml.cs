using System.Windows;
using System.Windows.Controls;
using HabitGrid.Core.Settings;
using HabitGrid.UI.Localization;
using HabitGrid.UI.ViewModels;

namespace HabitGrid.UI.Views;

/// <summary>
/// Trung tâm cài đặt: 6 nhóm theo chuẩn Windows 11.
/// Mọi thay đổi được lưu ngay, không có nút "Lưu".
/// </summary>
public partial class SettingsView : UserControl
{
    private bool _suppressEvents;

    public SettingsView()
    {
        InitializeComponent();

        Loaded += OnLoaded;
    }

    /// <summary>ViewModel của trang cài đặt.</summary>
    public SettingsViewModel? ViewModel => DataContext as SettingsViewModel;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        _suppressEvents = true;

        SelectItem(LanguageBox, ViewModel.Language.ToString());
        SelectItem(WeekStartBox, ViewModel.WeekStart.ToString());
        SelectItem(ThemeBox, ViewModel.Theme.ToString());
        SelectItem(BackgroundStyleBox, ViewModel.BackgroundStyle.ToString());
        SelectItem(PinningBox, ViewModel.PinningMode.ToString());
        SelectItem(DefaultSizeBox, ViewModel.DefaultWidgetSize.ToString());

        DayCutoffSlider.Value = ViewModel.DayStartHour;
        OpacitySlider.Value = ViewModel.BackgroundOpacity;
        CornerRadiusSlider.Value = ViewModel.CornerRadius;

        RunAtStartupCheck.IsChecked = ViewModel.RunAtStartup;
        EditModeCheck.IsChecked = ViewModel.EditMode;

        DayCutoffText.Text = $"{ViewModel.DayStartHour:00}:00";
        OpacityText.Text = $"{ViewModel.BackgroundOpacity:F0}%";
        CornerRadiusText.Text = $"{ViewModel.CornerRadius:F0} px";

        LicenseText.Text = ViewModel.LicenseText;
        VersionText.Text = ViewModel.VersionText;
        AppNameText.Text = LocalizationManager.Translate("app.name");

        // Các nút sao lưu chỉ dành cho gói Pro.
        var isPro = ViewModel.IsPro;
        ExportJsonButton.Visibility = isPro ? Visibility.Visible : Visibility.Collapsed;
        ExportCsvButton.Visibility = isPro ? Visibility.Visible : Visibility.Collapsed;
        ImportJsonButton.Visibility = isPro ? Visibility.Visible : Visibility.Collapsed;
        UpgradeButton.Visibility = isPro ? Visibility.Collapsed : Visibility.Visible;

        _suppressEvents = false;
    }

    private static void SelectItem(ComboBox comboBox, string tag)
    {
        foreach (var item in comboBox.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag as string, tag, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedItem = item;
                return;
            }
        }

        if (comboBox.Items.Count > 0)
        {
            comboBox.SelectedIndex = 0;
        }
    }

    private string? SelectedTag(ComboBox comboBox) => (comboBox.SelectedItem as ComboBoxItem)?.Tag as string;

    private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressEvents || ViewModel is null)
        {
            return;
        }

        if (SelectedTag(LanguageBox) == "English")
        {
            ViewModel.Language = AppLanguage.English;
        }
        else
        {
            ViewModel.Language = AppLanguage.Vietnamese;
        }
    }

    private void OnWeekStartChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressEvents || ViewModel is null)
        {
            return;
        }

        ViewModel.WeekStart = SelectedTag(WeekStartBox) == "Sunday" ? DayOfWeek.Sunday : DayOfWeek.Monday;
    }

    private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressEvents || ViewModel is null)
        {
            return;
        }

        ViewModel.Theme = SelectedTag(ThemeBox) switch
        {
            "Light" => AppThemeMode.Light,
            "Dark" => AppThemeMode.Dark,
            _ => AppThemeMode.System
        };
    }

    private void OnBackgroundStyleChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressEvents || ViewModel is null)
        {
            return;
        }

        ViewModel.BackgroundStyle = SelectedTag(BackgroundStyleBox) switch
        {
            "Solid" => WidgetBackgroundStyle.Solid,
            "None" => WidgetBackgroundStyle.None,
            _ => WidgetBackgroundStyle.Acrylic
        };
    }

    private void OnDayCutoffChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (DayCutoffText is not null) { DayCutoffText.Text = $"{e.NewValue:00}:00"; }

        if (_suppressEvents || ViewModel is null)
        {
            return;
        }

        ViewModel.DayStartHour = (int)e.NewValue;
    }

    private void OnOpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (OpacityText is not null) { OpacityText.Text = $"{e.NewValue:F0}%"; }

        if (_suppressEvents || ViewModel is null)
        {
            return;
        }

        ViewModel.BackgroundOpacity = e.NewValue;
    }

    private void OnCornerRadiusChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (CornerRadiusText is not null) { CornerRadiusText.Text = $"{e.NewValue:F0} px"; }

        if (_suppressEvents || ViewModel is null)
        {
            return;
        }

        ViewModel.CornerRadius = e.NewValue;
    }

    private void OnRunAtStartupChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressEvents || ViewModel is null)
        {
            return;
        }

        ViewModel.RunAtStartup = RunAtStartupCheck.IsChecked == true;
    }

    private void OnEditModeChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressEvents || ViewModel is null)
        {
            return;
        }

        ViewModel.EditMode = EditModeCheck.IsChecked == true;
    }

    private void OnPinningChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressEvents || ViewModel is null)
        {
            return;
        }

        if (int.TryParse(SelectedTag(PinningBox), out var mode))
        {
            ViewModel.PinningMode = mode;
        }
    }

    private void OnDefaultSizeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressEvents || ViewModel is null)
        {
            return;
        }

        if (int.TryParse(SelectedTag(DefaultSizeBox), out var size))
        {
            ViewModel.DefaultWidgetSize = size;
        }
    }

    private async void OnResetPositionsClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.ResetPositionsCommand.ExecuteAsync(null);
        }
    }

    private async void OnOpenDataFolderClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.OpenDataFolderCommand.ExecuteAsync(null);
        }
    }

    private async void OnExportJsonClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.ExportJsonCommand.ExecuteAsync(null);
        }
    }

    private async void OnExportCsvClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.ExportCsvCommand.ExecuteAsync(null);
        }
    }

    private async void OnImportJsonClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.ImportJsonCommand.ExecuteAsync(null);
        }
    }

    private async void OnCopyVersionClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.CopyVersionCommand.ExecuteAsync(null);
        }
    }

    private async void OnUpgradeClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.UpgradeCommand.ExecuteAsync(null);
        }
    }
}
