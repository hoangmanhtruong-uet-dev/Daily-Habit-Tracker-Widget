namespace HabitGrid.Core.Services;

/// <summary>
/// Bảng 5 mức màu của lưới heatmap: ô rỗng + 4 mức độ đậm nhạt.
/// </summary>
/// <param name="Level0Empty">Màu của ô rỗng, thích ứng theo theme Sáng/Tối.</param>
/// <param name="Level1">Mức nhạt nhất.</param>
/// <param name="Level2">Mức nhạt vừa.</param>
/// <param name="Level3">Mức đậm vừa.</param>
/// <param name="Level4">Mức đậm nhất.</param>
public record HeatmapShades(
    string Level0Empty,
    string Level1,
    string Level2,
    string Level3,
    string Level4);

/// <summary>
/// Sinh bảng màu heatmap từ màu gốc hoặc từ bảng màu preset.
/// </summary>
public interface IPaletteGenerator
{
    /// <summary>Sinh 5 mức màu từ một màu gốc #RRGGBB.</summary>
    HeatmapShades GenerateFromHex(string baseHexColor, bool isDarkTheme);

    /// <summary>Lấy bảng màu theo preset (ví dụ "default", "blue", "purple", ...).</summary>
    HeatmapShades GetPreset(string presetId, bool isDarkTheme);
}
