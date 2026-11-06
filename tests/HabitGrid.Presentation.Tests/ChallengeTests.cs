using FluentAssertions;
using HabitGrid.Core.Domain;
using HabitGrid.Core.Domain.Challenges;
using HabitGrid.Core.Domain.Monetization;
using HabitGrid.Presentation.Services;

namespace HabitGrid.Presentation.Tests;

public class ChallengeTests
{
    private static readonly DateOnly Today = AppFixture.Today;

    private static ChallengeService Create(AppFixture f, bool plus = false)
    {
        var live = new LiveEntitlements();
        live.Set(plus);
        return new ChallengeService(f.Settings, f.Habits, f.Logs, live, f.Days);
    }

    [Fact]
    public async Task Start_CreatesTheHabit_AndTheChallengeBeginsToday()
    {
        using var f = new AppFixture();
        var service = Create(f);

        var result = await service.StartAsync("read10", "Đọc 10 trang", 30);

        result.Succeeded.Should().BeTrue();
        var habit = (await f.Habits.GetAllActiveAsync()).Single();
        habit.Name.Should().Be("Đọc 10 trang");
        habit.Kind.Should().Be(HabitKind.Count);
        habit.EffectiveTarget.Should().Be(10);
        habit.StartDate.Should().Be(Today);
        result.Challenge!.End.Should().Be(Today.AddDays(29));

        var board = await service.RefreshAsync();
        board.Items.Should().ContainSingle().Which.State.Should().Be(ChallengeState.Active);
    }

    [Theory]
    [InlineData("nope", 30, ChallengeError.UnknownTemplate)]
    [InlineData("read10", 2, ChallengeError.InvalidDays)]
    [InlineData("read10", 101, ChallengeError.InvalidDays)]
    public async Task Start_RejectsBadInput_WithoutCreatingAHabit(string template, int days, ChallengeError expected)
    {
        using var f = new AppFixture();

        (await Create(f).StartAsync(template, "X", days)).Error.Should().Be(expected);
        (await f.Habits.GetAllActiveAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Start_RejectsABlankTitle()
    {
        using var f = new AppFixture();

        (await Create(f).StartAsync("read10", "   ", 30)).Error.Should().Be(ChallengeError.InvalidDays);
    }

    [Fact]
    public async Task FreeRunsOneAtATime_PlusRunsFive()
    {
        using var f = new AppFixture();
        var free = Create(f);

        (await free.StartAsync("read10", "A", 30)).Succeeded.Should().BeTrue();
        (await free.StartAsync("water", "B", 30)).Error.Should().Be(ChallengeError.ActiveLimit);

        var plus = Create(f, plus: true);
        for (var i = 0; i < 4; i++)
        {
            (await plus.StartAsync("journal", $"P{i}", 30)).Succeeded.Should().BeTrue();
        }

        (await plus.StartAsync("journal", "P5", 30)).Error.Should().Be(ChallengeError.ActiveLimit);
    }

    [Fact]
    public async Task Start_RespectsTheHabitLimit()
    {
        using var f = new AppFixture();
        for (var i = 0; i < 5; i++) await f.AddAsync($"H{i}", startDaysAgo: 3);

        (await Create(f).StartAsync("read10", "A", 30)).Error.Should().Be(ChallengeError.HabitLimit);
    }

    [Fact]
    public async Task Completion_IsRecordedOnce_AndKeptAsABadge()
    {
        using var f = new AppFixture();
        var service = Create(f);
        var started = await service.StartAsync("journal", "Nhật ký", 3);
        var habit = (await f.Habits.GetAllActiveAsync()).Single();

        // Lùi ngày bắt đầu để ba ngày đã trôi qua và đều xong.
        f.Days.Today = Today.AddDays(2);

        for (var i = 0; i < 3; i++)
        {
            await f.LogAsync(habit, Today.AddDays(i), 1);
        }

        var first = await service.RefreshAsync();
        first.NewlyCompleted.Should().ContainSingle();
        first.Items.Single().State.Should().Be(ChallengeState.Completed);

        (await service.RefreshAsync()).NewlyCompleted.Should().BeEmpty("chỉ chúc mừng một lần");
        (await service.CompletedAsync()).Should().ContainSingle().Which.Id.Should().Be(started.Challenge!.Id);

        await service.DismissAsync(started.Challenge.Id);
        (await service.RefreshAsync()).Items.Should().BeEmpty();
        (await service.CompletedAsync()).Should().ContainSingle("huy hiệu vẫn còn sau khi ẩn");
    }

    [Fact]
    public async Task AMissedDay_FailsTheChallenge_AndRestartCountsFromToday()
    {
        using var f = new AppFixture();
        var service = Create(f);
        var started = await service.StartAsync("journal", "Nhật ký", 5);
        f.Days.Today = Today.AddDays(3);

        var board = await service.RefreshAsync();
        board.Items.Single().State.Should().Be(ChallengeState.Failed);
        board.Items.Single().FailedOn.Should().Be(Today);

        (await service.RestartAsync(started.Challenge!.Id)).Should().Be(ChallengeError.None);
        var after = (await service.RefreshAsync()).Items.Single();
        after.State.Should().Be(ChallengeState.Active);
        after.Challenge.Start.Should().Be(Today.AddDays(3));
    }

    [Fact]
    public async Task AFailedChallenge_DoesNotBlockStartingANewOne()
    {
        using var f = new AppFixture();
        var service = Create(f);
        await service.StartAsync("journal", "A", 5);
        f.Days.Today = Today.AddDays(3);

        (await service.StartAsync("water", "B", 5)).Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task ADeletedHabit_ClosesItsChallenge_InsteadOfLeavingItRunning()
    {
        using var f = new AppFixture();
        var service = Create(f);
        await service.StartAsync("journal", "A", 30);
        var habit = (await f.Habits.GetAllActiveAsync()).Single();
        await f.Habits.SoftDeleteAsync(habit.Id);

        (await service.RefreshAsync()).Items.Should().BeEmpty();
        (await Create(f).StartAsync("water", "B", 30)).Succeeded.Should().BeTrue("thử thách của thói quen đã xóa không chiếm chỗ");
    }

    [Fact]
    public async Task CorruptedStorage_IsTreatedAsEmpty()
    {
        using var f = new AppFixture();
        await f.Settings.SetAsync(ChallengeService.Key, "{not json");

        (await Create(f).RefreshAsync()).Items.Should().BeEmpty();
    }
}
