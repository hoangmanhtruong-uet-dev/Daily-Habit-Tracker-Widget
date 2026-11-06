using FluentAssertions;
using HabitGrid.Core.Domain;
using HabitGrid.Core.Domain.Insights;
using Xunit;
using static HabitGrid.Core.Tests.Domain.DomainTestData;

namespace HabitGrid.Core.Tests.Insights;

public class MonthlyWrappedTests
{
    // Today = 22/10/2026. Tháng 9 đã đầy đủ; tháng 10 mới đến ngày 22.
    private static readonly DateOnly Sep = new(2026, 9, 15);
    private static readonly DateOnly Oct = new(2026, 10, 1);

    private static Habit Named(string name)
    {
        var h = NewHabit(start: new DateOnly(2026, 6, 1));
        h.Name = name;
        return h;
    }

    [Fact]
    public void CountsCheckIns_ActiveDays_AndPicksTheTopHabit()
    {
        var read = Named("Đọc sách");
        var run = Named("Chạy bộ");
        var logs = DoneRange(read, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10)).Concat(DoneRange(run, new DateOnly(2026, 9, 5), new DateOnly(2026, 9, 7))).ToList();

        var w = MonthlyWrappedBuilder.Build([read, run], logs, Sep, Today);

        w.Year.Should().Be(2026);
        w.Month.Should().Be(9);
        w.To.Should().Be(new DateOnly(2026, 9, 30), "tháng đã trọn vẹn");
        w.TotalCheckIns.Should().Be(13);
        w.ActiveDays.Should().Be(10);
        w.TopHabit!.Name.Should().Be("Đọc sách");
        w.TopHabit.Count.Should().Be(10);
        w.IsComplete(Today).Should().BeTrue();
    }

    [Fact]
    public void LongestStreak_IsTheLongestRunInsideTheMonth_AndNeedsAtLeastTwoDays()
    {
        var a = Named("A");
        var b = Named("B");
        var logs = new List<DayLog>();
        logs.AddRange(DoneRange(a, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 3)));
        logs.AddRange(DoneRange(a, new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 16)));
        logs.AddRange(DoneRange(b, new DateOnly(2026, 9, 20), new DateOnly(2026, 9, 22)));

        var w = MonthlyWrappedBuilder.Build([a, b], logs, Sep, Today);

        w.LongestStreak!.Name.Should().Be("A");
        w.LongestStreak.Count.Should().Be(7);

        var single = MonthlyWrappedBuilder.Build([a], [Done(a, new DateOnly(2026, 9, 3)), Done(a, new DateOnly(2026, 9, 9))], Sep, Today);
        single.LongestStreak.Should().BeNull("một ngày lẻ không phải chuỗi");
    }

    [Fact]
    public void RunningMonth_StopsAtToday_AndComparesWithTheSameNumberOfDaysLastMonth()
    {
        var h = Named("A");
        var logs = DoneRange(h, Oct, Today).Concat(DoneRange(h, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 11))).ToList();

        var w = MonthlyWrappedBuilder.Build([h], logs, Oct, Today);

        w.To.Should().Be(Today);
        w.TotalCheckIns.Should().Be(22);
        w.CheckInsDelta.Should().Be(22 - 11);
        w.IsComplete(Today).Should().BeFalse();
    }

    [Fact]
    public void PerfectDays_AreCounted_AndQuitHabitsAreIgnored()
    {
        var a = Named("A");
        var b = Named("B");
        var quit = NewHabit(HabitKind.Quit, start: new DateOnly(2026, 6, 1));
        var logs = DoneRange(a, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 5)).Concat(DoneRange(b, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 3))).ToList();

        var w = MonthlyWrappedBuilder.Build([a, b, quit], logs, Sep, Today);

        w.PerfectDays.Should().Be(3);
    }

    [Fact]
    public void GoldenHour_FollowsWhenCheckInsHappen()
    {
        var h = Named("A");
        var utc = TimeZoneInfo.Utc;
        DayLog At(int day, int hour) => Done(h, new DateOnly(2026, 9, day)) with { CheckedAt = new DateTime(2026, 9, day, hour, 30, 0, DateTimeKind.Utc) };

        var w = MonthlyWrappedBuilder.Build([h], [At(1, 7), At(2, 7), At(3, 7), At(4, 21)], Sep, Today, utc);

        w.GoldenHour.Should().Be(7);
    }

    [Fact]
    public void NoData_IsEmpty_WithoutCrashing()
    {
        var h = Named("A");

        var w = MonthlyWrappedBuilder.Build([h], [], Sep, Today);

        w.IsEmpty.Should().BeTrue();
        w.TopHabit.Should().BeNull();
        w.GoldenHour.Should().BeNull();
        w.CheckInsDelta.Should().BeNull();
    }

    [Theory]
    [InlineData(0, false, true)]
    [InlineData(1, false, false)]
    [InlineData(5, false, false)]
    [InlineData(0, true, true)]
    [InlineData(5, true, true)]
    public void FreeSeesOnlyTheFirstSlide(int index, bool plus, bool expected) =>
        WrappedSlides.CanView(index, plus).Should().Be(expected);

    [Fact]
    public void ThereAreSixSlides() => WrappedSlides.All.Should().HaveCount(6);
}
