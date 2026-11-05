using FluentAssertions;
using HabitGrid.Core.Domain;
using HabitGrid.Core.Domain.Insights;
using Xunit;
using static HabitGrid.Core.Tests.Domain.DomainTestData;

namespace HabitGrid.Core.Tests.Insights;

public class InsightsCalculatorTests
{
    [Fact]
    public void Report_CountsRatePerfectDaysCheckInsAndStreaks()
    {
        var a = NewHabit(start: Today.AddDays(-60));
        var b = NewHabit(start: Today.AddDays(-60));
        var logs = new List<DayLog>();
        logs.AddRange(DoneRange(a, Today.AddDays(-9), Today));
        logs.AddRange(DoneRange(b, Today.AddDays(-4), Today));

        var report = InsightsCalculator.Build([a, b], logs, Today, Today.AddDays(-9));

        report.CheckIns.Should().Be(15);
        report.PerfectDays.Should().Be(5, "chỉ 5 ngày cuối cả hai thói quen cùng xong");
        report.OverallRate.Should().BeApproximately((1.0 + 0.5) / 2, 0.001);
        report.BestStreak.Should().Be(10);
        report.Habits.Should().HaveCount(2);
        report.Weekdays.Should().HaveCount(7);
        report.Weekdays[0].Day.Should().Be(DayOfWeek.Monday);
        report.Hours.Should().HaveCount(24);
    }

    [Fact]
    public void WeeklyTrend_HasMovingAverageOverUpTo4Weeks()
    {
        var h = NewHabit(start: Today.AddDays(-100));
        var logs = DoneRange(h, Today.AddDays(-34), Today);

        var report = InsightsCalculator.Build([h], logs, Today, Today.AddDays(-55));

        report.Weeks.Should().NotBeEmpty();
        report.Weeks.Select(w => w.WeekStart.DayOfWeek).Should().OnlyContain(d => d == DayOfWeek.Monday);
        report.Weeks[^1].Rate.Should().Be(1);
        report.Weeks[0].Rate.Should().Be(0);
        report.Weeks[^1].MovingAverage.Should().Be(1);
        report.Weeks.Should().OnlyContain(w => w.MovingAverage == null || (w.MovingAverage >= 0 && w.MovingAverage <= 1));
    }

    [Fact]
    public void NumericHabits_ReportTotalAverageAndBest()
    {
        var water = NewHabit(HabitKind.Count, target: 8, start: Today.AddDays(-30));
        var logs = new List<DayLog>
        {
            WithStatus(water, Today, DayStatus.Done, 8),
            WithStatus(water, Today.AddDays(-1), DayStatus.Partial, 4),
            WithStatus(water, Today.AddDays(-2), DayStatus.Done, 12)
        };

        var row = InsightsCalculator.Build([water], logs, Today, Today.AddDays(-29)).Habits.Single();

        row.TotalValue.Should().Be(24);
        row.AveragePerActiveDay.Should().Be(8);
        row.BestValue.Should().Be(12);
        row.DoneCount.Should().Be(2);
    }

    [Fact]
    public void Mood_ComparesDoneDaysWithOtherDays()
    {
        var h = NewHabit(start: Today.AddDays(-30));
        var other = NewHabit(start: Today.AddDays(-30));
        var logs = new List<DayLog>
        {
            Done(h, Today) with { Mood = 5 },
            Done(h, Today.AddDays(-1)) with { Mood = 4 },
            WithStatus(other, Today.AddDays(-2), DayStatus.Partial) with { Mood = 2 }
        };

        var mood = InsightsCalculator.Build([h, other], logs, Today, Today.AddDays(-29)).Mood;

        mood.Samples.Should().Be(3);
        mood.DoneDaysAverage.Should().Be(4.5);
        mood.OtherDaysAverage.Should().Be(2);
    }

    [Fact]
    public void EmptyData_GivesAnEmptyButValidReport()
    {
        var report = InsightsCalculator.Build([], [], Today, Today.AddDays(-29));

        report.IsEmpty.Should().BeTrue();
        report.OverallRate.Should().BeNull();
        report.CheckIns.Should().Be(0);
    }

    [Fact]
    public void TwentyHabitsTwoYears_BuildsQuickly()
    {
        var habits = Enumerable.Range(0, 20).Select(_ => NewHabit(start: Today.AddDays(-730))).ToList();
        var random = new Random(3);
        var logs = habits.SelectMany(h => Enumerable.Range(0, 730).Where(_ => random.Next(100) < 70).Select(i => Done(h, Today.AddDays(-i)))).ToList();

        var best = TimeSpan.MaxValue;

        for (var i = 0; i < 3; i++)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            InsightsCalculator.Build(habits, logs, Today, Today.AddDays(-729));
            InsightEngine.Evaluate(habits, logs, Today);
            sw.Stop();
            best = sw.Elapsed < best ? sw.Elapsed : best;
        }

        // Ngân sách 300 ms trên máy người dùng; máy CI chậm và chạy song song nên cho dư.
        best.Should().BeLessThan(TimeSpan.FromSeconds(2));
    }
}

