using FluentAssertions;
using HabitGrid.Core.Domain;
using HabitGrid.Core.Domain.Gamification;
using HabitGrid.Presentation.Services;

namespace HabitGrid.Presentation.Tests;

public class GamificationServiceTests
{
    private static readonly DateOnly Today = AppFixture.Today;

    [Fact]
    public async Task FirstRun_OnExistingData_RecordsEverythingButCelebratesNothing()
    {
        using var f = new AppFixture();
        var h = await f.AddAsync("A", startDaysAgo: 40);
        for (var i = 0; i < 10; i++) await f.LogAsync(h, Today.AddDays(-i), 1);
        var service = f.NewGamification();
        var celebrated = 0;
        service.Celebrated += (_, _) => celebrated++;

        var snapshot = await service.RefreshAsync();

        snapshot.NewlyUnlocked.Should().BeEmpty();
        snapshot.LeveledUpTo.Should().BeNull();
        celebrated.Should().Be(0);
        (await f.Achievements.GetAllAsync()).Should().Contain(a => a.Id == "streak_7");
        (await f.Profile.GetAsync()).Xp.Should().Be(snapshot.Xp.TotalXp).And.BeGreaterThan(0);
    }

    [Fact]
    public async Task AfterFirstRun_ANewCheckIn_UnlocksAndCelebratesOnce()
    {
        using var f = new AppFixture();
        var h = await f.AddAsync("A", startDaysAgo: 40);
        var service = f.NewGamification();
        await service.RefreshAsync();
        GamificationSnapshot? seen = null;
        service.Celebrated += (_, s) => seen = s;

        await f.LogAsync(h, Today, 1);
        var snapshot = await service.RefreshAsync();
        var again = await service.RefreshAsync();

        snapshot.NewlyUnlocked.Should().Contain("first_checkin");
        seen.Should().NotBeNull();
        again.NewlyUnlocked.Should().BeEmpty("huy hiệu đã mở thì không báo lại");
        (await f.Profile.GetAsync()).Xp.Should().Be(snapshot.Xp.TotalXp);
    }

    [Fact]
    public async Task Unlocked_StaysUnlocked_EvenIfTheDataLaterChanges()
    {
        using var f = new AppFixture();
        var h = await f.AddAsync("A", startDaysAgo: 40);
        var service = f.NewGamification();
        await service.RefreshAsync();
        await f.LogAsync(h, Today, 1);
        await service.RefreshAsync();

        await f.Logs.DeleteAsync(h.Id, Today);
        var snapshot = await service.RefreshAsync();

        snapshot.Achievements.Single(a => a.Id == "first_checkin").Unlocked.Should().BeFalse("điều kiện hiện tại không còn");
        (await f.Achievements.GetAllAsync()).Should().Contain(a => a.Id == "first_checkin", "nhưng huy hiệu đã mở được giữ lại");
        (await f.Profile.GetAsync()).Xp.Should().Be(0, "XP thu hồi khi bỏ check");
    }

    [Fact]
    public async Task SevenDayOverallStreak_EarnsOneFreezeToken_OnlyOnce()
    {
        using var f = new AppFixture();
        var h = await f.AddAsync("A", startDaysAgo: 40);
        var service = f.NewGamification();
        await service.RefreshAsync();

        for (var i = 0; i < 7; i++) await f.LogAsync(h, Today.AddDays(-i), 1);
        var first = await service.RefreshAsync();
        var second = await service.RefreshAsync();

        first.OverallStreak.Should().Be(7);
        first.FreezeTokens.Should().BeGreaterThanOrEqualTo(1);
        second.FreezeTokens.Should().Be(first.FreezeTokens);
    }

    [Fact]
    public async Task TokenCap_DependsOnPlan()
    {
        using var f = new AppFixture();
        await f.Profile.SaveAsync((await f.Profile.GetAsync()) with { Xp = 1, FreezeTokens = 3 });
        var h = await f.AddAsync("A", startDaysAgo: 60);
        var service = f.NewGamification();
        await service.RefreshAsync();

        for (var i = 0; i < 21; i++) await f.LogAsync(h, Today.AddDays(-i), 1);
        var snapshot = await service.RefreshAsync();

        snapshot.FreezeTokens.Should().Be(FreezeEngine.FreeCap);
    }

