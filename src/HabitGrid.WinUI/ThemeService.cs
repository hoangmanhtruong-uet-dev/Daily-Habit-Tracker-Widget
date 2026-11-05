using HabitGrid.Core.Domain;
using HabitGrid.Presentation.Theming;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI.ViewManagement;

namespace HabitGrid.WinUI;

/// <summary>
/// Áp dụng theme lúc chạy (03-UI-REBUILD-GLOW2 §8): đổi màu của các brush dùng chung trong <c>Tokens.xaml</c>
/// thay vì thay brush, nên mọi chỗ đang dùng được cập nhật ngay.
/// </summary>
public sealed class ThemeService
{
    private readonly ISettingsStore _settings;
    private readonly UISettings _uiSettings = new();

    public ThemeService(ISettingsStore settings)
    {
        _settings = settings;
    }

    public ThemeDefinition Theme { get; private set; } = Themes.CandyBento;

    /// <summary>
    /// Theme hiển thị thay thế khi theme đã chọn không còn quyền dùng (Plus hết hạn): hiện Candy Bento nhưng vẫn giữ lựa chọn
    /// đã lưu để nâng cấp lại là dùng tiếp (SPEC-V2-10 §5).
    /// </summary>
    public ThemeDefinition? Override { get; set; }

    public ThemeDefinition Effective => Override ?? Theme;

    public ThemeMode Mode { get; private set; } = ThemeMode.System;

    public ThemePalette Palette { get; private set; } = Themes.CandyBento.Dark!;

    /// <summary>Phát sau khi bảng màu đổi (nền động, title bar cập nhật theo).</summary>
    public event EventHandler<ThemePalette>? Changed;

    public bool AnimationsEnabled { get; private set; } = true;

    public async Task LoadAsync()
    {
        Theme = Themes.Find(await _settings.GetAsync(SettingKeys.Theme));
        Mode = Enum.TryParse<ThemeMode>(await _settings.GetAsync(SettingKeys.ThemeMode), out var mode) ? mode : ThemeMode.System;
        ReduceMotionSetting = await _settings.GetAsync(SettingKeys.ReduceMotion) == "1";
        AnimationsEnabled = _uiSettings.AnimationsEnabled && !ReduceMotionSetting;
    }

    /// <summary>Bật/tắt giảm chuyển động ngay lập tức và nhớ lựa chọn.</summary>
    public async Task SetReduceMotionAsync(bool reduce)
    {
        await _settings.SetAsync(SettingKeys.ReduceMotion, reduce ? "1" : "0");
        ReduceMotionSetting = reduce;
        AnimationsEnabled = _uiSettings.AnimationsEnabled && !reduce;
        Changed?.Invoke(this, Palette);
    }

    /// <summary>Lựa chọn của người dùng (độc lập với cài đặt hệ thống).</summary>
    public bool ReduceMotionSetting { get; private set; }

    public async Task SetAsync(ThemeDefinition theme, ThemeMode mode, FrameworkElement? root)
    {
        Theme = theme;
        Mode = mode;
        await _settings.SetAsync(SettingKeys.Theme, theme.Id);
        await _settings.SetAsync(SettingKeys.ThemeMode, mode.ToString());
        Apply(root);
    }

    public void Apply(FrameworkElement? root)
    {
        Palette = Effective.Resolve(Mode, SystemIsDark());
        var resources = Application.Current.Resources;

        void Set(string key, string hex)
        {
            if (resources.TryGetValue(key, out var value) && value is SolidColorBrush brush)
            {
                brush.Color = Ui.ParseColor(hex, brush.Color);
            }
        }

        var p = Palette;
        Set("CanvasBrush", p.Canvas);
        Set("SurfaceBrush", p.Surface);
        Set("Surface2Brush", p.SurfaceStrong);
        Set("StrokeSubtleBrush", p.Stroke);
        Set("StrokeHighlightBrush", p.StrokeHighlight);
        Set("TextPrimaryBrush", p.TextPrimary);
        Set("TextSecondaryBrush", p.TextSecondary);
        Set("TextMutedBrush", p.TextMuted);
        Set("AccentBrandBrush", p.AccentStart);
        Set("CardIdleBrush", p.CardIdle);
        Set("HeatEmptyBrush", p.HeatEmpty);
        Set("SuccessBrush", p.Success);
        Set("WarningBrush", p.Warning);
        Set("FreezeBrush", p.Freeze);
        Set("RailBrush", p.Rail);
        Set("RailSelectedTextBrush", p.RailSelectedText);

        if (resources.TryGetValue("AccentGradientBrush", out var gradient) && gradient is LinearGradientBrush accent && accent.GradientStops.Count >= 2)
        {
            accent.GradientStops[0].Color = Ui.ParseColor(p.AccentStart);
            accent.GradientStops[1].Color = Ui.ParseColor(p.AccentEnd);
        }

        // Control hệ thống (TextBox, NumberBox…) theo sáng/tối của bảng màu.
        if (root is not null)
        {
            root.RequestedTheme = p.IsDark ? ElementTheme.Dark : ElementTheme.Light;
        }

        Changed?.Invoke(this, p);
    }

    private bool SystemIsDark()
    {
        // Nền hệ thống tối ⇔ màu Background có độ sáng thấp.
        var bg = _uiSettings.GetColorValue(UIColorType.Background);
        return bg.R + bg.G + bg.B < 3 * 128;
    }
}
