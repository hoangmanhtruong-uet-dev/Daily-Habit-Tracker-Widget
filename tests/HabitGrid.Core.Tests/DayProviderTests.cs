using FluentAssertions;
using HabitGrid.Core.Services;
using Xunit;

namespace HabitGrid.Core.Tests;

/// <summary>
/// Bộ kiểm thử ngày logic theo SPEC-01 mục 3.2.
/// </summary>
public class DayProviderTests
{
    [Fact]
    public void ToLogicalDate_BeforeCutoffHour_ShouldBelongToPreviousCalendarDay()
    {
        var now = new DateTime(2026, 10, 22, 1, 30, 0);
        var provider = new DayProvider(TimeSpan.FromHours(3), () => now);

        var result = provider.ToLogicalDate(now);

        result.Should().Be(new DateOnly(2026, 10, 21));
    }

    [Fact]
    public void ToLogicalDate_AfterCutoffHour_ShouldBelongToCurrentCalendarDay()
    {
        var now = new DateTime(2026, 10, 22, 3, 30, 0);
        var provider = new DayProvider(TimeSpan.FromHours(3), () => now);

        var result = provider.ToLogicalDate(now);

        result.Should().Be(new DateOnly(2026, 10, 22));
    }

    [Fact]
    public void ToLogicalDate_WithDefaultOffset_ShouldUseMidnightCutoff()
    {
        var provider = new DayProvider();

        provider.ToLogicalDate(new DateTime(2026, 10, 22, 0, 0, 0)).Should().Be(new DateOnly(2026, 10, 22));
        provider.ToLogicalDate(new DateTime(2026, 10, 22, 23, 59, 59)).Should().Be(new DateOnly(2026, 10, 22));
    }

    [Fact]
    public void ToLogicalDate_ExactlyAtCutoffHour_ShouldBelongToCurrentCalendarDay()
    {
        var now = new DateTime(2026, 10, 22, 3, 0, 0);
        var provider = new DayProvider(TimeSpan.FromHours(3), () => now);

        provider.ToLogicalDate(now).Should().Be(new DateOnly(2026, 10, 22));
    }

    [Fact]
    public void GetToday_ShouldFollowLogicalDay()
    {
        var now = new DateTime(2026, 10, 22, 2, 0, 0);
        var provider = new DayProvider(TimeSpan.FromHours(3), () => now);

        provider.GetToday().Should().Be(new DateOnly(2026, 10, 21));
    }

    [Fact]
    public void GetNextDayTransitionUtc_ShouldReturnMomentAfterLogicalDayEnds()
    {
        var now = new DateTime(2026, 10, 22, 1, 30, 0);
        var provider = new DayProvider(TimeSpan.FromHours(3), () => now);

        var transitionUtc = provider.GetNextDayTransitionUtc();

        // Ngày logic hiện tại là 21/10 -> ngày logic kế tiếp bắt đầu lúc 03:00 ngày 22/10 theo giờ địa phương.
        var expectedLocal = new DateTime(2026, 10, 22, 3, 0, 0, DateTimeKind.Local);
        var expectedUtc = TimeZoneInfo.ConvertTimeToUtc(expectedLocal, TimeZoneInfo.Local);

        transitionUtc.Should().Be(expectedUtc);
        transitionUtc.Should().BeAfter(now.ToUniversalTime());
    }

    [Fact]
    public void GetNextDayTransitionUtc_WhenAfterCutoff_ShouldMoveToNextCalendarDay()
    {
        var now = new DateTime(2026, 10, 22, 10, 0, 0);
        var provider = new DayProvider(TimeSpan.FromHours(3), () => now);

        var expectedLocal = new DateTime(2026, 10, 23, 3, 0, 0, DateTimeKind.Local);
        var expectedUtc = TimeZoneInfo.ConvertTimeToUtc(expectedLocal, TimeZoneInfo.Local);

        provider.GetNextDayTransitionUtc().Should().Be(expectedUtc);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(6)]
    public void Constructor_WithOffsetOutOfAllowedRange_ShouldThrow(int hours)
    {
        var act = () => new DayProvider(TimeSpan.FromHours(hours));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
