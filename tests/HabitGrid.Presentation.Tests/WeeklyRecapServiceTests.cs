using FluentAssertions;
using HabitGrid.Core.Domain;
using HabitGrid.Core.Domain.Insights;
using HabitGrid.Presentation.Services;

namespace HabitGrid.Presentation.Tests;

public class WeeklyRecapServiceTests
{
    // AppFixture.Today = thứ Năm 22/10/2026; thứ Hai của tuần là 19/10, tuần trước 12/10–18/10.
    private static readonly DateOnly Monday = new(2026, 10, 19);

    private static WeeklyRecapService Create(AppFixture f) => new(f.Habits, f.Logs, f.Settings, f.Days);

    private static async Task SeedLastWeekAsync(AppFixture f, Habit h)
    {
        for (var i = 0; i < 7; i++)
        {
            await f.LogAsync(h, Monday.AddDays(-7 + i), 1);
        }
    }

    [Fact]
    public async Task OutsideMondayToWednesday_NoRecapCardIsShown()
    {
        using var f = new AppFixture();
        var h = await f.AddAsync("A", startDaysAgo: 30);
        await SeedLastWeekAsync(f, h);
        f.Days.Today = Monday.AddDays(3); // thứ Năm

        (await Create(f).GetAsync()).Recap.Should().BeNull();
    }

    [Fact]
    public async Task OnMonday_TheRecapOfLastWeekShows_UntilDismissed()
    {
        using var f = new AppFixture();
        var h = await f.AddAsync("A", startDaysAgo: 30);
        await SeedLastWeekAsync(f, h);
        f.Days.Today = Monday;
        var service = Create(f);

        var card = await service.GetAsync();
        card.Recap.Should().NotBeNull();
        card.Recap!.Rate.Should().Be(1);
        card.Choices.Should().ContainSingle();

        await service.DismissAsync();
        (await service.GetAsync()).Recap.Should().BeNull();

        f.Days.Today = Monday.AddDays(7);
        await f.LogAsync(h, Monday.AddDays(2), 1);
        (await service.GetAsync()).Recap.Should().NotBeNull("tuần mới có dữ liệu thì hiện lại");
    }

    [Fact]
    public async Task NewUserWithNoCheckIns_SeesNothing()
    {
        using var f = new AppFixture();
        await f.AddAsync("A", startDaysAgo: 30);
        f.Days.Today = Monday;

        (await Create(f).GetAsync()).IsEmpty.Should().BeTrue();
    }

    [Fact]
    public async Task Intent_TracksProgressThisWeek_AndExpiresNextWeek()
    {
        using var f = new AppFixture();
        var h = await f.AddAsync("Chạy bộ", startDaysAgo: 30);
        f.Days.Today = Monday.AddDays(2);
        var service = Create(f);

        (await service.SetIntentAsync(h.Id)).Should().BeTrue();
        await f.LogAsync(h, Monday, 1);
        await f.LogAsync(h, Monday.AddDays(1), 1);

        var intent = (await service.GetAsync()).Intent!;
        intent.Name.Should().Be("Chạy bộ");
        intent.Done.Should().Be(2);
        intent.Due.Should().Be(7);

        f.Days.Today = Monday.AddDays(7);
        (await service.GetAsync()).Intent.Should().BeNull("ý định chỉ có hiệu lực trong tuần được chọn");
    }

    [Fact]
    public async Task Intent_UsesTheWeeklyTimesForTimesPerWeekHabits()
    {
        using var f = new AppFixture();
        var h = await f.AddAsync("Tập gym", frequency: new HabitFrequency.TimesPerWeek(3), startDaysAgo: 30);
        f.Days.Today = Monday.AddDays(1);
        var service = Create(f);
        await service.SetIntentAsync(h.Id);
        await f.LogAsync(h, Monday, 1);

        var intent = (await service.GetAsync()).Intent!;
        intent.Due.Should().Be(3);
        intent.Done.Should().Be(1);
    }

    [Fact]
    public async Task Intent_CanBeCleared_AndRejectsQuitOrUnknownHabits()
    {
        using var f = new AppFixture();
        var h = await f.AddAsync("A", startDaysAgo: 30);
        var quit = await f.AddAsync("Bỏ trà sữa", kind: HabitKind.Quit, startDaysAgo: 30);
        f.Days.Today = Monday;
        var service = Create(f);

        (await service.SetIntentAsync(quit.Id)).Should().BeFalse();
        (await service.SetIntentAsync(Guid.NewGuid())).Should().BeFalse();
        await service.SetIntentAsync(h.Id);
        await service.SetIntentAsync(null);
        (await service.GetAsync()).Intent.Should().BeNull();
    }

    [Fact]
    public async Task ADeletedIntentHabit_DisappearsInsteadOfBreakingTheCard()
    {
        using var f = new AppFixture();
        var h = await f.AddAsync("A", startDaysAgo: 30);
        f.Days.Today = Monday;
        var service = Create(f);
        await service.SetIntentAsync(h.Id);
        await f.Habits.SoftDeleteAsync(h.Id);

        (await service.GetAsync()).Intent.Should().BeNull();
    }
}
