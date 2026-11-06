using FluentAssertions;
using HabitGrid.Core.Domain;
using HabitGrid.Core.Domain.Gamification;
using HabitGrid.Core.Domain.Monetization;
using HabitGrid.Presentation.Services;

namespace HabitGrid.Presentation.Tests;

public class BuddyAccessoryCatalogTests
{
    [Fact]
    public void Catalog_IsConsistent()
    {
        BuddyAccessories.All.Select(a => a.Id).Should().OnlyHaveUniqueItems();
        BuddyAccessories.All.Select(a => a.Slot).Distinct().Should().BeEquivalentTo(Enum.GetValues<AccessorySlot>());
        BuddyAccessories.All.Where(a => a.Unlock == AccessoryUnlockKind.Level).Should().OnlyContain(a => a.RequiredLevel > 0);
        BuddyAccessories.All.Where(a => a.Unlock == AccessoryUnlockKind.Plus).Should().HaveCountGreaterThan(2);
    }

    [Fact]
    public void EveryBadgeRequirement_IsARealBadge()
    {
        var real = AchievementCatalog.Default.Select(d => d.Id).ToHashSet();

        BuddyAccessories.All.Where(a => a.Unlock == AccessoryUnlockKind.Badge)
            .Should().OnlyContain(a => a.Requirement != null && real.Contains(a.Requirement));
    }

    [Theory]
    [InlineData(1, Season.Winter)]
    [InlineData(2, Season.Winter)]
    [InlineData(3, Season.Spring)]
    [InlineData(5, Season.Spring)]
    [InlineData(6, Season.Summer)]
    [InlineData(8, Season.Summer)]
    [InlineData(9, Season.Autumn)]
    [InlineData(11, Season.Autumn)]
    [InlineData(12, Season.Winter)]
    public void SeasonsFollowTheCalendarMonth(int month, Season expected)
    {
        Seasons.Of(new DateOnly(2026, month, 15)).Should().Be(expected);
        Seasons.Of(new DateOnly(2026, Seasons.FirstMonth(expected), 1)).Should().Be(expected);
    }

    [Fact]
    public void EverySeason_HasAHeadAndANeckItem()
    {
        foreach (var season in Enum.GetValues<Season>())
        {
            var items = BuddyAccessories.All.Where(a => a.Season == season).ToList();
            items.Select(a => a.Slot).Should().Contain([AccessorySlot.Head, AccessorySlot.Neck], $"mùa {season}");
        }

        BuddyAccessories.All.Where(a => a.Unlock == AccessoryUnlockKind.Seasonal).Should().OnlyContain(a => a.Season != null);
    }

    [Theory]
    [InlineData("vi")]
    [InlineData("en")]
    public void EveryAccessory_HasAName_InBothLanguages(string language)
    {
        using var f = new AppFixture();
        f.Localizer.SetLanguage(language);

        foreach (var accessory in BuddyAccessories.All)
        {
            f.Localizer["accessory." + accessory.Id].Should().NotStartWith("[", $"thiếu tên phụ kiện {accessory.Id} ({language})");
        }
    }
}

public class BuddyWardrobeServiceTests
{
    private static BuddyWardrobeService Create(AppFixture f, bool plus)
    {
        var live = new LiveEntitlements();
        live.Set(plus);
        return new BuddyWardrobeService(f.Settings, f.Profile, f.Achievements, live, f.Days);
    }

    private static async Task SetLevelAsync(AppFixture f, int level) =>
        await f.Profile.SaveAsync((await f.Profile.GetAsync()) with { Level = level });

    [Fact]
    public async Task LevelAndBadgeUnlocks_AreOwned_OthersAreLocked()
    {
        using var f = new AppFixture();
        await SetLevelAsync(f, 8);
        await f.Achievements.UnlockAsync("streak_7", DateTime.UtcNow);

        var items = (await Create(f, plus: false).GetAsync()).Items.ToDictionary(i => i.Accessory.Id);

        items["cap"].Owned.Should().BeTrue();
        items["shades"].Owned.Should().BeTrue("đúng cấp 8");
        items["medal"].Owned.Should().BeTrue("có huy hiệu streak_7");
        items["scarf"].Owned.Should().BeFalse("cần cấp 12");
        items["crown"].Owned.Should().BeFalse();
        items["tophat"].Owned.Should().BeFalse("món Plus");
    }

