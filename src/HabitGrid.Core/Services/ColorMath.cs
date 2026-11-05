namespace HabitGrid.Core.Services;

/// <summary>
/// Tiện ích toán màu thuần (không phụ thuộc WPF/System.Drawing) dùng cho bộ sinh bảng màu heatmap.
/// </summary>
public static class ColorMath
{
    /// <summary>
    /// Phân tích chuỗi hex "#RRGGBB" hoặc "RRGGBB" thành thành phần R, G, B (0–255).
    /// </summary>
    /// <exception cref="FormatException">Khi chuỗi không hợp lệ.</exception>
    public static (byte R, byte G, byte B) ParseHex(string hex)
    {
        ArgumentNullException.ThrowIfNull(hex);

        var span = hex.AsSpan().Trim();
        if (span.Length > 0 && span[0] == '#')
        {
            span = span[1..];
        }

        if (span.Length != 6)
        {
            throw new FormatException($"Chuỗi màu không hợp lệ: '{hex}'. Định dạng mong đợi là #RRGGBB.");
        }

        const System.Globalization.NumberStyles HexFormat = System.Globalization.NumberStyles.HexNumber;

        if (!byte.TryParse(span[..2], HexFormat, null, out var r) ||
            !byte.TryParse(span[2..4], HexFormat, null, out var g) ||
            !byte.TryParse(span[4..6], HexFormat, null, out var b))
        {
            throw new FormatException($"Chuỗi màu không hợp lệ: '{hex}'. Định dạng mong đợi là #RRGGBB.");
        }

        return (r, g, b);
    }

    /// <summary>Chuyển thành phần màu 0–255 thành chuỗi "#RRGGBB".</summary>
    public static string ToHex(byte r, byte g, byte b) => $"#{r:X2}{g:X2}{b:X2}";

    /// <summary>Chuyển chuỗi hex thành HSL (H: 0–360, S/L: 0–1).</summary>
    public static (double H, double S, double L) ToHsl(byte r, byte g, byte b)
    {
        var rd = r / 255.0;
        var gd = g / 255.0;
        var bd = b / 255.0;

        var max = Math.Max(rd, Math.Max(gd, bd));
        var min = Math.Min(rd, Math.Min(gd, bd));
        var delta = max - min;
        var l = (max + min) / 2.0;

        double h = 0;
        double s = 0;

        if (delta > double.Epsilon)
        {
            s = l > 0.5 ? delta / (2.0 - max - min) : delta / (max + min);

            h = max switch
            {
                _ when Math.Abs(max - rd) < double.Epsilon => (gd - bd) / delta + (gd < bd ? 6.0 : 0.0),
                _ when Math.Abs(max - gd) < double.Epsilon => (bd - rd) / delta + 2.0,
                _ => (rd - gd) / delta + 4.0
            };

            h *= 60.0;
        }

        return (h, s, l);
    }

    /// <summary>Chuyển HSL (H: 0–360, S/L: 0–1) thành thành phần màu 0–255.</summary>
    public static (byte R, byte G, byte B) FromHsl(double h, double s, double l)
    {
        h = ((h % 360.0) + 360.0) % 360.0 / 360.0;
        s = Math.Clamp(s, 0.0, 1.0);
        l = Math.Clamp(l, 0.0, 1.0);

        if (s <= double.Epsilon)
        {
            var gray = (byte)Math.Round(l * 255.0);
            return (gray, gray, gray);
        }

        var q = l < 0.5 ? l * (1.0 + s) : l + s - (l * s);
        var p = (2.0 * l) - q;

        return (
            (byte)Math.Round(HueToRgb(p, q, h + (1.0 / 3.0)) * 255.0),
            (byte)Math.Round(HueToRgb(p, q, h) * 255.0),
            (byte)Math.Round(HueToRgb(p, q, h - (1.0 / 3.0)) * 255.0));
    }

    /// <summary>Độ sáng tương đối theo WCAG (0–1), dùng để kiểm tra tương phản.</summary>
    public static double RelativeLuminance(byte r, byte g, byte b)
    {
        static double Channel(double c) => c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);

        return (0.2126 * Channel(r / 255.0))
             + (0.7152 * Channel(g / 255.0))
             + (0.0722 * Channel(b / 255.0));
    }

    /// <summary>Độ sáng tương đối của một chuỗi hex.</summary>
    public static double RelativeLuminance(string hex)
    {
        var (r, g, b) = ParseHex(hex);
        return RelativeLuminance(r, g, b);
    }

    private static double HueToRgb(double p, double q, double t)
    {
        if (t < 0.0)
        {
            t += 1.0;
        }

        if (t > 1.0)
        {
            t -= 1.0;
        }

        if (t < 1.0 / 6.0)
        {
            return p + ((q - p) * 6.0 * t);
        }

        if (t < 1.0 / 2.0)
        {
            return q;
        }

        if (t < 2.0 / 3.0)
        {
            return p + ((q - p) * ((2.0 / 3.0) - t) * 6.0);
        }

        return p;
    }
}
