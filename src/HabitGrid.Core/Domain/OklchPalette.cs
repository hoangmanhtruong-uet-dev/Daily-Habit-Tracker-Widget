using System.Globalization;

namespace HabitGrid.Core.Domain;

/// <summary>Thang màu 5 mức (0 = trống, 1–4 = đậm dần).</summary>
public sealed record HeatmapShades(string Empty, string Level1, string Level2, string Level3, string Level4)
{
    public string this[int level] => level switch
    {
        0 => Empty,
        1 => Level1,
        2 => Level2,
        3 => Level3,
        4 => Level4,
        _ => throw new ArgumentOutOfRangeException(nameof(level))
    };
}

/// <summary>Sinh thang màu trong không gian OKLCH để độ đậm đều mắt ở cả hai theme.</summary>
public static class OklchPalette
{
    public const string EmptyLight = "#EBEDF0";
    public const string EmptyDark = "#26262E";

    // Độ sáng (L) mục tiêu cho 4 mức, từ nhạt đến đậm ở theme sáng; theme tối đi ngược lại.
    private static readonly double[] LightLevels = [0.88, 0.76, 0.64, 0.52];
    private static readonly double[] DarkLevels = [0.34, 0.46, 0.60, 0.74];

    public static HeatmapShades Generate(string baseHex, bool isDark)
    {
        var (r, g, b) = ParseHex(baseHex);
        var (_, chroma, hue) = ToOklch(r, g, b);

        // Giữ độ rực của màu gốc nhưng không quá xám hay quá chói.
        var c = Math.Clamp(chroma, 0.04, 0.20);
        var levels = isDark ? DarkLevels : LightLevels;

        var shades = levels.Select((l, i) => FromOklch(l, c * (0.55 + 0.15 * i), hue)).ToArray();
        return new HeatmapShades(isDark ? EmptyDark : EmptyLight, shades[0], shades[1], shades[2], shades[3]);
    }

    public static (double L, double C, double H) ToOklch(int r, int g, int b)
    {
        var lr = ToLinear(r / 255.0);
        var lg = ToLinear(g / 255.0);
        var lb = ToLinear(b / 255.0);

        var l = Math.Cbrt(0.4122214708 * lr + 0.5363325363 * lg + 0.0514459929 * lb);
        var m = Math.Cbrt(0.2119034982 * lr + 0.6806995451 * lg + 0.1073969566 * lb);
        var s = Math.Cbrt(0.0883024619 * lr + 0.2817188376 * lg + 0.6299787005 * lb);

        var okL = 0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s;
        var okA = 1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s;
        var okB = 0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s;

        var chroma = Math.Sqrt(okA * okA + okB * okB);
        var hue = Math.Atan2(okB, okA) * 180.0 / Math.PI;
        if (hue < 0)
        {
            hue += 360.0;
        }

        return (okL, chroma, hue);
    }

    public static string FromOklch(double l, double c, double hueDegrees)
    {
        var h = hueDegrees * Math.PI / 180.0;
        var a = c * Math.Cos(h);
        var b = c * Math.Sin(h);

        var l_ = l + 0.3963377774 * a + 0.2158037573 * b;
        var m_ = l - 0.1055613458 * a - 0.0638541728 * b;
        var s_ = l - 0.0894841775 * a - 1.2914855480 * b;

        var lc = l_ * l_ * l_;
        var mc = m_ * m_ * m_;
        var sc = s_ * s_ * s_;

        var r = 4.0767416621 * lc - 3.3077115913 * mc + 0.2309699292 * sc;
        var g = -1.2684380046 * lc + 2.6097574011 * mc - 0.3413193965 * sc;
        var bl = -0.0041960863 * lc - 0.7034186147 * mc + 1.7076147010 * sc;

        return $"#{ToByte(r):X2}{ToByte(g):X2}{ToByte(bl):X2}";
    }

    private static int ToByte(double linear)
    {
        var clamped = Math.Clamp(linear, 0.0, 1.0);
        return (int)Math.Round(FromLinear(clamped) * 255.0);
    }

    private static double ToLinear(double c) => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);

    private static double FromLinear(double c) => c <= 0.0031308 ? 12.92 * c : 1.055 * Math.Pow(c, 1 / 2.4) - 0.055;

    private static (int R, int G, int B) ParseHex(string hex)
    {
        var s = hex.TrimStart('#');
        if (s.Length != 6)
        {
            throw new FormatException($"Mã màu không hợp lệ: '{hex}'.");
        }

        return (
            int.Parse(s[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            int.Parse(s[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            int.Parse(s[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    }
}
