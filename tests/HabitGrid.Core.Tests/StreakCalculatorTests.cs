using FluentAssertions;
using HabitGrid.Core.Models;
using HabitGrid.Core.Services;
using Xunit;

namespace HabitGrid.Core.Tests;

/// <summary>
/// Bộ kiểm thử thuật toán tính streak theo SPEC-01 mục 3.1.
/// </summary>
public class StreakCalculatorTests
{
    private readonly ScheduleCalculator _scheduleCalculator = new();
    private readonly StreakCalculator _sut;

    public StreakCalculatorTests()
    {
        _sut = new StreakCalculator(_scheduleCalculator);
    }

    // Ngày tham chiếu: 2026-10-22 là thứ Năm.
    private static readonly DateOnly Today = new(2026, 10, 22);

    [Fact]
    public void Calculate_WhenNoEntries_ShouldReturnZeroStreak()
    {
        var habit = CreateHabit();

        var result = _sut.Calculate(habit, [], Today);

        result.CurrentStreak.Should().Be(0);
        result.LongestStreak.Should().Be(0);
        result.CompletionRate30Days.Should().Be(0.0);
        result.TotalCompletedDays.Should().Be(0);
    }

    [Fact]
    public void Calculate_WhenCompletedToday_ShouldIncludeTodayInStreak()
    {
        var habit = CreateHabit();
        var entries = new List<Entry>
        {
            EntryFor(habit, new DateOnly(2026, 10, 20)),
            EntryFor(habit, new DateOnly(2026, 10, 21)),
            EntryFor(habit, Today)
        };

        var result = _sut.Calculate(habit, entries, Today);

        result.CurrentStreak.Should().Be(3);
        result.LongestStreak.Should().Be(3);
        result.TotalCompletedDays.Should().Be(3);
    }

    [Fact]
    public void Calculate_WhenTodayNotCompletedYet_ShouldKeepStreakFromYesterday()
    {
        var habit = CreateHabit();
        var entries = new List<Entry>
        {
            EntryFor(habit, new DateOnly(2026, 10, 20)),
            EntryFor(habit, new DateOnly(2026, 10, 21))
            // Hôm nay chưa đánh dấu — chuỗi chưa bị gãy.
        };

        var result = _sut.Calculate(habit, entries, Today);

        result.CurrentStreak.Should().Be(2);
    }

    [Fact]
    public void Calculate_WhenYesterdayMissed_ShouldBreakCurrentStreakToZero()
    {
        var habit = CreateHabit();
        var entries = new List<Entry>
        {
            EntryFor(habit, new DateOnly(2026, 10, 20))
            // 21/10 bị bỏ lỡ, 22/10 chưa đánh dấu.
        };

        var result = _sut.Calculate(habit, entries, Today);

        result.CurrentStreak.Should().Be(0);
        result.LongestStreak.Should().Be(1);
    }

    [Fact]
    public void Calculate_WithWeekendOffSchedule_ShouldSkipWeekendsWithoutBreakingStreak()
    {
        // Chỉ làm từ thứ Hai đến thứ Sáu.
        var habit = CreateHabit(scheduleMask: DayOfWeekMask.Monday
                                           | DayOfWeekMask.Tuesday
                                           | DayOfWeekMask.Wednesday
                                           | DayOfWeekMask.Thursday
                                           | DayOfWeekMask.Friday);

        var entries = new List<Entry>
        {
            EntryFor(habit, new DateOnly(2026, 10, 16)), // Thứ Sáu
            // 17/10 (T7) và 18/10 (CN) không thuộc lịch.
            EntryFor(habit, new DateOnly(2026, 10, 19)), // Thứ Hai
            EntryFor(habit, new DateOnly(2026, 10, 20)), // Thứ Ba
            EntryFor(habit, new DateOnly(2026, 10, 21)), // Thứ Tư
            EntryFor(habit, Today)                       // Thứ Năm
        };

        var result = _sut.Calculate(habit, entries, Today);

        result.CurrentStreak.Should().Be(5);
        result.LongestStreak.Should().Be(5);
    }

