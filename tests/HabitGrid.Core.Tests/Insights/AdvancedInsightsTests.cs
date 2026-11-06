using FluentAssertions;
using HabitGrid.Core.Domain;
using HabitGrid.Core.Domain.Insights;
using Xunit;
using static HabitGrid.Core.Tests.Domain.DomainTestData;

namespace HabitGrid.Core.Tests.Insights;

public class CorrelationTests
{
    private static Habit Named(string name)
    {
        var h = NewHabit(start: Today.AddDays(-120));
        h.Name = name;
        return h;
    }

    [Fact]
    public void FindsAPairThatMovesTogether_AndIgnoresIndependentOnes()
    {
        var gym = Named("Gym");
        var protein = Named("Protein");
        var reading = Named("Đọc sách");
        var logs = new List<DayLog>();

        // Gym xong vào các ngày chẵn; Protein luôn đi cùng Gym; Đọc sách làm ngày chia hết cho 3 (độc lập với tính chẵn lẻ).
        for (var i = 1; i <= 80; i++)
        {
            var day = Today.AddDays(-i);

            if (i % 2 == 0)
            {
                logs.Add(Done(gym, day));
                logs.Add(Done(protein, day));
            }

            if (i % 3 == 0)
            {
                logs.Add(Done(reading, day));
            }
        }

        var found = AdvancedInsights.Correlations([gym, protein, reading], logs, Today);

        found.Should().ContainSingle();
        found[0].Lift.Should().BeGreaterThan(0.9);
        new[] { found[0].A.Name, found[0].B.Name }.Should().BeEquivalentTo(["Gym", "Protein"]);
    }

    [Fact]
    public void NeedsEnoughDaysInBothGroups()
    {
        var a = Named("A");
        var b = Named("B");
        // Chỉ 10 ngày dữ liệu chung → dưới ngưỡng 14.
        var logs = Enumerable.Range(1, 10).SelectMany(i => new[] { Done(a, Today.AddDays(-i)), Done(b, Today.AddDays(-i)) }).ToList();
        var young = new[] { Named("Y1"), Named("Y2") };
        foreach (var y in young) y.StartDate = Today.AddDays(-10);

        AdvancedInsights.Correlations(young, logs, Today).Should().BeEmpty();
    }

    [Fact]
    public void FrozenAndSkippedDays_AreExcluded_SoAVacationDoesNotFakeACorrelation()
    {
        var a = Named("A");
        var b = Named("B");
        var logs = new List<DayLog>();

        for (var i = 1; i <= 60; i++)
        {
            var day = Today.AddDays(-i);
            logs.Add(i <= 30 ? WithStatus(a, day, DayStatus.Frozen) : (i % 2 == 0 ? Done(a, day) : WithStatus(a, day, DayStatus.None)));
            logs.Add(i <= 30 ? WithStatus(b, day, DayStatus.Frozen) : (i % 4 == 0 ? Done(b, day) : WithStatus(b, day, DayStatus.None)));
        }

        // Chỉ còn 30 ngày dùng được; A xong 15 ngày, B xong 7 ngày và luôn trùng A khi xong → vẫn tìm thấy, nhưng không dùng ngày nghỉ.
        var found = AdvancedInsights.Correlations([a, b], logs, Today);

        found.Should().OnlyContain(c => c.Days <= 30);
    }

    [Fact]
    public void QuitAndArchivedHabits_AreIgnored()
    {
        var a = Named("A");
        var quit = NewHabit(HabitKind.Quit, start: Today.AddDays(-100));
        var archived = Named("Cũ");
        archived.ArchivedAt = DateTime.UtcNow;
        var logs = Enumerable.Range(1, 60).Where(i => i % 2 == 0).SelectMany(i => new[] { Done(a, Today.AddDays(-i)), Done(archived, Today.AddDays(-i)) }).ToList();

        AdvancedInsights.Correlations([a, quit, archived], logs, Today).Should().BeEmpty();
    }
}

public class ComparisonTests
{
    [Fact]
    public void Compare_ReportsRatesStreaksDoneDaysAndTheOverlap()
    {
        var a = NewHabit(start: Today.AddDays(-60));
        var b = NewHabit(start: Today.AddDays(-60));
        var logsA = DoneRange(a, Today.AddDays(-9), Today);
        var logsB = DoneRange(b, Today.AddDays(-4), Today);

        var result = AdvancedInsights.Compare(a, b, logsA, logsB, Today, Today.AddDays(-9));

        result.StreakA.Should().Be(10);
        result.StreakB.Should().Be(5);
        result.DoneA.Should().Be(10);
        result.DoneB.Should().Be(5);
        result.BothDone.Should().Be(5);
        result.RateA.Should().Be(1);
        result.RateB.Should().BeApproximately(0.5, 0.001);
        result.Weeks.Should().HaveCount(AdvancedInsights.CompareWeeks);
        result.Weeks.Select(w => w.WeekStart.DayOfWeek).Should().OnlyContain(d => d == DayOfWeek.Monday);
        result.Weeks[^1].RateA.Should().Be(1);
    }
}

