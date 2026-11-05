namespace HabitGrid.Presentation.Theming;

public enum ThemeMode
{
    System,
    Dark,
    Light
}

/// <summary>
/// Lớp token NGỮ NGHĨA của một theme (03-UI-REBUILD-GLOW2 §8). Thành phần giao diện chỉ dùng các token này,
/// không dùng mã màu trực tiếp. Màu dạng <c>#RRGGBB</c> hoặc <c>#AARRGGBB</c>.
/// </summary>
public sealed record ThemePalette(
    bool IsDark,
    string Canvas,
    IReadOnlyList<string> Blobs,
    double BlobOpacity,
    string Surface,
    string SurfaceStrong,
    string Stroke,
    string StrokeHighlight,
    string TextPrimary,
    string TextSecondary,
    string TextMuted,
    string AccentStart,
    string AccentEnd,
    string CardIdle,
    string HeatEmpty,
    string Success,
    string Warning,
    string Freeze,
    string Rail,
    string RailSelectedText);

/// <summary>Một theme có thể có bản tối, bản sáng, hoặc cả hai.</summary>
public sealed record ThemeDefinition(string Id, string NameKey, ThemePalette? Dark, ThemePalette? Light)
{
    /// <summary>Bảng màu cho chế độ yêu cầu; theme chỉ có một chế độ thì luôn dùng chế độ đó.</summary>
    public ThemePalette Resolve(ThemeMode mode, bool systemIsDark)
    {
        var wantDark = mode switch
        {
            ThemeMode.Dark => true,
            ThemeMode.Light => false,
            _ => systemIsDark
        };

        return (wantDark ? Dark ?? Light : Light ?? Dark)
               ?? throw new InvalidOperationException($"Theme '{Id}' không có bảng màu nào.");
    }
}

public static class Themes
{
    public const string CandyBentoId = "candy-bento";
    public const string NeonNightId = "neon-night";
    public const string SoftPastelId = "soft-pastel";

    /// <summary>A. Candy Bento — mặc định: nền tối sâu có đốm màu trôi, thẻ gradient rực.</summary>
    public static ThemeDefinition CandyBento { get; } = new(
        CandyBentoId,
        "theme.candy",
        Dark: new ThemePalette(
            IsDark: true,
            Canvas: "#0E0E14",
            Blobs: ["#8B5CF6", "#EC4899", "#06B6D4"],
            BlobOpacity: 0.32,
            Surface: "#14FFFFFF",
            SurfaceStrong: "#1F1F2B",
            Stroke: "#1AFFFFFF",
            StrokeHighlight: "#26FFFFFF",
            TextPrimary: "#F7F7FB",
            TextSecondary: "#B4B4C6",
            TextMuted: "#7C7C92",
            AccentStart: "#8B5CF6",
            AccentEnd: "#EC4899",
            CardIdle: "#12FFFFFF",
            HeatEmpty: "#1FFFFFFF",
            Success: "#22C55E",
            Warning: "#F59E0B",
            Freeze: "#38BDF8",
            Rail: "#0AFFFFFF",
            RailSelectedText: "#FFFFFF"),
        Light: new ThemePalette(
            IsDark: false,
            Canvas: "#F6F4FB",
            Blobs: ["#C4B5FD", "#FBCFE8", "#A5F3FC"],
            BlobOpacity: 0.55,
            Surface: "#B3FFFFFF",
            SurfaceStrong: "#FFFFFF",
            Stroke: "#14000000",
            StrokeHighlight: "#FFFFFFFF",
            TextPrimary: "#17142B",
            TextSecondary: "#56526E",
            TextMuted: "#8C88A3",
            AccentStart: "#7C3AED",
            AccentEnd: "#DB2777",
            CardIdle: "#CCFFFFFF",
            HeatEmpty: "#E7E3F2",
            Success: "#16A34A",
            Warning: "#D97706",
            Freeze: "#0EA5E9",
            Rail: "#80FFFFFF",
            RailSelectedText: "#FFFFFF"));

    /// <summary>B. Neon Night — đen tuyền, viền neon.</summary>
    public static ThemeDefinition NeonNight { get; } = new(
        NeonNightId,
        "theme.neon",
        Dark: new ThemePalette(
            IsDark: true,
            Canvas: "#05050A",
            Blobs: ["#22D3EE", "#A855F7", "#F0ABFC"],
            BlobOpacity: 0.18,
            Surface: "#0DFFFFFF",
            SurfaceStrong: "#12121C",
            Stroke: "#3322D3EE",
            StrokeHighlight: "#5522D3EE",
            TextPrimary: "#E6FBFF",
            TextSecondary: "#9AB8C2",
            TextMuted: "#5F7A85",
            AccentStart: "#22D3EE",
            AccentEnd: "#A855F7",
            CardIdle: "#0AFFFFFF",
            HeatEmpty: "#18FFFFFF",
            Success: "#4ADE80",
            Warning: "#FBBF24",
            Freeze: "#67E8F9",
            Rail: "#08FFFFFF",
            RailSelectedText: "#05050A"),
        Light: null);

    /// <summary>C. Soft Pastel — nền kem, thẻ pastel kiểu sticker.</summary>
    public static ThemeDefinition SoftPastel { get; } = new(
        SoftPastelId,
        "theme.pastel",
        Dark: null,
        Light: new ThemePalette(
            IsDark: false,
            Canvas: "#FBF7F2",
            Blobs: ["#FDE68A", "#FBCFE8", "#BBF7D0"],
            BlobOpacity: 0.6,
            Surface: "#E6FFFFFF",
            SurfaceStrong: "#FFFFFF",
            Stroke: "#33241F1A",
            StrokeHighlight: "#FFFFFFFF",
            TextPrimary: "#2A2420",
            TextSecondary: "#6B5F57",
            TextMuted: "#A0948B",
            AccentStart: "#F472B6",
            AccentEnd: "#FB923C",
            CardIdle: "#F2FFFFFF",
            HeatEmpty: "#EFE7DD",
            Success: "#16A34A",
            Warning: "#D97706",
            Freeze: "#0EA5E9",
            Rail: "#99FFFFFF",
            RailSelectedText: "#FFFFFF"));

    public static IReadOnlyList<ThemeDefinition> All { get; } = [CandyBento, NeonNight, SoftPastel];

    public static ThemeDefinition Find(string? id) =>
        All.FirstOrDefault(t => t.Id == id) ?? CandyBento;
}