    [Fact]
    public void Calculate_ForCountHabit_WhenValueLessThanTarget_ShouldNotCountAsCompleted()
    {
        var habit = CreateHabit(kind: HabitKind.Count, target: 8);
        var entries = new List<Entry>
        {
            EntryFor(habit, Today, value: 7)
        };

        var result = _sut.Calculate(habit, entries, Today);

        result.CurrentStreak.Should().Be(0);
        result.TotalCompletedDays.Should().Be(0);
    }

    [Fact]
    public void Calculate_ForCountHabit_WhenValueReachesOrExceedsTarget_ShouldCountAsCompleted()
    {
        var habit = CreateHabit(kind: HabitKind.Count, target: 8);
        var entries = new List<Entry>
        {
            EntryFor(habit, new DateOnly(2026, 10, 21), value: 8),
            EntryFor(habit, Today, value: 10)
        };

        var result = _sut.Calculate(habit, entries, Today);

        result.CurrentStreak.Should().Be(2);
        result.TotalCompletedDays.Should().Be(2);
    }

    [Fact]
    public void Calculate_LongestStreak_WhenMultipleStreaksExist_ShouldReturnMaximum()
    {
        var habit = CreateHabit();
        var entries = new List<Entry>
        {
            // Chuỗi 1: 12/10 -> 14/10 (3 ngày)
            EntryFor(habit, new DateOnly(2026, 10, 12)),
            EntryFor(habit, new DateOnly(2026, 10, 13)),
            EntryFor(habit, new DateOnly(2026, 10, 14)),
            // 15/10 bị bỏ lỡ — chuỗi bị cắt.
            // Chuỗi 2: 16/10 (T6) và 19/10 -> 22/10 (bỏ qua cuối tuần) = 5 ngày
            EntryFor(habit, new DateOnly(2026, 10, 16)),
            EntryFor(habit, new DateOnly(2026, 10, 19)),
            EntryFor(habit, new DateOnly(2026, 10, 20)),
            EntryFor(habit, new DateOnly(2026, 10, 21)),
            EntryFor(habit, Today)
        };

        var result = _sut.Calculate(habit, entries, Today);

        // Lịch AllWeek nên 17/10 (T7) và 18/10 (CN) CŨNG là ngày có lịch và đã bị bỏ lỡ -> cắt chuỗi.
        // Các chuỗi tạo thành: 12-14/10 (3 ngày), 16/10 (1 ngày), 19-22/10 (4 ngày) -> kỷ lục là 4.
        result.LongestStreak.Should().Be(4);
        result.CurrentStreak.Should().Be(4);
        result.TotalCompletedDays.Should().Be(8);
    }

    [Fact]
    public void Calculate_30DayRate_ShouldCalculateAccurateRatio()
    {
        // Bắt đầu 01/10/2026 => cửa sổ 30 ngày bị chặn còn 22 ngày có lịch (01/10 -> 22/10).
        var habit = CreateHabit(startDate: new DateOnly(2026, 10, 1));

        var entries = new List<Entry>();
        for (var day = 1; day <= 11; day++)
        {
            entries.Add(EntryFor(habit, new DateOnly(2026, 10, day)));
        }

        var result = _sut.Calculate(habit, entries, Today);

        result.CompletionRate30Days.Should().Be(11d / 22d);
        result.TotalCompletedDays.Should().Be(11);
    }

