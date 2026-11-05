using FluentAssertions;
using HabitGrid.Core.Domain;
using HabitGrid.Core.Domain.Focus;

namespace HabitGrid.Presentation.Tests;

public class FocusServiceTests
{
    private static FocusResult Result(Guid? habit, int minutes)
    {
        var end = DateTime.SpecifyKind(AppFixture.Today.ToDateTime(new TimeOnly(10, 0)), DateTimeKind.Local).ToUniversalTime();
        return new FocusResult(habit, end.AddMinutes(-minutes), end, minutes, FocusMode.Pomodoro, true);
    }

    [Fact]
    public async Task Pomodoro25_OnDurationHabit_Adds25Minutes_AndFiveXp()
    {
        using var f = new AppFixture();
        var read = await f.AddAsync("Đọc", HabitKind.Duration, target: 30, startDaysAgo: 5);
        await f.LogAsync(read, AppFixture.Today, 10);
        var gamification = f.NewGamification();
        var before = await gamification.RefreshAsync();

        await f.NewFocus().CompleteAsync(Result(read.Id, 25));
        var after = await gamification.RefreshAsync();

        var log = await f.Logs.GetAsync(read.Id, AppFixture.Today);
        log!.Value.Should().Be(35);
        log.Status.Should().Be(DayStatus.Done, "35 ≥ mục tiêu 30");
        after.Xp.Awards.Count(a => a.Kind == "focus").Should().Be(1);
        after.Xp.Awards.Single(a => a.Kind == "focus").Amount.Should().Be(5);
        after.Xp.TotalXp.Should().BeGreaterThan(before.Xp.TotalXp);
    }

    [Fact]
    public async Task NonDurationHabit_OnlyRecordsTheSession()
    {
        using var f = new AppFixture();
        var habit = await f.AddAsync("A", startDaysAgo: 5);

        var record = await f.NewFocus().CompleteAsync(Result(habit.Id, 30));

        record.Should().NotBeNull();
        (await f.Logs.GetAsync(habit.Id, AppFixture.Today)).Should().BeNull();
        (await f.Focus.GetAllAsync()).Should().ContainSingle().Which.Minutes.Should().Be(30);
    }

    [Fact]
    public async Task ZeroMinutes_RecordsNothing()
    {
        using var f = new AppFixture();

        (await f.NewFocus().CompleteAsync(Result(null, 0))).Should().BeNull();
        (await f.Focus.GetAllAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task TenHoursOfFocus_UnlocksTheFocusBadge()
    {
        using var f = new AppFixture();
        var focus = f.NewFocus();
        for (var i = 0; i < 12; i++) await focus.CompleteAsync(Result(null, 50));

        var snapshot = await f.NewGamification().RefreshAsync();

        snapshot.Achievements.Single(a => a.Id == "focus_10h").Unlocked.Should().BeTrue();
    }

    [Fact]
    public async Task Candidates_ListDurationHabitsFirst_AndExcludeQuit()
    {
        using var f = new AppFixture();
        await f.AddAsync("Bool", startDaysAgo: 1);
        await f.AddAsync("Quit", HabitKind.Quit, startDaysAgo: 1);
        await f.AddAsync("Read", HabitKind.Duration, target: 20, startDaysAgo: 1);

        var list = await f.NewFocus().CandidatesAsync();

        list.Select(h => h.Name).Should().Equal("Read", "Bool");
    }
}