    [Fact]
    public async Task Equip_ReplacesTheSameSlot_AndTogglesOff()
    {
        using var f = new AppFixture();
        await SetLevelAsync(f, 20);
        var service = Create(f, plus: false);

        (await service.ToggleAsync("cap")).Should().Be(EquipResult.Equipped);
        (await service.ToggleAsync("grad")).Should().Be(EquipResult.Equipped);
        (await service.GetAsync()).Worn[AccessorySlot.Head].Id.Should().Be("grad");

        (await service.ToggleAsync("shades")).Should().Be(EquipResult.Equipped);
        (await service.GetAsync()).Worn.Should().HaveCount(2, "mỗi vị trí một món");

        (await service.ToggleAsync("grad")).Should().Be(EquipResult.Removed);
        (await service.GetAsync()).Worn.Should().NotContainKey(AccessorySlot.Head);
    }

    [Fact]
    public async Task LockedAndUnknownItems_CannotBeEquipped()
    {
        using var f = new AppFixture();
        var service = Create(f, plus: false);

        (await service.ToggleAsync("crown")).Should().Be(EquipResult.Locked);
        (await service.ToggleAsync("tophat")).Should().Be(EquipResult.Locked);
        (await service.ToggleAsync("nope")).Should().Be(EquipResult.Unknown);
        (await service.GetAsync()).Worn.Should().BeEmpty();
    }

    [Fact]
    public async Task PlusItem_StopsShowingWhenPlusEnds_ButComesBackWhenItReturns()
    {
        using var f = new AppFixture();
        (await Create(f, plus: true).ToggleAsync("tophat")).Should().Be(EquipResult.Equipped);

        (await Create(f, plus: true).GetAsync()).Worn.Should().ContainKey(AccessorySlot.Head);
        (await Create(f, plus: false).GetAsync()).Worn.Should().BeEmpty("hết Plus thì không hiển thị món Plus");
        (await Create(f, plus: true).GetAsync()).Worn[AccessorySlot.Head].Id.Should().Be("tophat", "lựa chọn được giữ lại");
    }

    [Fact]
    public async Task CorruptedStorage_MeansNothingWorn()
    {
        using var f = new AppFixture();
        await f.Settings.SetAsync(BuddyWardrobeService.Key, "{{{");

        (await Create(f, plus: true).GetAsync()).Worn.Should().BeEmpty();
    }

    [Fact]
    public async Task SeasonalItems_AreCollectedDuringTheirSeason_AndKeptAfterwards()
    {
        using var f = new AppFixture(); // 22/10/2026 = mùa thu
        var service = Create(f, plus: true);

        var items = (await service.GetAsync()).Items.ToDictionary(i => i.Accessory.Id);
        items["maple"].Owned.Should().BeTrue("đang mùa thu");
        items["santa"].Owned.Should().BeFalse("chưa tới mùa đông");

        f.Days.Today = new DateOnly(2026, 12, 5);
        items = (await service.GetAsync()).Items.ToDictionary(i => i.Accessory.Id);
        items["santa"].Owned.Should().BeTrue();
        items["maple"].Owned.Should().BeTrue("món đã sưu tập được giữ sau khi mùa qua");
        items["tulip"].Owned.Should().BeFalse("chưa từng có mặt trong mùa xuân");
    }

    [Fact]
    public async Task FreeCannotCollectSeasonalItems_AndLosingPlusHidesThem()
    {
        using var f = new AppFixture();
        (await Create(f, plus: false).ToggleAsync("maple")).Should().Be(EquipResult.Locked);

        (await Create(f, plus: true).ToggleAsync("maple")).Should().Be(EquipResult.Equipped);
        (await Create(f, plus: false).GetAsync()).Worn.Should().BeEmpty();
        (await Create(f, plus: true).GetAsync()).Worn[AccessorySlot.Head].Id.Should().Be("maple");
    }

    [Fact]
    public async Task FreeBrowsingDoesNotCollectAnything()
    {
        using var f = new AppFixture();
        await Create(f, plus: false).GetAsync();
        f.Days.Today = new DateOnly(2026, 12, 5);

        (await Create(f, plus: true).GetAsync()).Items.Single(i => i.Accessory.Id == "maple").Owned.Should().BeFalse("Free không sưu tập được lúc mùa thu");
    }
}