    [Fact]
    public async Task Freeze_Auto_FillsTheMissedDay_ForEveryScheduledHabit_AndSpendsOneToken()
    {
        using var f = new AppFixture();
        var a = await f.AddAsync("A", startDaysAgo: 30);
        var b = await f.AddAsync("B", startDaysAgo: 30);
        foreach (var d in new[] { 2, 3, 4 }) await f.LogAsync(a, Today.AddDays(-d), 1);
        await f.Profile.SaveAsync((await f.Profile.GetAsync()) with { FreezeTokens = 2 });
        var service = f.NewGamification();

        var plan = await service.CheckFreezeAsync();

        plan.Days.Should().Equal(Today.AddDays(-1));
        (await f.Logs.GetAsync(a.Id, Today.AddDays(-1)))!.Status.Should().Be(DayStatus.Frozen);
        (await f.Logs.GetAsync(b.Id, Today.AddDays(-1)))!.Status.Should().Be(DayStatus.Frozen);
        (await f.Profile.GetAsync()).FreezeTokens.Should().Be(1, "một token cho một ngày, không phải mỗi thói quen");
    }

    [Fact]
    public async Task Freeze_WhenAutoIsOff_ReturnsAPlanWithoutApplyingIt_UntilConfirmed()
    {
        using var f = new AppFixture();
        var a = await f.AddAsync("A", startDaysAgo: 30);
        foreach (var d in new[] { 2, 3 }) await f.LogAsync(a, Today.AddDays(-d), 1);
        await f.Profile.SaveAsync((await f.Profile.GetAsync()) with { FreezeTokens = 1 });
        var service = f.NewGamification();
        await service.SetAutoFreezeAsync(false);

        var plan = await service.CheckFreezeAsync();

        plan.NeedsConfirmation.Should().BeTrue();
        (await f.Logs.GetAsync(a.Id, Today.AddDays(-1))).Should().BeNull();
        (await f.Profile.GetAsync()).FreezeTokens.Should().Be(1);

        await service.ApplyFreezeAsync(plan);

        (await f.Logs.GetAsync(a.Id, Today.AddDays(-1)))!.Status.Should().Be(DayStatus.Frozen);
        (await f.Profile.GetAsync()).FreezeTokens.Should().Be(0);
    }

    [Fact]
    public async Task Freeze_WithoutEnoughTokens_DoesNothing()
    {
        using var f = new AppFixture();
        var a = await f.AddAsync("A", startDaysAgo: 30);
        foreach (var d in new[] { 3, 4 }) await f.LogAsync(a, Today.AddDays(-d), 1);
        await f.Profile.SaveAsync((await f.Profile.GetAsync()) with { FreezeTokens = 1 });
        var service = f.NewGamification();

        var plan = await service.CheckFreezeAsync();

        plan.HasPlan.Should().BeFalse();
        (await f.Profile.GetAsync()).FreezeTokens.Should().Be(1);
    }

    [Fact]
    public async Task DiscoveryFlags_FeedTheirBadges()
    {
        using var f = new AppFixture();
        var service = f.NewGamification();
        await service.RefreshAsync();

        await service.SetFlagAsync("theme_changed");
        var snapshot = await service.RefreshAsync();

        snapshot.NewlyUnlocked.Should().Contain("theme_changed");
    }

    [Fact]
    public void OverallStreak_BridgesFrozenDays_ButDoesNotCountThem()
    {
        var h = Guid.NewGuid();
        var logs = new List<DayLog>
        {
            new() { HabitId = h, Day = Today, Status = DayStatus.Done, Value = 1 },
            new() { HabitId = h, Day = Today.AddDays(-1), Status = DayStatus.Frozen },
            new() { HabitId = h, Day = Today.AddDays(-2), Status = DayStatus.Done, Value = 1 }
        };

        GamificationService.OverallStreak(logs, Today).Should().Be(2);
    }
}
