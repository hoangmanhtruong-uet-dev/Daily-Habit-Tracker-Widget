using FluentAssertions;
using HabitGrid.Core.Domain;
using HabitGrid.Core.Domain.Monetization;
using HabitGrid.Core.Domain.Routines;
using HabitGrid.Presentation.Services;

namespace HabitGrid.Presentation.Tests;

public class RoutineTimerTests
{
    private static async Task<(RoutineTimerService Timer, RoutineService Routines, Routine Routine, List<Habit> Habits)> SetupAsync(AppFixture f, bool plus = true)
    {
        var live = new LiveEntitlements();
        live.Set(true);
        var routines = new RoutineService(f.Habits, f.Logs, new HabitGrid.Data.V3.RoutineRepository(f.Database, f.Clock, f.Bus), live, f.Days, f.Settings);
        var habits = new List<Habit>();
        for (var i = 0; i < 3; i++) habits.Add(await f.AddAsync($"B{i}", startDaysAgo: 5));
        var created = await routines.CreateAsync(new RoutineDraft("Sáng", HabitIcon.Emoji("🌅"), TimeOfDay.Morning, habits.Select(h => h.Id).ToList()));
        var gate = new LiveEntitlements();
        gate.Set(plus);
        return (new RoutineTimerService(routines, f.Settings, gate), routines, created.Routine!, habits);
    }

    [Fact]
    public async Task Plan_DefaultsToFiveMinutes_AndKeepsSavedDurations_InRoutineOrder()
    {
        using var f = new AppFixture();
        var (timer, _, routine, habits) = await SetupAsync(f);

        await timer.SetDurationAsync(habits[1].Id, 12);
        var plan = await timer.PlanAsync(routine.Id);

        plan.Select(p => p.HabitId).Should().Equal(habits.Select(h => h.Id));
        plan.Select(p => p.Minutes).Should().Equal(5, 12, 5);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(500, 120)]
    [InlineData(25, 25)]
    public async Task SetDuration_IsClampedToTheAllowedRange(int input, int expected)
    {
        using var f = new AppFixture();
        var (timer, _, routine, habits) = await SetupAsync(f);

        await timer.SetDurationAsync(habits[0].Id, input);

        (await timer.PlanAsync(routine.Id))[0].Minutes.Should().Be(expected);
    }

    [Fact]
    public async Task Runner_OnlyContainsStepsNotDoneToday()
    {
        using var f = new AppFixture();
        var (timer, _, routine, habits) = await SetupAsync(f);
        await f.LogAsync(habits[0], AppFixture.Today, 1);

        var runner = await timer.BuildRunnerAsync(routine.Id);

        runner!.Steps.Select(s => s.HabitId).Should().Equal(habits[1].Id, habits[2].Id);
    }

    [Fact]
    public async Task Runner_IsNullWhenFree_OrWhenEverythingIsDone()
    {
        using var f = new AppFixture();
        var (timer, routines, routine, habits) = await SetupAsync(f);
        var (freeTimer, _, freeRoutine, _) = await SetupAsyncFree(f);

        (await freeTimer.BuildRunnerAsync(freeRoutine.Id)).Should().BeNull();
        freeTimer.IsAvailable.Should().BeFalse();

        await routines.CompleteAllAsync(routine.Id);
        (await timer.BuildRunnerAsync(routine.Id)).Should().BeNull();
    }

    private static async Task<(RoutineTimerService Timer, RoutineService Routines, Routine Routine, List<Habit> Habits)> SetupAsyncFree(AppFixture f)
    {
        var live = new LiveEntitlements();
        live.Set(true);
        var routines = new RoutineService(f.Habits, f.Logs, new HabitGrid.Data.V3.RoutineRepository(f.Database, f.Clock, f.Bus), live, f.Days, f.Settings);
        var existing = (await routines.ListAsync()).First();
        var gate = new LiveEntitlements();
        gate.Set(false);
        return (new RoutineTimerService(routines, f.Settings, gate), routines, existing, []);
    }

    [Fact]
    public async Task CorruptedStorage_FallsBackToDefaults()
    {
        using var f = new AppFixture();
        var (timer, _, routine, _) = await SetupAsync(f);
        await f.Settings.SetAsync(RoutineTimerService.Key, "[[[");

        (await timer.PlanAsync(routine.Id)).Should().OnlyContain(p => p.Minutes == RoutineRunner.DefaultMinutes);
    }
}