public class InsightRuleTests
{
    private static readonly IScheduleCalculator Schedule = new ScheduleCalculator();
    private static readonly IStatsCalculator Stats = new StatsCalculator();
    private static readonly IStreakCalculator Streaks = new StreakCalculator();

    [Fact]
    public void WeakWeekday_Positive_MissesMondays()
    {
        var h = NewHabit(start: Today.AddDays(-120));
        var logs = Enumerable.Range(1, 90).Select(i => Today.AddDays(-i))
            .Where(d => d.DayOfWeek != DayOfWeek.Monday)
            .Select(d => Done(h, d)).ToList();

        var insight = InsightEngine.WeakWeekday(h, logs, Today, Schedule).Single();

        insight.Key.Should().Be("weak_weekday");
        insight.Params["weekday"].Should().Be(((int)DayOfWeek.Monday).ToString());
    }

    [Fact]
    public void WeakWeekday_Negative_EvenMissesOrTooFewSamples()
    {
        var even = NewHabit(start: Today.AddDays(-120));
        var evenLogs = Enumerable.Range(1, 90).Where(i => i % 2 == 0).Select(i => Done(even, Today.AddDays(-i))).ToList();
        var young = NewHabit(start: Today.AddDays(-20));
        var youngLogs = Enumerable.Range(1, 20).Select(i => Today.AddDays(-i)).Where(d => d.DayOfWeek != DayOfWeek.Monday).Select(d => Done(young, d)).ToList();

        InsightEngine.WeakWeekday(even, evenLogs, Today, Schedule).Should().BeEmpty();
        InsightEngine.WeakWeekday(young, youngLogs, Today, Schedule).Should().BeEmpty("chỉ có ~3 thứ Hai, dưới 8 mẫu");
    }

    [Fact]
    public void MomentumUp_And_Slipping()
    {
        var up = NewHabit(start: Today.AddDays(-60));
        var upLogs = DoneRange(up, Today.AddDays(-14), Today.AddDays(-1));
        var down = NewHabit(start: Today.AddDays(-60));
        var downLogs = DoneRange(down, Today.AddDays(-28), Today.AddDays(-15));

        InsightEngine.Momentum(up, upLogs, Today, Stats).Single().Key.Should().Be("momentum_up");
        var slip = InsightEngine.Momentum(down, downLogs, Today, Stats).Single();
        slip.Key.Should().Be("slipping");
        slip.Params["delta"].Should().Be("100");
    }

    [Fact]
    public void Momentum_Negative_SteadyOrTooNew()
    {
        var steady = NewHabit(start: Today.AddDays(-60));
        var steadyLogs = DoneRange(steady, Today.AddDays(-28), Today.AddDays(-1));
        var young = NewHabit(start: Today.AddDays(-10));

        InsightEngine.Momentum(steady, steadyLogs, Today, Stats).Should().BeEmpty();
        InsightEngine.Momentum(young, DoneRange(young, Today.AddDays(-5), Today.AddDays(-1)), Today, Stats).Should().BeEmpty();
    }

    [Fact]
    public void NearMilestone_TwoDaysBefore7_ButNotWhenDoneTodayOrFar()
    {
        var h = NewHabit(start: Today.AddDays(-30));
        var five = DoneRange(h, Today.AddDays(-5), Today.AddDays(-1));

        var insight = InsightEngine.NearMilestone(h, five, Today, Streaks).Single();
        insight.Params["n"].Should().Be("2");
        insight.Params["milestone"].Should().Be("7");

        InsightEngine.NearMilestone(h, DoneRange(h, Today.AddDays(-5), Today), Today, Streaks).Should().BeEmpty("hôm nay đã xong");
        InsightEngine.NearMilestone(h, DoneRange(h, Today.AddDays(-2), Today.AddDays(-1)), Today, Streaks).Should().BeEmpty();
    }

    [Fact]
    public void MoodLink_Positive_AndNegativeWithoutEnoughSamples()
    {
        var h = NewHabit(start: Today.AddDays(-60));
        var other = NewHabit(start: Today.AddDays(-60));
        var logs = new List<DayLog>();
        for (var i = 1; i <= 20; i++)
        {
            logs.Add(i % 2 == 0
                ? Done(h, Today.AddDays(-i)) with { Mood = 5 }
                : WithStatus(other, Today.AddDays(-i), DayStatus.Partial) with { Mood = 2 });
        }

        InsightEngine.MoodLink(h, logs.Where(l => l.HabitId == h.Id).ToList(), logs, Today).Should().ContainSingle();
        InsightEngine.MoodLink(h, logs.Take(10).Where(l => l.HabitId == h.Id).ToList(), logs.Take(10).ToList(), Today).Should().BeEmpty();
    }

