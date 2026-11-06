using FluentAssertions;
using HabitGrid.Core.Domain;
using HabitGrid.Core.Domain.Insights;
using Xunit;
using static HabitGrid.Core.Tests.Domain.DomainTestData;

namespace HabitGrid.Core.Tests.Insights;

public class WeeklyRecapTests
{
    // Today = thứ Năm 22/10/2026 → tuần trước = thứ Hai 12/10 … Chủ nhật 18/10; tuần trước đó = 05/10 … 11/10.
    private static readonly DateOnly LastMon = new(2026, 10, 12);
    private static readonly DateOnly LastSun = new(2026, 10, 18);

    private static Habit Named(string name, int startDaysAgo = 60)
    {
        var h = NewHabit(start: Today.AddDays(-startDaysAgo));
        h.Name = name;
        return h;
    }

    [Fact]
    public void CoversTheFullPreviousWeek_NotTheRunningOne()
    {
        var h = Named("A");

        var recap = WeeklyRecapBuilder.Build([h], DoneRange(h, LastMon, Today), Today);

        recap.WeekStart.Should().Be(LastMon);
        recap.WeekEnd.Should().Be(LastSun);
        recap.CheckIns.Should().Be(7, "những ngày tuần này không được đếm vào tuần trước");
    }

    [Fact]
    public void PerfectWeek_IsPraised_WithBestDayAndPerfectDays()
    {
        var a = Named("A");
        var b = Named("B");
        var logs = DoneRange(a, LastMon, LastSun).Concat(DoneRange(b, LastMon, LastSun)).ToList();

        var recap = WeeklyRecapBuilder.Build([a, b], logs, Today);

        recap.Rate.Should().Be(1);
        recap.PerfectDays.Should().Be(7);
        recap.Tip.Should().Be(RecapTipKind.Praise);
        recap.BestDay.Should().Be(LastMon, "hòa thì lấy ngày sớm nhất");
        recap.Points.Should().Be(100, "tuần trước đó có lịch nhưng chưa làm gì (0%)");
    }

    [Fact]
    public void AClearlyWeakHabit_IsSuggested_AsTheFocus()
    {
        var strong = Named("Mạnh");
        var weak = Named("Yếu");
        var logs = DoneRange(strong, LastMon, LastSun);
        logs.Add(Done(weak, LastMon));

        var recap = WeeklyRecapBuilder.Build([strong, weak], logs, Today);

        recap.Tip.Should().Be(RecapTipKind.FocusWeakest);
        recap.WeakestHabit!.Name.Should().Be("Yếu");
        recap.BestHabit!.Name.Should().Be("Mạnh");
    }

    [Fact]
    public void AWeakHabitWithTooFewDueDays_IsNotNagged()
    {
        var strong = Named("Mạnh");
        var newcomer = Named("Mới", startDaysAgo: 5);
        newcomer.StartDate = LastSun.AddDays(-1); // chỉ 2 ngày đến hạn trong tuần
        var logs = DoneRange(strong, LastMon, LastSun);

        WeeklyRecapBuilder.Build([strong, newcomer], logs, Today).WeakestHabit.Should().BeNull();
    }

    [Fact]
    public void NoCheckIns_MeansNoData_AndQuitHabitsAreIgnored()
    {
        var quit = NewHabit(HabitKind.Quit, start: Today.AddDays(-60));
        var h = Named("A");

        var recap = WeeklyRecapBuilder.Build([quit, h], [], Today);

        recap.Tip.Should().Be(RecapTipKind.NoData);
        recap.CheckIns.Should().Be(0);
        recap.BestDay.Should().BeNull();
    }

    [Fact]
    public void ComparesWithTheWeekBefore()
    {
        var h = Named("A");
        var logs = DoneRange(h, LastMon, LastMon.AddDays(4));           // 5/7 tuần trước
        logs.AddRange(DoneRange(h, LastMon.AddDays(-7), LastMon.AddDays(-7))); // 1/7 tuần trước đó

        var recap = WeeklyRecapBuilder.Build([h], logs, Today);

        recap.Rate.Should().BeApproximately(5 / 7.0, 0.001);
        recap.PreviousRate.Should().BeApproximately(1 / 7.0, 0.001);
        recap.Points.Should().Be(57);
        recap.Tip.Should().Be(RecapTipKind.Improving);
    }

    [Fact]
    public void FrozenDays_DoNotHurtTheRate()
    {
        var h = Named("A");
        var logs = DoneRange(h, LastMon, LastMon.AddDays(3));
        logs.AddRange(Enumerable.Range(4, 3).Select(i => WithStatus(h, LastMon.AddDays(i), DayStatus.Frozen)));

        WeeklyRecapBuilder.Build([h], logs, Today).Rate.Should().Be(1);
    }
}
