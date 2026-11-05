namespace HabitGrid.Core.Services;

/// <summary>
/// Hiện thực <see cref="IPaletteGenerator"/>.
///
/// Nguyên tắc:
/// - Theme Sáng: Level1 nhạt nhất → Level4 đậm nhất.
/// - Theme Tối: Level1 tối nhất (gần nền) → Level4 sáng nhất (nổi bật nhất trên nền tối).
/// - Sinh màu từ màu gốc bằng cách giữ nguyên Hue/Saturation, điều chỉnh Lightness theo một thang cố định
///   nên thứ tự độ sáng luôn đơn điệu và không bao giờ trùng màu.
/// </summary>
public sealed class PaletteGenerator : IPaletteGenerator
{
    /// <summary>Mã preset mặc định (xanh lá kiểu GitHub).</summary>
    public const string DefaultPresetId = "default";

    /// <summary>Màu ô rỗng khi dùng theme Sáng.</summary>
    public const string EmptyColorLight = "#EBEDF0";

    /// <summary>Màu ô rỗng khi dùng theme Tối.</summary>
    public const string EmptyColorDark = "#161B22";

    /// <summary>Khoảng cách tối thiểu về độ sáng giữa hai mức liên tiếp.</summary>
    private const double LightnessSpan = 0.34;

    private static readonly IReadOnlyDictionary<string, PresetShades> Presets =
        new Dictionary<string, PresetShades>(StringComparer.OrdinalIgnoreCase)
        {
            [DefaultPresetId] = new PresetShades(
                Light: ["#9BE9A8", "#40C463", "#30A14E", "#216E39"],
                Dark: ["#0E4429", "#006D32", "#26A641", "#39D353"]),

            ["blue"] = new PresetShades(
                Light: ["#A5D8FF", "#5AB0F5", "#2F80D0", "#1B4F8F"],
                Dark: ["#0A2A52", "#12599E", "#2E86DE", "#58B6FF"]),

            ["purple"] = new PresetShades(
                Light: ["#D6BCFA", "#B07CF0", "#8B4FD0", "#5B2C9B"],
                Dark: ["#2B1650", "#5B2C9B", "#8B4FD0", "#B07CF0"]),

            ["orange"] = new PresetShades(
                Light: ["#FFD8A8", "#FFA94D", "#F77F00", "#B85C00"],
                Dark: ["#4A2503", "#8A4405", "#C2650A", "#F5931E"]),

            ["pink"] = new PresetShades(
                Light: ["#FFC9DE", "#FF8FB8", "#E5508A", "#A02A5C"],
                Dark: ["#430D24", "#8C1F4E", "#C93A78", "#F077A8"]),

            ["grayscale"] = new PresetShades(
                Light: ["#D0D0D0", "#A0A0A0", "#707070", "#404040"],
                Dark: ["#232323", "#3D3D3D", "#5C5C5C", "#7A7A7A"])
        };

    /// <summary>Danh sách mã preset hiện có, dùng cho màn hình chọn màu (tính năng Pro).</summary>
    public static IReadOnlyCollection<string> AvailablePresetIds => Presets.Keys.ToArray();

    public HeatmapShades GenerateFromHex(string baseHexColor, bool isDarkTheme)
    {
        var (r, g, b) = ColorMath.ParseHex(baseHexColor);
        var (h, s, l) = ColorMath.ToHsl(r, g, b);

        var shades = BuildShades(h, s, l, isDarkTheme);
        return new HeatmapShades(shades[0], shades[1], shades[2], shades[3], shades[4]);
    }

    public HeatmapShades GetPreset(string presetId, bool isDarkTheme)
    {
        var id = string.IsNullOrWhiteSpace(presetId) ? DefaultPresetId : presetId;

        if (!Presets.TryGetValue(id, out var preset))
        {
            preset = Presets[DefaultPresetId];
        }

        var levels = isDarkTheme ? preset.Dark : preset.Light;
        var empty = isDarkTheme ? EmptyColorDark : EmptyColorLight;

        return new HeatmapShades(empty, levels[0], levels[1], levels[2], levels[3]);
    }

    /// <summary>
    /// Dựng 5 mức màu từ Hue/Saturation cơ sở.
    /// Mức 0 là màu ô rỗng theo theme; 4 mức còn lại nằm trên một thang độ sáng đơn điệu.
    /// </summary>
    private static string[] BuildShades(double h, double s, double l, bool isDarkTheme)
    {
        var empty = isDarkTheme ? EmptyColorDark : EmptyColorLight;

        double lightest;
        double darkest;

        if (isDarkTheme)
        {
            // Theme tối: mức 1 tối nhất, mức 4 sáng nhất.
            darkest = Math.Clamp(l - LightnessSpan, 0.10, 0.42);
            lightest = Math.Clamp(l + LightnessSpan, 0.50, 0.92);
        }
        else
        {
            // Theme sáng: mức 1 nhạt nhất, mức 4 đậm nhất.
            lightest = Math.Clamp(l + LightnessSpan, 0.55, 0.88);
            darkest = Math.Clamp(l - LightnessSpan, 0.10, 0.45);
        }

        var span = lightest - darkest;

        var levels = new string[5];
        levels[0] = empty;

        if (isDarkTheme)
        {
            // Theme tối: Level1 (tối nhất) -> Level4 (sáng nhất).
            levels[1] = HexFromHsl(h, s, darkest);
            levels[2] = HexFromHsl(h, s, lightest - (span * 0.66));
            levels[3] = HexFromHsl(h, s, lightest - (span * 0.33));
            levels[4] = HexFromHsl(h, s, lightest);
        }
        else
        {
            // Theme sáng: Level1 (nhạt nhất) -> Level4 (đậm nhất).
            levels[1] = HexFromHsl(h, s, lightest);
            levels[2] = HexFromHsl(h, s, lightest - (span * 0.33));
            levels[3] = HexFromHsl(h, s, lightest - (span * 0.66));
            levels[4] = HexFromHsl(h, s, darkest);
        }

        return levels;
    }

    private static string HexFromHsl(double h, double s, double l)
    {
        var (r, g, b) = ColorMath.FromHsl(h, s, l);
        return ColorMath.ToHex(r, g, b);
    }

    private sealed record PresetShades(string[] Light, string[] Dark);
}
