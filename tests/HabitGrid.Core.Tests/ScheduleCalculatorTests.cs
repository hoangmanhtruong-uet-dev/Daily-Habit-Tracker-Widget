using FluentAssertions;
using HabitGrid.Core.Models;
using HabitGrid.Core.Services;
using Xunit;

namespace HabitGrid.Core.Tests;

/// <summary>
/// Bộ kiểm thử tính lịch lặp (chưa có trong danh sách bắt buộc nhưng cần cho độ tin cậy của thuật toán streak).
/// </summary>
public class ScheduleCalculatorTests
{
    private readonly ScheduleCalculator _sut = new();

    [Fact]
    public void IsScheduled_WithAllWeekMask_ShouldReturnTrueForEveryDay()
    {
        var habit = new Habit { Name = "A", ScheduleMask = DayOfWeekMask.AllWeek };

        for (var day = new DateOnly(2026, 10, 19); day <= new DateOnly(2026, 10, 25); day = day.AddDays(1))
        {
            _sut.IsScheduled(habit, day).Should().BeTrue();
        }
    }

    [Fact]
    public void IsScheduled_WithWeekdaysOnly_ShouldExcludeWeekend()
    {
        var habit = new Habit
        {
            Name = "A",
            ScheduleMask = DayOfWeekMask.Monday
                           | DayOfWeekMask.Tuesday
                           | DayOfWeekMask.Wednesday
                           | DayOfWeekMask.Thursday
                           | DayOfWeekMask.Friday
        };

        _sut.IsScheduled(habit, new DateOnly(2026, 10, 16)).Should().BeTrue();  // Thứ Sáu
        _sut.IsScheduled(habit, new DateOnly(2026, 10, 17)).Should().BeFalse(); // Thứ Bảy
        _sut.IsScheduled(habit, new DateOnly(2026, 10, 18)).Should().BeFalse(); // Chủ Nhật
        _sut.IsScheduled(habit, new DateOnly(2026, 10, 19)).Should().BeTrue();  // Thứ Hai
    }

    [Fact]
    public void IsScheduled_WithSundayOnly_ShouldMatchSunday()
    {
        var habit = new Habit { Name = "A", ScheduleMask = DayOfWeekMask.Sunday };

        _sut.IsScheduled(habit, new DateOnly(2026, 10, 18)).Should().BeTrue();
        _sut.IsScheduled(habit, new DateOnly(2026, 10, 19)).Should().BeFalse();
    }

    [Fact]
    public void CountScheduledDays_ShouldCountBothInclusiveEndpoints()
    {
        var habit = new Habit { Name = "A", ScheduleMask = DayOfWeekMask.AllWeek };

        _sut.CountScheduledDays(habit, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 22)).Should().Be(22);
    }

    [Fact]
    public void CountScheduledDays_WithWeekdaysOnly_ShouldExcludeWeekends()
    {
        var habit = new Habit
        {
            Name = "A",
            ScheduleMask = DayOfWeekMask.Monday
                           | DayOfWeekMask.Tuesday
                           | DayOfWeekMask.Wednesday
                           | DayOfWeekMask.Thursday
                           | DayOfWeekMask.Friday
        };

        // 01/10 (Thứ Năm) -> 31/10 (Thứ Bảy): 31 ngày, có 9 ngày cuối tuần (5 T7 + 4 CN).
        _sut.CountScheduledDays(habit, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)).Should().Be(22);
    }

    [Fact]
    public void CountScheduledDays_WithInvertedRange_ShouldReturnZero()
    {
        var habit = new Habit { Name = "A" };

        _sut.CountScheduledDays(habit, new DateOnly(2026, 10, 22), new DateOnly(2026, 10, 1)).Should().Be(0);
    }
}
