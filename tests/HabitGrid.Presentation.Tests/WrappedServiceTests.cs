using FluentAssertions;
using HabitGrid.Core.Domain;
using HabitGrid.Core.Domain.Monetization;
using HabitGrid.Presentation.Services;

namespace HabitGrid.Presentation.Tests;

public class WrappedServiceTests
{
    private static WrappedService Create(AppFixture f, bool plus = true)
    {
        var live = new LiveEntitlements();
        live.Set(plus);
        return new WrappedService(f.Habits, f.Logs, f.Days, live);
    }

    [Fact]
    public async Task LastMonth_IsBuiltFromLastMonthsLogs_AndThisMonthStopsAtToday()
    {
        using var f = new AppFixture(); // hôm nay 22/10/2026
        var h = await f.AddAsync("Đọc sách", startDaysAgo: 60);
        for (var d = 1; d <= 10; d++) await f.LogAsync(h, new DateOnly(2026, 9, d), 1);
        for (var d = 1; d <= 3; d++) await f.LogAsync(h, new DateOnly(2026, 10, d), 1);
        var service = Create(f);

        var last = await service.BuildAsync(1);
        last.Month.Should().Be(9);
        last.TotalCheckIns.Should().Be(10);
        last.TopHabit!.Name.Should().Be("Đọc sách");
        last.To.Should().Be(new DateOnly(2026, 9, 30));

        var current = await service.BuildAsync(0);
        current.Month.Should().Be(10);
        current.TotalCheckIns.Should().Be(3);
        current.To.Should().Be(AppFixture.Today);
    }

    [Fact]
    public async Task ArchivedHabits_StillCountInThePastMonth_ButDeletedOnesDoNot()
    {
        using var f = new AppFixture();
        var archived = await f.AddAsync("Cũ", startDaysAgo: 60);
        var deleted = await f.AddAsync("Xóa", startDaysAgo: 60);
        await f.LogAsync(archived, new DateOnly(2026, 9, 2), 1);
        await f.LogAsync(deleted, new DateOnly(2026, 9, 2), 1);
        await f.Habits.ArchiveAsync(archived.Id);
        await f.Habits.SoftDeleteAsync(deleted.Id);

        (await Create(f).BuildAsync(1)).TotalCheckIns.Should().Be(1);
    }

    [Fact]
    public async Task NoHabits_GivesAnEmptyWrapped()
    {
        using var f = new AppFixture();

        (await Create(f).BuildAsync(1)).IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void IsPlus_FollowsEntitlements()
    {
        using var f = new AppFixture();

        Create(f, plus: false).IsPlus.Should().BeFalse();
        Create(f, plus: true).IsPlus.Should().BeTrue();
    }
}
