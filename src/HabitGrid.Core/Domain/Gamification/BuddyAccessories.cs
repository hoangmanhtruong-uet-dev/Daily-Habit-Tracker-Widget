namespace HabitGrid.Core.Domain.Gamification;

public enum AccessorySlot
{
    Head,
    Face,
    Neck
}

public enum AccessoryUnlockKind
{
    /// <summary>Mở khi đạt cấp độ.</summary>
    Level,

    /// <summary>Mở khi nhận một huy hiệu.</summary>
    Badge,

    /// <summary>Chỉ dành cho Plus.</summary>
    Plus
}

/// <param name="Requirement">Cấp độ cần đạt (Level) hoặc id huy hiệu (Badge); bỏ trống với Plus.</param>
public sealed record BuddyAccessory(string Id, AccessorySlot Slot, string Emoji, AccessoryUnlockKind Unlock, string? Requirement = null)
{
    public int RequiredLevel => Unlock == AccessoryUnlockKind.Level && int.TryParse(Requirement, out var level) ? level : 0;
}

/// <summary>
/// Phụ kiện cho Buddy (P6). Free mở bằng cấp độ hoặc huy hiệu; Plus có thêm các món riêng. Mỗi vị trí (đầu/mặt/cổ) đeo một món.
/// Hình hiện là emoji ghép lên linh vật; có hình vẽ riêng thì đổi <see cref="BuddyAccessory.Emoji"/> mà không đụng logic.
/// </summary>
public static class BuddyAccessories
{
    public static readonly IReadOnlyList<BuddyAccessory> All =
    [
        new("cap", AccessorySlot.Head, "🧢", AccessoryUnlockKind.Level, "3"),
        new("party", AccessorySlot.Head, "🥳", AccessoryUnlockKind.Badge, "first_perfect_day"),
        new("grad", AccessorySlot.Head, "🎓", AccessoryUnlockKind.Level, "15"),
        new("crown", AccessorySlot.Head, "👑", AccessoryUnlockKind.Badge, "streak_30"),
        new("tophat", AccessorySlot.Head, "🎩", AccessoryUnlockKind.Plus),
        new("blossom", AccessorySlot.Head, "🌸", AccessoryUnlockKind.Plus),

        new("shades", AccessorySlot.Face, "🕶️", AccessoryUnlockKind.Level, "8"),
        new("nerd", AccessorySlot.Face, "🤓", AccessoryUnlockKind.Badge, "checkins_100"),
        new("monocle", AccessorySlot.Face, "🧐", AccessoryUnlockKind.Plus),

        new("scarf", AccessorySlot.Neck, "🧣", AccessoryUnlockKind.Level, "12"),
        new("medal", AccessorySlot.Neck, "🏅", AccessoryUnlockKind.Badge, "streak_7"),
        new("ribbon", AccessorySlot.Neck, "🎀", AccessoryUnlockKind.Plus),
        new("gem", AccessorySlot.Neck, "💎", AccessoryUnlockKind.Plus)
    ];

    public static BuddyAccessory? Find(string id) => All.FirstOrDefault(a => a.Id == id);

    public static bool IsOwned(BuddyAccessory accessory, int level, IReadOnlySet<string> badges, bool isPlus) => accessory.Unlock switch
    {
        AccessoryUnlockKind.Level => level >= accessory.RequiredLevel,
        AccessoryUnlockKind.Badge => accessory.Requirement is { } id && badges.Contains(id),
        _ => isPlus
    };

    /// <summary>Phụ kiện đang đeo thật sự hiển thị: bỏ những món không còn sở hữu (hết Plus), nhưng không xóa lựa chọn đã lưu.</summary>
    public static IReadOnlyDictionary<AccessorySlot, BuddyAccessory> Worn(
        IReadOnlyDictionary<AccessorySlot, string> equipped, int level, IReadOnlySet<string> badges, bool isPlus)
    {
        var worn = new Dictionary<AccessorySlot, BuddyAccessory>();

        foreach (var (slot, id) in equipped)
        {
            if (Find(id) is { } accessory && accessory.Slot == slot && IsOwned(accessory, level, badges, isPlus))
            {
                worn[slot] = accessory;
            }
        }

        return worn;
    }
}
