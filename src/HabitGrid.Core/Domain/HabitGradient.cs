using System.Globalization;

namespace HabitGrid.Core.Domain;

/// <summary>
/// Gradient 2 điểm của một thói quen (03-UI-REBUILD-GLOW2 §4.1).
/// <see cref="ScrimOpacity"/> là độ phủ đen tối thiểu dưới chữ trắng để đạt tương phản WCAG AA (4.5:1) trên cả hai đầu gradient.
/// </summary>
public sealed record GradientStops(string Start, string End, string Glow, double ScrimOpacity)
{
    public bool NeedsScrim => ScrimOpacity > 0;
}

public static class HabitGradient
{
    public const double MinTextContrast = 4.5;

    private static readonly Dictionary<string, GradientStops> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object Gate = new();

    /// <summary>Sinh gradient từ màu gốc trong không gian OKLCH: đầu sáng hơn và lệch hue −18°, cuối tối hơn và lệch +22°.</summary>
    public static GradientStops From(string baseHex)
    {
        lock (Gate)
        {
            if (Cache.TryGetValue(baseHex, out var cached))
            {
                return cached;
            }
        }

        var (r, g, b) = Parse(baseHex);
        var (l, c, h) = OklchPalette.ToOklch(r, g, b);

        // Giữ độ sáng trong vùng mà chữ trắng còn đọc được; chroma giữ độ rực nhưng không quá chói.
        var chroma = Math.Clamp(c, 0.06, 0.22);
        var start = OklchPalette.FromOklch(Math.Clamp(l + 0.06, 0.48, 0.74), chroma, Wrap(h - 18));
        var end = OklchPalette.FromOklch(Math.Clamp(l - 0.04, 0.38, 0.66), chroma, Wrap(h + 22));
        var glow = OklchPalette.FromOklch(Math.Clamp(l, 0.55, 0.75), chroma, h);

        var scrim = Math.Max(ScrimFor(start), ScrimFor(end));
        var stops = new GradientStops(start, end, glow, scrim);

        lock (Gate)
        {
            Cache[baseHex] = stops;
        }

        return stops;
    }

    /// <summary>Tỷ lệ tương phản WCAG giữa hai màu hex.</summary>
    public static double ContrastRatio(string hexA, string hexB)
    {
        var la = RelativeLuminance(hexA);
        var lb = RelativeLuminance(hexB);
        var (lighter, darker) = la > lb ? (la, lb) : (lb, la);
        return (lighter + 0.05) / (darker + 0.05);
    }

    /// <summary>Màu sau khi phủ một lớp đen với độ mờ <paramref name="opacity"/>.</summary>
    public static string Darken(string hex, double opacity)
    {
        var (r, g, b) = Parse(hex);
        var k = 1 - Math.Clamp(opacity, 0, 1);
        return $"#{(int)Math.Round(r * k):X2}{(int)Math.Round(g * k):X2}{(int)Math.Round(b * k):X2}";
    }

    private static double ScrimFor(string hex)
    {
        for (var opacity = 0.0; opacity <= 0.6; opacity += 0.02)
        {
            if (ContrastRatio("#FFFFFF", Darken(hex, opacity)) >= MinTextContrast)
            {
                return Math.Round(opacity, 2);
            }
        }

        return 0.6;
    }

    private static double RelativeLuminance(string hex)
    {
        var (r, g, b) = Parse(hex);
        return 0.2126 * Channel(r) + 0.7152 * Channel(g) + 0.0722 * Channel(b);

        static double Channel(int v)
        {
            var c = v / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
    }

    private static double Wrap(double hue) => (hue % 360 + 360) % 360;

    private static (int R, int G, int B) Parse(string hex)
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