    [Fact]
    public void Calculate_WhenTodayNotScheduled_ShouldFallBackToNearestScheduledDay()
    {
        // Chỉ làm thứ Hai, Thứ Tư, Thứ Sáu.
        var habit = CreateHabit(scheduleMask: DayOfWeekMask.Monday
                                           | DayOfWeekMask.Wednesday
                                           | DayOfWeekMask.Friday);

        var entries = new List<Entry>
        {
            EntryFor(habit, new DateOnly(2026, 10, 19)), // Thứ Hai
            EntryFor(habit, new DateOnly(2026, 10, 21))  // Thứ Tư
        };

        var result = _sut.Calculate(habit, entries, Today);

        // Hôm nay là thứ Năm (không có lịch) -> chuỗi tính từ 21/10.
        result.CurrentStreak.Should().Be(2);
    }

    [Fact]
    public void Calculate_WhenMarkedOnOffDay_ShouldCountAsCompletedButNotBreakStreak()
    {
        // Chỉ làm từ thứ Hai đến thứ Sáu.
        var habit = CreateHabit(scheduleMask: DayOfWeekMask.Monday
                                           | DayOfWeekMask.Tuesday
                                           | DayOfWeekMask.Wednesday
                                           | DayOfWeekMask.Thursday
                                           | DayOfWeekMask.Friday);

        var entries = new List<Entry>
        {
            EntryFor(habit, new DateOnly(2026, 10, 16)), // Thứ Sáu
            EntryFor(habit, new DateOnly(2026, 10, 18)), // Chủ Nhật - ngoài lịch
            EntryFor(habit, new DateOnly(2026, 10, 19)), // Thứ Hai
            EntryFor(habit, new DateOnly(2026, 10, 20)), // Thứ Ba
            EntryFor(habit, new DateOnly(2026, 10, 21)), // Thứ Tư
            EntryFor(habit, Today)                       // Thứ Năm
        };

        var result = _sut.Calculate(habit, entries, Today);

        result.CurrentStreak.Should().Be(5, "ngày ngoài lịch không được làm gãy chuỗi ngày có lịch");
        result.TotalCompletedDays.Should().Be(6, "ngày ngoài lịch vẫn được tính vào tổng số lần hoàn thành");
    }

    [Fact]
    public void Calculate_ShouldIgnoreEntriesBeforeStartDate()
    {
        var habit = CreateHabit(startDate: new DateOnly(2026, 10, 19));
        var entries = new List<Entry>
        {
            EntryFor(habit, new DateOnly(2026, 10, 15)),
            EntryFor(habit, new DateOnly(2026, 10, 16)),
            EntryFor(habit, new DateOnly(2026, 10, 19)),
            EntryFor(habit, new DateOnly(2026, 10, 20)),
            EntryFor(habit, new DateOnly(2026, 10, 21)),
            EntryFor(habit, Today)
        };

        var result = _sut.Calculate(habit, entries, Today);

        result.CurrentStreak.Should().Be(4);
        result.TotalCompletedDays.Should().Be(4);
    }

    [Fact]
    public void Calculate_WhenBooleanEntryHasZeroValue_ShouldNotCountAsCompleted()
    {
        var habit = CreateHabit();
        var entries = new List<Entry>
        {
            EntryFor(habit, new DateOnly(2026, 10, 21), value: 0),
            EntryFor(habit, Today, value: 0)
        };

        var result = _sut.Calculate(habit, entries, Today);

        result.CurrentStreak.Should().Be(0);
        result.TotalCompletedDays.Should().Be(0);
    }

    private static Habit CreateHabit(
        HabitKind kind = HabitKind.Boolean,
        int target = 1,
        DayOfWeekMask scheduleMask = DayOfWeekMask.AllWeek,
        DateOnly? startDate = null)
    {
        return new Habit
        {
            Name = "Uống nước",
            Kind = kind,
            Target = target,
            ScheduleMask = scheduleMask,
            StartDate = startDate ?? Today.AddDays(-30)
        };
    }

    private static Entry EntryFor(Habit habit, DateOnly day, int value = 1)
    {
        return new Entry
        {
            HabitId = habit.Id,
            Day = day,
            Value = value,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
