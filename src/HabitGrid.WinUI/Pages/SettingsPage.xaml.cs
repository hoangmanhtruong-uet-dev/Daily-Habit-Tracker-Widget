using HabitGrid.Core.Domain;
using HabitGrid.Presentation.Services;
using HabitGrid.Presentation.Theming;
using HabitGrid.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

namespace HabitGrid.WinUI.Pages;

/// <summary>Cài đặt: chủ đề màu, chế độ sáng/tối, giảm chuyển động, ngôn ngữ. Áp dụng ngay, không cần khởi động lại.</summary>
public sealed partial class SettingsPage : Page
{
    private readonly ThemeService _theme = App.Host.Get<ThemeService>();
    private readonly StackPanel _root = new() { Spacing = 16, Padding = new Thickness(36, 28, 36, 32), MaxWidth = 860, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly Dictionary<string, Border> _tiles = [];
    private ChipGroup? _modeChips;
    private static readonly ThemeMode[] ModeOrder = [ThemeMode.System, ThemeMode.Light, ThemeMode.Dark];
    private TextBlock? _lightOnlyNote;
    private bool _building;

    public SettingsPage()
    {
        InitializeComponent();
        Scroll.Content = _root;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        Build();
    }

    private void Build()
    {
        _building = true;
        _root.Children.Clear();
        _tiles.Clear();

        _root.Children.Add(new TextBlock { Text = Ui.T("settings.title"), Style = Res("TitleDisplayTextStyle") });

        // Chủ đề màu
        var themeCard = Card(Ui.T("settings.theme"));
        var tiles = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14 };

        foreach (var theme in Themes.All)
        {
            tiles.Children.Add(CreateTile(theme));
        }

        themeCard.Children.Add(tiles);
        _lightOnlyNote = new TextBlock { Text = Ui.T("settings.pastel.note"), Style = Res("BodySecondaryTextStyle") };
        themeCard.Children.Add(_lightOnlyNote);
        Wrap(themeCard);

        // Chế độ
        var modeCard = Card(Ui.T("settings.mode"));
        _modeChips = new ChipGroup { MinChipWidth = 130, MaxWidth = 460, HorizontalAlignment = HorizontalAlignment.Left };

        foreach (var key in new[] { "settings.mode.system", "settings.mode.light", "settings.mode.dark" })
        {
            _modeChips.Add(Ui.T(key));
        }

        _modeChips.SelectedIndex = Array.IndexOf(ModeOrder, _theme.Mode);
        _modeChips.SelectionChanged += async (_, _) =>
        {
            if (!_building)
            {
                await _theme.SetAsync(_theme.Theme, ModeOrder[Math.Max(0, _modeChips.SelectedIndex)], XamlRoot.Content as FrameworkElement);
            }
        };
        modeCard.Children.Add(_modeChips);
        Wrap(modeCard);

        // Giảm chuyển động
        var motionCard = Card(Ui.T("settings.motion"));
        var motion = new ToggleSwitch { IsOn = _theme.ReduceMotionSetting, OnContent = string.Empty, OffContent = string.Empty };
        AutomationProperties.SetName(motion, Ui.T("settings.motion"));
        motion.Toggled += async (_, _) =>
        {
            if (!_building)
            {
                await _theme.SetReduceMotionAsync(motion.IsOn);
            }
        };
        motionCard.Children.Add(new TextBlock { Text = Ui.T("settings.motion.hint"), Style = Res("BodySecondaryTextStyle"), TextWrapping = TextWrapping.Wrap });
        motionCard.Children.Add(motion);
        Wrap(motionCard);

        // Widget chạy cùng Windows (WidgetHost đọc khóa này và tự đăng ký/gỡ)
        var widgetsCard = Card(Ui.T("settings.widgets"));
        var autostart = new ToggleSwitch { OnContent = Ui.T("settings.autostart"), OffContent = Ui.T("settings.autostart") };
        AutomationProperties.SetName(autostart, Ui.T("settings.autostart"));
        var settingsStore = App.Host.Get<ISettingsStore>();
        _ = LoadAutostartAsync();
        async Task LoadAutostartAsync()
        {
            autostart.IsOn = await settingsStore.GetAsync(SettingKeys.WidgetsAutoStart) != "0";
        }

        autostart.Toggled += async (_, _) =>
        {
            if (!_building)
            {
                await settingsStore.SetAsync(SettingKeys.WidgetsAutoStart, autostart.IsOn ? "1" : "0");
            }
        };
        widgetsCard.Children.Add(new TextBlock { Text = Ui.T("settings.autostart.hint"), Style = Res("BodySecondaryTextStyle"), TextWrapping = TextWrapping.Wrap });
        widgetsCard.Children.Add(autostart);
        Wrap(widgetsCard);

        // Ngôn ngữ
        var languageCard = Card(Ui.T("settings.language"));
        var language = new ChipGroup { MinChipWidth = 130, MaxWidth = 320, HorizontalAlignment = HorizontalAlignment.Left };
        language.Add(Ui.T("settings.lang.vi"));
        language.Add(Ui.T("settings.lang.en"));
        language.SelectedIndex = Ui.Loc.Language == "en" ? 1 : 0;
        language.SelectionChanged += async (_, _) =>
        {
            if (!_building && App.MainWindowInstance is { } window)
            {
                await window.SetLanguageAsync(language.SelectedIndex == 1 ? "en" : "vi");
            }
        };
        languageCard.Children.Add(language);
        Wrap(languageCard);

        UpdateTiles();
        _building = false;
    }

