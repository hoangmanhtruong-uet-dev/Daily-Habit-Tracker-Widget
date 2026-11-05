namespace HabitGrid.Core.Domain.Moments;

/// <summary>Một ảnh khoảnh khắc gắn với check-in của một thói quen trong một ngày. Ảnh nằm trên máy người dùng.</summary>
public sealed record Moment(Guid HabitId, DateOnly Day, string Path, DateTime CreatedAtUtc);

/// <summary>Một khung thẻ chia sẻ kiểu photobooth.</summary>
/// <param name="Background">Hai màu gradient nền (#RRGGBB).</param>
/// <param name="Ink">Màu chữ trên khung.</param>
/// <param name="Panel">Màu ô bên trong (#AARRGGBB).</param>
public sealed record ShareFrame(string Id, string NameKey, string BackgroundStart, string BackgroundEnd, string Ink, string Panel, bool IsFree, string Sticker);

/// <summary>Danh mục khung thẻ chia sẻ. Free: 2 khung; Plus: tất cả (SPEC-V2-10 §1 "tắt watermark share card").</summary>
public static class ShareFrames
{
    public static ShareFrame Default { get; } = new("default", "frame.default", "#8B5CF6", "#EC4899", "#FFFFFF", "#33FFFFFF", true, "✨");

    public static IReadOnlyList<ShareFrame> All { get; } =
    [
        Default,
        new("candy", "frame.candy", "#FF9AC8", "#FFC6E0", "#5B1E3D", "#66FFFFFF", true, "🍬"),
        new("midnight", "frame.midnight", "#0B1026", "#2B1B5A", "#F1F0FF", "#1FFFFFFF", false, "🌙"),
        new("dew", "frame.dew", "#C8F1E6", "#DCEBFF", "#1D3B4A", "#80FFFFFF", false, "🌿"),
        new("dreamy", "frame.dreamy", "#B794F6", "#F3C4FB", "#2E1450", "#55FFFFFF", false, "💜"),
        new("orbit", "frame.orbit", "#05050A", "#1A3A5C", "#E8F7FF", "#1AFFFFFF", false, "🪐")
    ];

    public static ShareFrame Find(string? id) => All.FirstOrDefault(f => f.Id == id) ?? Default;

    public static bool CanUse(ShareFrame frame, bool isPlus) => isPlus || frame.IsFree;
}