    [Fact]
    public void GoldenHour_Positive_AndNegativeWhenSpreadOut()
    {
        var h = NewHabit(start: Today.AddDays(-60));
        DateTime At(int day, int hour) => DateTime.SpecifyKind(Today.AddDays(-day).ToDateTime(new TimeOnly(hour, 10)), DateTimeKind.Local).ToUniversalTime();

        var focused = Enumerable.Range(1, 12).Select(i => Done(h, Today.AddDays(-i)) with { CheckedAt = At(i, 7 + i % 2) }).ToList();
        var spread = Enumerable.Range(1, 12).Select(i => Done(h, Today.AddDays(-i)) with { CheckedAt = At(i, i * 2 % 24) }).ToList();

        var golden = InsightEngine.GoldenHour(focused, Today).Single();
        golden.Params["start"].Should().Be("07:00");
        InsightEngine.GoldenHour(spread, Today).Should().BeEmpty();
        InsightEngine.GoldenHour(focused.Take(5).ToList(), Today).Should().BeEmpty("dưới 10 lần check");
    }

    [Fact]
    public void StackSuggestion_WhenTwoHabitsAreCheckedTogether()
    {
        var a = NewHabit(start: Today.AddDays(-60));
        a.Name = "Uống nước";
        var b = NewHabit(start: Today.AddDays(-60));
        b.Name = "Vitamin";
        var c = NewHabit(start: Today.AddDays(-60));
        DateTime At(int day, int minute) => DateTime.SpecifyKind(Today.AddDays(-day).ToDateTime(new TimeOnly(8, minute)), DateTimeKind.Local).ToUniversalTime();
        var logs = Enumerable.Range(1, 8).SelectMany(i => new[]
        {
            Done(a, Today.AddDays(-i)) with { CheckedAt = At(i, 0) },
            Done(b, Today.AddDays(-i)) with { CheckedAt = At(i, 5) },
            Done(c, Today.AddDays(-i)) with { CheckedAt = At(i, 0).AddHours(10) }
        }).ToList();
        var byHabit = logs.GroupBy(l => l.HabitId).ToDictionary(g => g.Key, g => (IReadOnlyList<DayLog>)g.ToList());

        var insight = InsightEngine.StackSuggestion([a, b, c], byHabit, Today).Single();

        insight.Params["a"].Should().Be("Uống nước");
        insight.Params["b"].Should().Be("Vitamin");
    }

    [Fact]
    public void Pick_TakesTop3DistinctKeys_AndHidesRecentlyShownOnes()
    {
        var id = Guid.NewGuid();
        var ranked = new[]
        {
            new Insight("near_milestone", 2, new Dictionary<string, string>(), id),
            new Insight("near_milestone", 1.9, new Dictionary<string, string>(), Guid.NewGuid()),
            new Insight("golden_hour", 1.5, new Dictionary<string, string>()),
            new Insight("momentum_up", 1, new Dictionary<string, string>(), id),
            new Insight("mood_link", 0.9, new Dictionary<string, string>(), id)
        };

        InsightEngine.Pick(ranked, new Dictionary<string, DateOnly>(), Today).Select(i => i.Key)
            .Should().Equal("near_milestone", "golden_hour", "momentum_up");

        var shown = new Dictionary<string, DateOnly> { [ranked[2].Identity] = Today.AddDays(-3), [ranked[0].Identity] = Today };
        InsightEngine.Pick(ranked, shown, Today).Select(i => i.Key)
            // golden_hour bị ẩn vì đã hiện 3 ngày trước; insight hiện lần đầu hôm nay vẫn được giữ.
            .Should().Equal("near_milestone", "momentum_up", "mood_link");
    }
}

public class CsvExporterTests
{
    [Fact]
    public void Export_HasHeaderAndOneRowPerDay_EscapesAndBlocksFormulas()
    {
        var h = NewHabit(HabitKind.Count, target: 8);
        h.Name = "Uống nước, nhiều";
        h.SortKey = "a";
        var logs = new List<DayLog>
        {
            WithStatus(h, Today, DayStatus.Done, 8) with { Note = "Nói \"tốt\"\nxuống dòng", Mood = 4 },
            WithStatus(h, Today.AddDays(-1), DayStatus.Partial, 3) with { Note = "=HYPERLINK(\"x\")" }
        };

        var csv = CsvExporter.Export([h], logs);
        var lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        lines[0].Should().Be("date,habit,kind,value,target,unit,status,note,mood");
        csv.Should().Contain("2026-10-21,\"Uống nước, nhiều\",count,3,8,,partial,\"'=HYPERLINK(\"\"x\"\")\",");
        csv.Should().Contain("\"Nói \"\"tốt\"\"\nxuống dòng\",4");
        csv.IndexOf("2026-10-21", StringComparison.Ordinal).Should().BeLessThan(csv.IndexOf("2026-10-22", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("a,b", "\"a,b\"")]
    [InlineData("+1", "'+1")]
    [InlineData("-cmd", "'-cmd")]
    [InlineData("@SUM", "'@SUM")]
    public void Field_EscapesAsExpected(string input, string expected) =>
        CsvExporter.Field(input).Should().Be(expected);
}
