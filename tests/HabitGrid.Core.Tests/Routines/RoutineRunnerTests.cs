using FluentAssertions;
using HabitGrid.Core.Domain.Routines;
using Xunit;

namespace HabitGrid.Core.Tests.Routines;

public class RoutineRunnerTests
{
    private static readonly DateTime T0 = new(2026, 11, 6, 7, 0, 0, DateTimeKind.Utc);

    private static TimedStep Step(string name, int minutes) => new(Guid.NewGuid(), name, minutes);

    [Fact]
    public void StepsRunBackToBack_AndTheOverflowCarriesToTheNextStep()
    {
        var a = Step("A", 2);
        var b = Step("B", 3);
        var runner = new RoutineRunner([a, b]);
        runner.Start(T0);

        runner.Tick(T0.AddSeconds(119)).Should().BeEmpty();
        var done = runner.Tick(T0.AddSeconds(130));

        done.Should().ContainSingle().Which.Should().Be(new StepOutcome(a, true));
        runner.Current.Should().Be(b);
        runner.Elapsed(T0.AddSeconds(130)).TotalSeconds.Should().BeApproximately(10, 0.01, "10 giây dư thuộc về bước B");
        runner.Remaining(T0.AddSeconds(130)).TotalSeconds.Should().BeApproximately(170, 0.01);
    }

    [Fact]
    public void ALongGap_FinishesSeveralStepsAndTheWholeRoutine()
    {
        var steps = new[] { Step("A", 1), Step("B", 1), Step("C", 1) };
        var runner = new RoutineRunner(steps);
        runner.Start(T0);

        var done = runner.Tick(T0.AddMinutes(10));

        done.Select(o => o.Step).Should().Equal(steps);
        done.Should().OnlyContain(o => o.Completed);
        runner.IsFinished.Should().BeTrue();
        runner.IsRunning.Should().BeFalse();
        runner.Tick(T0.AddMinutes(11)).Should().BeEmpty();
    }

    [Fact]
    public void PausedTime_DoesNotCount()
    {
        var a = Step("A", 1);
        var runner = new RoutineRunner([a]);
        runner.Start(T0);
        runner.Pause(T0.AddSeconds(30));

        runner.IsPaused.Should().BeTrue();
        runner.Tick(T0.AddMinutes(30)).Should().BeEmpty();
        runner.Resume(T0.AddMinutes(30));
        runner.Tick(T0.AddMinutes(30).AddSeconds(29)).Should().BeEmpty();
        runner.Tick(T0.AddMinutes(30).AddSeconds(31)).Should().ContainSingle();
    }

    [Fact]
    public void CompleteAndSkip_AdvanceWithTheRightOutcome()
    {
        var a = Step("A", 5);
        var b = Step("B", 5);
        var c = Step("C", 5);
        var runner = new RoutineRunner([a, b, c]);
        runner.Start(T0);

        runner.CompleteCurrent(T0.AddSeconds(20)).Should().Be(new StepOutcome(a, true));
        runner.SkipCurrent(T0.AddSeconds(25)).Should().Be(new StepOutcome(b, false));
        runner.Current.Should().Be(c);
        runner.IsRunning.Should().BeTrue("đang chạy thì bước kế chạy tiếp");
        runner.Elapsed(T0.AddSeconds(25)).Should().Be(TimeSpan.Zero);
        runner.CompleteCurrent(T0.AddSeconds(30));
        runner.IsFinished.Should().BeTrue();
        runner.CompleteCurrent(T0.AddSeconds(31)).Should().BeNull();
    }

    [Fact]
    public void AdvancingWhilePaused_StaysPausedOnTheNextStep()
    {
        var runner = new RoutineRunner([Step("A", 5), Step("B", 5)]);
        runner.Start(T0);
        runner.Pause(T0.AddSeconds(10));

        runner.SkipCurrent(T0.AddSeconds(11));

        runner.IsPaused.Should().BeTrue();
        runner.Tick(T0.AddHours(1)).Should().BeEmpty();
    }

    [Fact]
    public void TotalRemaining_SumsTheCurrentAndLaterSteps()
    {
        var runner = new RoutineRunner([Step("A", 2), Step("B", 3), Step("C", 5)]);
        runner.Start(T0);

        runner.TotalRemaining(T0.AddSeconds(60)).Should().Be(TimeSpan.FromSeconds(60 + 8 * 60));
    }

    [Fact]
    public void NothingHappensBeforeStart_AndEmptyRoutinesAreHarmless()
    {
        var runner = new RoutineRunner([Step("A", 1)]);
        runner.Tick(T0.AddHours(1)).Should().BeEmpty();
        runner.CompleteCurrent(T0).Should().BeNull();
        runner.IsFinished.Should().BeFalse();

        var empty = new RoutineRunner([]);
        empty.Start(T0);
        empty.IsFinished.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(121)]
    public void RejectsOutOfRangeDurations(int minutes) =>
        FluentActions.Invoking(() => new RoutineRunner([Step("A", minutes)])).Should().Throw<ArgumentOutOfRangeException>();
}
