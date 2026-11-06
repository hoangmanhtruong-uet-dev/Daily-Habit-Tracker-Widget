using System.Text.Json;
using HabitGrid.Core.Domain;
using HabitGrid.Core.Domain.Gamification;

namespace HabitGrid.Presentation.Services;

public enum EquipResult
{
    Equipped,
    Removed,
    Locked,
    Unknown
}

/// <summary>Một món trong tủ đồ của Buddy: đã sở hữu chưa, đang đeo không.</summary>
public sealed record WardrobeItem(BuddyAccessory Accessory, bool Owned, bool Equipped);

public sealed record Wardrobe(IReadOnlyList<WardrobeItem> Items, IReadOnlyDictionary<AccessorySlot, BuddyAccessory> Worn);

/// <summary>Tủ đồ Buddy (P6): đeo/tháo phụ kiện theo vị trí. Lựa chọn lưu trong settings (JSON), giữ nguyên khi hết Plus (món Plus chỉ ngừng hiển thị).</summary>
public sealed class BuddyWardrobeService
{
    public const string Key = "buddy.accessories";

    private readonly ISettingsStore _settings;
    private readonly IProfileRepository _profile;
    private readonly IAchievementRepository _achievements;
    private readonly IEntitlements _entitlements;

    public BuddyWardrobeService(ISettingsStore settings, IProfileRepository profile, IAchievementRepository achievements, IEntitlements entitlements)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        _achievements = achievements ?? throw new ArgumentNullException(nameof(achievements));
        _entitlements = entitlements ?? throw new ArgumentNullException(nameof(entitlements));
    }

    public async Task<Wardrobe> GetAsync(CancellationToken ct = default)
    {
        var (level, badges) = await StateAsync(ct);
        var equipped = await LoadAsync(ct);
        var worn = BuddyAccessories.Worn(equipped, level, badges, _entitlements.IsPlus);
        var items = BuddyAccessories.All
            .Select(a => new WardrobeItem(a, BuddyAccessories.IsOwned(a, level, badges, _entitlements.IsPlus), worn.TryGetValue(a.Slot, out var w) && w.Id == a.Id))
            .ToList();
        return new Wardrobe(items, worn);
    }

    /// <summary>Đeo món chưa đeo (thay món cũ cùng vị trí), hoặc tháo nếu đang đeo đúng món đó.</summary>
    public async Task<EquipResult> ToggleAsync(string accessoryId, CancellationToken ct = default)
    {
        if (BuddyAccessories.Find(accessoryId) is not { } accessory)
        {
            return EquipResult.Unknown;
        }

        var (level, badges) = await StateAsync(ct);

        if (!BuddyAccessories.IsOwned(accessory, level, badges, _entitlements.IsPlus))
        {
            return EquipResult.Locked;
        }

        var equipped = await LoadAsync(ct);
        var removing = equipped.TryGetValue(accessory.Slot, out var current) && current == accessory.Id;

        if (removing)
        {
            equipped.Remove(accessory.Slot);
        }
        else
        {
            equipped[accessory.Slot] = accessory.Id;
        }

        await _settings.SetAsync(Key, JsonSerializer.Serialize(equipped.ToDictionary(p => p.Key.ToString(), p => p.Value)), ct);
        return removing ? EquipResult.Removed : EquipResult.Equipped;
    }

    private async Task<(int Level, IReadOnlySet<string> Badges)> StateAsync(CancellationToken ct)
    {
        var profile = await _profile.GetAsync(ct);
        var badges = (await _achievements.GetAllAsync(ct)).Select(a => a.Id).ToHashSet();
        return (profile.Level, badges);
    }

    private async Task<Dictionary<AccessorySlot, string>> LoadAsync(CancellationToken ct)
    {
        var text = await _settings.GetAsync(Key, ct);
        var result = new Dictionary<AccessorySlot, string>();

        if (string.IsNullOrWhiteSpace(text))
        {
            return result;
        }

        try
        {
            foreach (var (slot, id) in JsonSerializer.Deserialize<Dictionary<string, string>>(text) ?? [])
            {
                if (Enum.TryParse<AccessorySlot>(slot, out var parsed))
                {
                    result[parsed] = id;
                }
            }
        }
        catch (JsonException)
        {
            // Dữ liệu hỏng: coi như chưa đeo gì.
        }

        return result;
    }
}
