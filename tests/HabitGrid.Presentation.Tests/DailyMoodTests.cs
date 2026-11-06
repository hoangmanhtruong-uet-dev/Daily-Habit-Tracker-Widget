using FluentAssertions;
using HabitGrid.Core.Domain;
using HabitGrid.Core.Domain.Insights;
using HabitGrid.Presentation.Services;

namespace HabitGrid.Presentation.Tests;

public class DailyMoodTests
{
    private static readonly DateOnly Today = AppFixture.Today;

    [Fact]
    public async Task SetAndGet_RoundTrips_WithTrimmedNote_AndClearing()
    {
        using var f = new AppFixture();
        var service = new DailyMoodService(f.Settings, f.Days);

        (await service.SetAsync(Today, 4, "  Hôm nay vui\nlắm  ")).Should().BeTrue();
        (await service.SetAsync(Today.AddDays(-1), 2)).Should().BeTrue();

        var mood = await service.GetAsync(Today);
        mood!.Mood.Should().Be(4);
        mood.Note.Should().Be("Hôm nay vui lắm");
        (await service.GetAsync(Today.AddDays(-1)))!.Note.Should().BeNull();
        (await service.AllAsync()).Should().HaveCount(2);

        await service.SetAsync(Today, null);
        (await service.GetAsync(Today)).Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task OutOfRangeMood_IsRejected(int mood)
    {
        using var f = new AppFixture();

        (await new DailyMoodService(f.Settings, f.Days).SetAsync(Today, mood)).Should().BeFalse();
    }

    [Fact]
    public async Task FutureDays_AreRejected()
    {
        using var f = new AppFixture();

        (await new DailyMoodService(f.Settings, f.Days).SetAsync(Today.AddDays(1), 3)).Should().BeFalse();
    }

    [Fact]
    public void Note_IsCapped_AndParseIgnoresGarbageLines()
    {
        DailyMoodService.Clean(new string('x', 500))!.Length.Should().Be(DailyMoodService.MaxNote);
        DailyMoodService.Clean("   ").Should().BeNull();

        var parsed = DailyMoodService.Parse("2026-10-21=5|tốt\nrác\n2026-10-22=9\n2026-10-20=3\n");

        parsed.Keys.Should().BeEquivalentTo([new DateOnly(2026, 10, 21), new DateOnly(2026, 10, 20)]);
        parsed[new DateOnly(2026, 10, 21)].Note.Should().Be("tốt");
    }

    [Fact]
    public async Task ConcurrentWrites_DoNotLoseEntries()
    {
        using var f = new AppFixture();
        var service = new DailyMoodService(f.Settings, f.Days);

        await Task.WhenAll(Enumerable.Range(0, 10).Select(i => service.SetAsync(Today.AddDays(-i), 1 + i % 5)));

        (await service.AllAsync()).Should().HaveCount(10);
    }

    [Fact]
    public async Task Insights_UseTheDayMood_AndCompareDoneVsOtherDays()
    {
        using var f = new AppFixture();
        var h = await f.AddAsync("A", startDaysAgo: 30);
        await f.LogAsync(h, Today, 1);
        var moods = new Dictionary<DateOnly, int> { [Today] = 5, [Today.AddDays(-1)] = 2 };

        var report = InsightsCalculator.Build(await f.Habits.GetAllAsync(), await f.Logs.GetAllForHabitAsync(h.Id), Today, Today.AddDays(-29), dayMoods: moods);

        report.Mood.Samples.Should().Be(2);
        report.Mood.DoneDaysAverage.Should().Be(5);
        report.Mood.OtherDaysAverage.Should().Be(2);
        report.Mood.Average.Should().Be(3.5);
    }
}