public class MonthComparisonTests
{
    [Fact]
    public void ComparesTheSameNumberOfDays_OfThisAndLastMonth()
    {
        // 2026-10-22: tháng này = 1..22/10; tháng trước = 1..22/9.
        var h = NewHabit(start: Today.AddDays(-100));
        var logs = new List<DayLog>();
        logs.AddRange(DoneRange(h, new DateOnly(2026, 10, 1), Today));
        logs.AddRange(DoneRange(h, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 11)));

        var result = AdvancedInsights.MonthOverMonth([h], logs, Today);

        result.ThisFrom.Should().Be(new DateOnly(2026, 10, 1));
        result.LastTo.Should().Be(new DateOnly(2026, 9, 22));
        result.OverallThis.Should().Be(1);
        result.OverallLast.Should().BeApproximately(11 / 22.0, 0.001);
        result.OverallPoints.Should().Be(50);
        result.CheckInsThis.Should().Be(22);
        result.CheckInsLast.Should().Be(11);
        result.Habits.Single().Points.Should().Be(50);
    }

    [Fact]
    public void TodayPending_IsNotAMiss_AndANewHabitHasNoLastMonth()
    {
        var h = NewHabit(start: new DateOnly(2026, 10, 10));
        var logs = DoneRange(h, new DateOnly(2026, 10, 10), Today.AddDays(-1));

        var result = AdvancedInsights.MonthOverMonth([h], logs, Today);

        result.OverallThis.Should().Be(1, "hôm nay chưa xong không phải là lỡ");
        result.OverallLast.Should().BeNull();
        result.OverallPoints.Should().BeNull();
    }
}

public class YearHeatmapTests
{
    [Fact]
    public void ColorsEachDayByTheShareOfDueHabitsDone()
    {
        var a = NewHabit(start: Today.AddDays(-30));
        var b = NewHabit(start: Today.AddDays(-30));
        var c = NewHabit(start: Today.AddDays(-30));
        var logs = new List<DayLog>
        {
            Done(a, Today.AddDays(-1)), Done(b, Today.AddDays(-1)), Done(c, Today.AddDays(-1)),
            Done(a, Today.AddDays(-2)), Done(b, Today.AddDays(-2)),
            Done(a, Today.AddDays(-3))
        };

        var heat = AdvancedInsights.YearHeatmap([a, b, c], logs, Today);
        HeatCell Cell(int ago) => heat.Cells.Single(x => x.Day == Today.AddDays(-ago));

        Cell(1).Level.Should().Be(4);
        Cell(2).Level.Should().Be(3, "2/3 = 67%");
        Cell(3).Level.Should().Be(1, "1/3 = 33% chưa tới ngưỡng 34% của mức 2");
    }

    [Fact]
    public void HasTheRightShape_FlagsFutureAndBeforeStart()
    {
        var h = NewHabit(start: Today.AddDays(-10));

        var heat = AdvancedInsights.YearHeatmap([h], DoneRange(h, Today.AddDays(-5), Today), Today);

        heat.Weeks.Should().HaveCount(52).And.OnlyContain(w => w.Count == 7);
        heat.Cells.Where(c => c.Day > Today).Should().OnlyContain(c => c.IsFuture && c.Level == 0);
        heat.Cells.Where(c => c.Day < h.StartDate).Should().OnlyContain(c => c.IsBeforeStart);
        heat.Cells.Single(c => c.IsToday).Level.Should().Be(4);
    }

    [Fact]
    public void FrozenDays_AreNotCountedAsDue_SoAVacationDoesNotShowAsAMiss()
    {
        var a = NewHabit(start: Today.AddDays(-30));
        var b = NewHabit(start: Today.AddDays(-30));
        var logs = new List<DayLog> { Done(a, Today.AddDays(-1)), WithStatus(b, Today.AddDays(-1), DayStatus.Frozen) };

        AdvancedInsights.YearHeatmap([a, b], logs, Today).Cells.Single(c => c.Day == Today.AddDays(-1)).Level.Should().Be(4);
    }
}
