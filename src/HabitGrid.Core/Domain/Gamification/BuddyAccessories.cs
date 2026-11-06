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
    Plus,

    /// <summary>Đồ theo mùa (Plus): nhận được trong mùa của nó rồi giữ luôn khi còn Plus.</summary>
    Seasonal
}

public enum Season
{
    Spring,
    Summer,
    Autumn,
    Winter
}

/// <summary>Mùa theo tháng dương lịch: xuân 3–5, hè 6–8, thu 9–11, đông 12–2 (cố định, không phụ thuộc vị trí).</summary>
public static class Seasons
{
    public static Season Of(DateOnly day) => day.Month switch
    {
        >= 3 and <= 5 => Season.Spring,
        >= 6 and <= 8 => Season.Summer,
        >= 9 and <= 11 => Season.Autumn,
        _ => Season.Winter
    };

    /// <summary>Tháng đầu tiên của mùa.</summary>
    public static int FirstMonth(Season season) => season switch
    {
        Season.Spring => 3,
        Season.Summer => 6,
        Season.Autumn => 9,
        _ => 12
    };
}

/// <summary>Mọi thứ cần biết để quyết định người dùng có món nào.</summary>
public sealed record AccessoryContext(int Level, IReadOnlySet<string> Badges, bool IsPlus, DateOnly Today, IReadOnlySet<string> Collected);

/// <param name="Requirement">Cấp độ cần đạt (Level) hoặc id huy hiệu (Badge); bỏ trống với Plus.</param>
public sealed record BuddyAccessory(string Id, AccessorySlot Slot, string Emoji, AccessoryUnlockKind Unlock, string? Requirement = null, Season? Season = null)
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
        new("gem", AccessorySlot.Neck, "💎", AccessoryUnlockKind.Plus),

        new("tulip", AccessorySlot.Head, "🌷", AccessoryUnlockKind.Seasonal, Season: Gamification.Season.Spring),
        new("butterfly", AccessorySlot.Neck, "🦋", AccessoryUnlockKind.Seasonal, Season: Gamification.Season.Spring),
        new("straw", AccessorySlot.Head, "👒", AccessoryUnlockKind.Seasonal, Season: Gamification.Season.Summer),
        new("lei", AccessorySlot.Neck, "🌺", AccessoryUnlockKind.Seasonal, Season: Gamification.Season.Summer),
        new("maple", AccessorySlot.Head, "🍁", AccessoryUnlockKind.Seasonal, Season: Gamification.Season.Autumn),
        new("pumpkin", AccessorySlot.Neck, "🎃", AccessoryUnlockKind.Seasonal, Season: Gamification.Season.Autumn),
        new("santa", AccessorySlot.Head, "🎅", AccessoryUnlockKind.Seasonal, Season: Gamification.Season.Winter),
        new("snowflake", AccessorySlot.Neck, "❄️", AccessoryUnlockKind.Seasonal, Season: Gamification.Season.Winter)
    ];

    public static BuddyAccessory? Find(string id) => All.FirstOrDefault(a => a.Id == id);

    public static bool IsOwned(BuddyAccessory accessory, AccessoryContext context) => accessory.Unlock switch
    {
        AccessoryUnlockKind.Level => context.Level >= accessory.RequiredLevel,
        AccessoryUnlockKind.Badge => accessory.Requirement is { } id && context.Badges.Contains(id),
        AccessoryUnlockKind.Plus => context.IsPlus,
        _ => context.IsPlus && (context.Collected.Contains(accessory.Id) || InSeason(accessory, context.Today))
    };

    /// <summary>Món theo mùa đang nhận được (mùa của nó là mùa hiện tại).</summary>
    public static bool InSeason(BuddyAccessory accessory, DateOnly today) =>
        accessory.Season is { } season && Seasons.Of(today) == season;

    /// <summary>Phụ kiện đang đeo thật sự hiển thị: bỏ những món không còn sở hữu (hết Plus), nhưng không xóa lựa chọn đã lưu.</summary>
    public static IReadOnlyDictionary<AccessorySlot, BuddyAccessory> Worn(IReadOnlyDictionary<AccessorySlot, string> equipped, AccessoryContext context)
    {
        var worn = new Dictionary<AccessorySlot, BuddyAccessory>();

        foreach (var (slot, id) in equipped)
        {
            if (Find(id) is { } accessory && accessory.Slot == slot && IsOwned(accessory, context))
            {
                worn[slot] = accessory;
            }
        }

        return worn;
    }
}
