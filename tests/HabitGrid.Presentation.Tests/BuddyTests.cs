using FluentAssertions;
using HabitGrid.Core.Domain.Gamification;
using HabitGrid.Presentation.ViewModels;

namespace HabitGrid.Presentation.Tests;

public class BuddyTests
{
    [Theory]
    [InlineData(1, BuddyStage.Egg)]
    [InlineData(4, BuddyStage.Egg)]
    [InlineData(5, BuddyStage.Baby)]
    [InlineData(14, BuddyStage.Baby)]
    [InlineData(15, BuddyStage.Teen)]
    [InlineData(30, BuddyStage.Adult)]
    [InlineData(49, BuddyStage.Adult)]
    [InlineData(50, BuddyStage.Legend)]
    [InlineData(100, BuddyStage.Legend)]
    public void Stage_EvolvesAtLevels5_15_30_50(int level, BuddyStage expected) =>
        BuddyViewModel.StageFor(level).Should().Be(expected);

    [Theory]
    [InlineData("vi")]
    [InlineData("en")]
    public void EveryBadge_HasNameAndDescription_InBothLanguages(string language)
    {
        using var f = new AppFixture();
        f.Localizer.SetLanguage(language);

        foreach (var definition in AchievementCatalog.Default)
        {
            f.Localizer[$"badge.{definition.Id}.name"].Should().NotStartWith("[", $"thiếu tên huy hiệu {definition.Id} ({language})");
            f.Localizer[$"badge.{definition.Id}.desc"].Should().NotStartWith("[", $"thiếu mô tả huy hiệu {definition.Id} ({language})");
        }

        foreach (var stage in new[] { "egg", "baby", "teen", "adult", "legend" })
        {
            f.Localizer[$"stage.{stage}"].Should().NotStartWith("[");
        }
    }

    [Fact]
    public async Task Load_ShowsLevelXpFreezeAndHidesSecretBadges()
    {
        using var f = new AppFixture();
        var h = await f.AddAsync("A", startDaysAgo: 30);
        for (var i = 0; i < 8; i++) await f.LogAsync(h, AppFixture.Today.AddDays(-i), 1);
        var vm = new BuddyViewModel(f.NewGamification(), f.Profile, f.Achievements);

        await vm.LoadAsync();

        vm.Level.Should().BeGreaterThanOrEqualTo(1);
        vm.TotalXp.Should().BeGreaterThan(0);
        vm.Badges.Should().HaveCount(AchievementCatalog.Default.Count);
        vm.Badges.Single(b => b.Id == "streak_7").Unlocked.Should().BeTrue();
        vm.Badges.Where(b => b.Group == "secret" && !b.Unlocked).Should().OnlyContain(b => b.Hidden);
        vm.UnlockedCount.Should().Be(vm.Badges.Count(b => b.Unlocked));
        vm.ErrorMessage.Should().BeNull();
    }
}
