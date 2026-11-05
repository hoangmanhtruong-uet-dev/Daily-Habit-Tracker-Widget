using FluentAssertions;
using HabitGrid.Core.Domain;
using HabitGrid.Core.Domain.Monetization;
using Xunit;
using static HabitGrid.Core.Tests.Domain.DomainTestData;

namespace HabitGrid.Core.Tests.Monetization;

public class LicensePolicyTests
{
    private static readonly DateTime Now = new(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void NoState_IsFree() =>
        LicensePolicy.Evaluate(null, Now).IsPlus.Should().BeFalse();

    [Theory]
    [InlineData(0, true, false)]
    [InlineData(6, true, false)]
    [InlineData(8, true, true)]
    [InlineData(14, true, true)]
    [InlineData(15, false, true)]
    public void Subscription_Offline_FollowsCacheAndGrace(int daysOffline, bool plus, bool recheck)
    {
        var state = new LicenseState(PlusTier.Monthly, Now.AddDays(-daysOffline), Now.AddDays(30));

        var status = LicensePolicy.Evaluate(state, Now);

        status.IsPlus.Should().Be(plus);
        status.NeedsRecheck.Should().Be(recheck);
    }

    [Fact]
    public void Subscription_PastItsKnownExpiry_EndsImmediately()
    {
        var state = new LicenseState(PlusTier.Yearly, Now.AddDays(-1), Now.AddHours(-1));

        LicensePolicy.Evaluate(state, Now).IsPlus.Should().BeFalse();
    }

    [Theory]
    [InlineData(PlusTier.Lifetime)]
    [InlineData(PlusTier.LegacyPro)]
    public void Durable_StaysPlusOffline_AndOnlyAsksToRecheckAfter30Days(PlusTier tier)
    {
        LicensePolicy.Evaluate(new LicenseState(tier, Now.AddDays(-10)), Now).Should().Match<LicenseStatus>(s => s.IsPlus && !s.NeedsRecheck);
        LicensePolicy.Evaluate(new LicenseState(tier, Now.AddDays(-400)), Now).Should().Match<LicenseStatus>(s => s.IsPlus && s.NeedsRecheck);
    }

    [Fact]
    public void FromOwned_LegacyProV1_BecomesPlus()
    {
        var state = LicensePolicy.FromOwned([new OwnedAddOn(PlusProducts.LegacyPro, true)], Now);

        state.Tier.Should().Be(PlusTier.LegacyPro);
        LicensePolicy.Evaluate(state, Now).IsPlus.Should().BeTrue();
    }

    [Fact]
    public void FromOwned_PrefersDurableOverSubscription_AndIgnoresInactive()
    {
        var owned = new[]
        {
            new OwnedAddOn(PlusProducts.Monthly, true, Now.AddDays(20)),
            new OwnedAddOn(PlusProducts.Lifetime, true),
            new OwnedAddOn(PlusProducts.Yearly, false, Now.AddDays(200))
        };

        LicensePolicy.FromOwned(owned, Now).Tier.Should().Be(PlusTier.Lifetime);
        LicensePolicy.FromOwned([new OwnedAddOn(PlusProducts.Yearly, false)], Now).Tier.Should().Be(PlusTier.None);
        LicensePolicy.FromOwned([new OwnedAddOn("something_else", true)], Now).Tier.Should().Be(PlusTier.None);
    }

    [Fact]
    public void FromOwned_KeepsTrialFlagAndExpiry()
    {
        var state = LicensePolicy.FromOwned([new OwnedAddOn(PlusProducts.Yearly, true, Now.AddDays(7), IsTrial: true)], Now);

        state.IsTrial.Should().BeTrue();
        state.ExpiresAtUtc.Should().Be(Now.AddDays(7));
    }
}

public class EntitlementsTests
{
    [Fact]
    public void Live_SwitchesBetweenFreeAndPlus_AndRaisesChanged()
    {
        var live = new LiveEntitlements();
        var raised = 0;
        live.Changed += (_, _) => raised++;

        live.MaxActiveHabits.Should().Be(FreeEntitlements.FreeMaxActiveHabits);
        live.Set(true);
        live.MaxActiveHabits.Should().Be(int.MaxValue);
        live.Set(true);
        live.Set(false);

        raised.Should().Be(2);
    }

    [Theory]
    [InlineData(ThemeAccess.CandyBento, false, 1, true)]
    [InlineData(ThemeAccess.NeonNight, false, 1, true)]
    [InlineData(ThemeAccess.SoftPastel, false, 9, false)]
    [InlineData(ThemeAccess.SoftPastel, false, 10, true)]
    [InlineData(ThemeAccess.SoftPastel, true, 1, true)]
    [InlineData("seasonal-x", false, 99, false)]
    [InlineData("seasonal-x", true, 1, true)]
    public void ThemeAccess_FreeLevelAndPlus(string theme, bool plus, int level, bool expected) =>
        ThemeAccess.IsUnlocked(theme, plus, level).Should().Be(expected);
}

public class HabitAllowanceTests
{
    private static List<Habit> Habits(int count) =>
        Enumerable.Range(0, count).Select(i =>
        {
            var h = NewHabit();
            h.Name = $"H{i}";
            h.SortKey = $"a{i:00}";
            return h;
        }).ToList();

    [Fact]
    public void UnderTheLimit_NothingIsReadOnly()
    {
        var result = HabitAllowance.Evaluate(Habits(5), null, 5);

        result.ReadOnly.Should().BeEmpty();
        result.NeedsChoice.Should().BeFalse();
    }

    [Fact]
    public void Downgrade_With9Habits_UserChooses5_TheOther4AreReadOnly()
    {
        var habits = Habits(9);
        var chosen = new[] { habits[8].Id, habits[6].Id, habits[4].Id, habits[2].Id, habits[0].Id };

        var result = HabitAllowance.Evaluate(habits, chosen, 5);

        result.Running.Should().BeEquivalentTo(chosen);
        result.ReadOnly.Should().BeEquivalentTo([habits[1].Id, habits[3].Id, habits[5].Id, habits[7].Id]);
        result.NeedsChoice.Should().BeFalse();
        habits.Should().HaveCount(9, "không thói quen nào bị xóa");
    }

    [Fact]
    public void Downgrade_WithoutAChoice_KeepsTheFirstFiveForNow_AndAsks()
    {
        var habits = Habits(9);

        var result = HabitAllowance.Evaluate(habits, null, 5);

        result.NeedsChoice.Should().BeTrue();
        result.Running.Should().Equal(habits.Take(5).Select(h => h.Id));
        result.ReadOnly.Should().HaveCount(4);
    }

    [Fact]
    public void StaleOrExtraChoices_AreIgnored_AndTheRestIsFilled()
    {
        var habits = Habits(7);
        var chosen = new[] { Guid.NewGuid(), habits[6].Id, habits[5].Id };

        var result = HabitAllowance.Evaluate(habits, chosen, 5);

        result.Running.Should().HaveCount(5).And.Contain([habits[6].Id, habits[5].Id]);
        result.NeedsChoice.Should().BeTrue();
    }

    [Fact]
    public void ArchivedHabits_DoNotCountTowardTheLimit()
    {
        var habits = Habits(7);
        habits[0].ArchivedAt = DateTime.UtcNow;
        habits[1].ArchivedAt = DateTime.UtcNow;

        HabitAllowance.Evaluate(habits, null, 5).ReadOnly.Should().BeEmpty();
    }
}
