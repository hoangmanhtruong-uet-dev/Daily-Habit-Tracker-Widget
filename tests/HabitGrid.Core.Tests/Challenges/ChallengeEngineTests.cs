using FluentAssertions;
using HabitGrid.Core.Domain;
using HabitGrid.Core.Domain.Challenges;
using Xunit;
using static HabitGrid.Core.Tests.Domain.DomainTestData;

namespace HabitGrid.Core.Tests.Challenges;

public class ChallengeEngineTests
{
    private static Challenge For(Habit h, int days, DateOnly start) => new(Guid.NewGuid(), h.Id, "t", "read10", start, days);

    [Fact]
    public void AllDaysDone_Completes_OnTheLastDay()
    {
        var h = NewHabit(start: Today.AddDays(-10));
        var c = For(h, 5, Today.AddDays(-4));

        var p = ChallengeEngine.Evaluate(c, h, DoneRange(h, Today.AddDays(-4), Today), Today);

        p.State.Should().Be(ChallengeState.Completed);
        p.GoodDays.Should().Be(5);
        p.DayNumber.Should().Be(5);
        p.Fraction.Should().Be(1);
    }

    [Fact]
    public void TodayPending_IsStillActive_NotFailed()
    {
        var h = NewHabit(start: Today.AddDays(-10));
        var c = For(h, 10, Today.AddDays(-2));

        var p = ChallengeEngine.Evaluate(c, h, DoneRange(h, Today.AddDays(-2), Today.AddDays(-1)), Today);

        p.State.Should().Be(ChallengeState.Active);
        p.GoodDays.Should().Be(2);
        p.DayNumber.Should().Be(3);
        p.DaysLeft.Should().Be(7);
    }

    [Fact]
    public void AMissedPastDay_Fails_AndReportsWhen()
    {
        var h = NewHabit(start: Today.AddDays(-10));
        var c = For(h, 10, Today.AddDays(-3));
        var logs = new List<DayLog> { Done(h, Today.AddDays(-3)), Done(h, Today.AddDays(-1)) };

        var p = ChallengeEngine.Evaluate(c, h, logs, Today);

        p.State.Should().Be(ChallengeState.Failed);
        p.FailedOn.Should().Be(Today.AddDays(-2));
    }

    [Fact]
    public void FrozenAndSkippedDays_AreExcused()
    {
        var h = NewHabit(start: Today.AddDays(-10));
        var c = For(h, 4, Today.AddDays(-3));
        var logs = new List<DayLog>
        {
            Done(h, Today.AddDays(-3)),
            WithStatus(h, Today.AddDays(-2), DayStatus.Frozen),
            WithStatus(h, Today.AddDays(-1), DayStatus.Skipped),
            Done(h, Today)
        };

        ChallengeEngine.Evaluate(c, h, logs, Today).State.Should().Be(ChallengeState.Completed);
    }

    [Fact]
    public void QuitChallenge_IsCleanByDefault_FailsOnARelapse_AndTodayOnlyCountsOnceItIsOver()
    {
        var h = NewHabit(HabitKind.Quit, start: Today.AddDays(-10));
        var c = For(h, 3, Today.AddDays(-2));

        var clean = ChallengeEngine.Evaluate(c, h, [], Today);
        clean.State.Should().Be(ChallengeState.Active, "hôm nay chưa trôi qua nên chưa tính");
        clean.GoodDays.Should().Be(2);
        ChallengeEngine.Evaluate(c, h, [], Today.AddDays(1)).State.Should().Be(ChallengeState.Completed);

        var relapse = ChallengeEngine.Evaluate(c, h, [WithStatus(h, Today.AddDays(-1), DayStatus.Failed)], Today);
        relapse.State.Should().Be(ChallengeState.Failed);
        relapse.FailedOn.Should().Be(Today.AddDays(-1));
    }

    [Fact]
    public void LogsOfOtherHabits_AreIgnored_AndLatestLogWins()
    {
        var h = NewHabit(start: Today.AddDays(-10));
        var other = NewHabit(start: Today.AddDays(-10));
        var c = For(h, 2, Today.AddDays(-1));
        var logs = new List<DayLog>
        {
            Done(other, Today.AddDays(-1)),
            Done(h, Today.AddDays(-1)),
            WithStatus(h, Today.AddDays(-1), DayStatus.None) with { UpdatedAt = DateTime.UtcNow.AddMinutes(5) }
        };

        ChallengeEngine.Evaluate(c, h, logs, Today).State.Should().Be(ChallengeState.Failed);
    }

    [Fact]
    public void CatalogIsConsistent()
    {
        ChallengeCatalog.Templates.Select(t => t.Id).Should().OnlyHaveUniqueItems();
        ChallengeCatalog.Templates.Should().OnlyContain(t => t.DefaultDays >= ChallengeCatalog.MinDays && t.DefaultDays <= ChallengeCatalog.MaxDays && t.Target >= 1);
        ChallengeCatalog.DayChoices.Should().OnlyContain(d => d >= ChallengeCatalog.MinDays && d <= ChallengeCatalog.MaxDays);
    }
}