    private static Style Res(string key) => (Style)Application.Current.Resources[key];

    private static StackPanel Card(string title)
    {
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock { Text = title.ToUpperInvariant(), Style = Res("SectionHeader"), Margin = new Thickness(0) });
        return panel;
    }

    private void Wrap(StackPanel content) =>
        _root.Children.Add(new Border { Style = Res("CardBorder"), Child = content });

    private UIElement CreateTile(ThemeDefinition theme)
    {
        var palette = theme.Dark ?? theme.Light!;
        var name = Ui.T(theme.NameKey);

        var swatch = new Border
        {
            Height = 64,
            CornerRadius = new CornerRadius(16),
            Background = Ui.GradientBrush(new HabitGrid.Core.Domain.GradientStops(palette.AccentStart, palette.AccentEnd, palette.AccentEnd, 0))
        };

        var canvas = new Border
        {
            Height = 22,
            CornerRadius = new CornerRadius(8),
            Background = Ui.Brush(theme.Light is { } light && theme.Dark is null ? light.Canvas : palette.Canvas),
            BorderBrush = (Brush)Application.Current.Resources["StrokeSubtleBrush"],
            BorderThickness = new Thickness(1)
        };

        var label = new TextBlock { Text = name, Style = Res("CardTitleTextStyle"), HorizontalAlignment = HorizontalAlignment.Center };
        var content = new StackPanel { Spacing = 8, Width = 150 };
        content.Children.Add(swatch);
        content.Children.Add(canvas);
        content.Children.Add(label);

        var tile = new Border
        {
            Padding = new Thickness(12),
            CornerRadius = new CornerRadius(22),
            BorderThickness = new Thickness(2),
            Background = (Brush)Application.Current.Resources["CardIdleBrush"],
            Child = content,
            IsTabStop = true,
            UseSystemFocusVisuals = true
        };
        AutomationProperties.SetName(tile, name);
        AutomationProperties.SetAccessibilityView(tile, Microsoft.UI.Xaml.Automation.Peers.AccessibilityView.Control);

        async void Apply()
        {
            if (_building)
            {
                return;
            }

            await _theme.SetAsync(theme, _theme.Mode, XamlRoot.Content as FrameworkElement);
            UpdateTiles();
        }

        tile.Tapped += (_, _) => Apply();
        tile.KeyDown += (_, e) =>
        {
            if (e.Key is Windows.System.VirtualKey.Enter or Windows.System.VirtualKey.Space)
            {
                Apply();
                e.Handled = true;
            }
        };

        _tiles[theme.Id] = tile;
        return tile;
    }

    private void UpdateTiles()
    {
        if (_lightOnlyNote is not null)
        {
            _lightOnlyNote.Visibility = _theme.Theme.Dark is null ? Visibility.Visible : Visibility.Collapsed;
        }

        foreach (var (id, tile) in _tiles)
        {
            var selected = id == _theme.Theme.Id;
            tile.BorderBrush = selected
                ? (Brush)Application.Current.Resources["AccentGradientBrush"]
                : (Brush)Application.Current.Resources["StrokeSubtleBrush"];
        }
    }
}
