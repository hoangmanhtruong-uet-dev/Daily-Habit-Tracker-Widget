using FluentAssertions;
using HabitGrid.Core.Domain;
using Xunit;
using static HabitGrid.Core.Tests.Domain.DomainTestData;

namespace HabitGrid.Core.Tests.Domain;

public class StreakCalculatorV2Tests
{
    private readonly StreakCalculator _sut = new();

    [Fact]
    public void Daily_NoLogs_IsZero()
    {
        var habit = NewHabit();

        var result = _sut.Calculate(habit, [], Today);

        result.Current.Should().Be(0);
        result.Best.Should().Be(0);
        result.Unit.Should().Be(StreakUnit.Day);
        result.AtRiskToday.Should().BeFalse();
    }

    [Fact]
    public void Daily_TodayPending_DoesNotBreakStreakAndIsAtRisk()
    {
        var habit = NewHabit();
        var logs = DoneRange(habit, Today.AddDays(-5), Today.AddDays(-1));

        var result = _sut.Calculate(habit, logs, Today);

        result.Current.Should().Be(5);
        result.AtRiskToday.Should().BeTrue();
        result.LastCompleted.Should().Be(Today.AddDays(-1));
    }

    [Fact]
    public void Daily_TodayDone_CountsToday()
    {
        var habit = NewHabit();
        var logs = DoneRange(habit, Today.AddDays(-2), Today);

        var result = _sut.Calculate(habit, logs, Today);

        result.Current.Should().Be(3);
        result.AtRiskToday.Should().BeFalse();
    }

    [Fact]
    public void Daily_MissedYesterday_BreaksStreak()
    {
        var habit = NewHabit();
        var logs = DoneRange(habit, Today.AddDays(-10), Today.AddDays(-3));
        logs.Add(Done(habit, Today));

        var result = _sut.Calculate(habit, logs, Today);

        result.Current.Should().Be(1);
        result.Best.Should().Be(8);
    }

    [Fact]
    public void WeekDays_OffDaysDoNotBreakStreak()
    {
        // Thứ 2, 4, 6. 2026-10-22 là thứ 5 nên không có lịch.
        var habit = NewHabit(frequency: new HabitFrequency.WeekDays(
            Mask(DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday)));

        var logs = new List<DayLog>
        {
            Done(habit, new DateOnly(2026, 10, 19)), // Mon
            Done(habit, new DateOnly(2026, 10, 21)), // Wed
            Done(habit, new DateOnly(2026, 10, 16)), // Fri
        };

        var result = _sut.Calculate(habit, logs, Today);

        result.Current.Should().Be(3);
    }

    [Fact]
    public void Skipped_DoesNotBreakAndDoesNotCount()
    {
        var habit = NewHabit();
        var logs = DoneRange(habit, Today.AddDays(-4), Today.AddDays(-3));
        logs.Add(WithStatus(habit, Today.AddDays(-2), DayStatus.Skipped));
        logs.AddRange(DoneRange(habit, Today.AddDays(-1), Today));

        var result = _sut.Calculate(habit, logs, Today);

        result.Current.Should().Be(4, "ngày bỏ qua không đứt chuỗi và không được cộng");
    }

    [Fact]
    public void Frozen_KeepsStreakAlive()
    {
        var habit = NewHabit();
        var logs = DoneRange(habit, Today.AddDays(-6), Today.AddDays(-3));
        logs.Add(WithStatus(habit, Today.AddDays(-2), DayStatus.Frozen));
        logs.Add(Done(habit, Today.AddDays(-1)));

        var result = _sut.Calculate(habit, logs, Today);

        result.Current.Should().Be(5);
    }

    [Fact]
    public void Partial_DoesNotCountAsDone()
    {
        var habit = NewHabit(HabitKind.Count, target: 8);
        var logs = new List<DayLog>
        {
            Done(habit, Today.AddDays(-2)),
            WithStatus(habit, Today.AddDays(-1), DayStatus.Partial, 5),
            Done(habit, Today)
        };

        var result = _sut.Calculate(habit, logs, Today);

        result.Current.Should().Be(1);
    }

    [Fact]
    public void TodayBeforeStartDate_IsZero()
    {
        var habit = NewHabit(start: Today.AddDays(3));

        _sut.Calculate(habit, [], Today).Current.Should().Be(0);
    }

    [Fact]
    public void LogsOutsideRange_AreIgnored()
    {
        var habit = NewHabit(start: Today.AddDays(-2));
        var logs = new List<DayLog>
        {
            Done(habit, Today.AddDays(-30)),
            Done(habit, Today.AddDays(5))
        };

        _sut.Calculate(habit, logs, Today).Current.Should().Be(0);
    }

    [Fact]
    public void DuplicateDay_UsesMostRecentlyUpdated()
    {
        var habit = NewHabit();
        var older = Done(habit, Today) with { UpdatedAt = new DateTime(2026, 10, 22, 8, 0, 0, DateTimeKind.Utc) };
        var newer = WithStatus(habit, Today, DayStatus.None) with
        {
            UpdatedAt = new DateTime(2026, 10, 22, 9, 0, 0, DateTimeKind.Utc)
        };

        _sut.Calculate(habit, [older, newer], Today).Current.Should().Be(0);
    }

    // ---- X lần / tuần ----

    [Fact]
    public void TimesPerWeek_CountsWeeks()
    {
        var habit = NewHabit(frequency: new HabitFrequency.TimesPerWeek(3), start: new DateOnly(2026, 9, 28));
        var logs = new List<DayLog>();

        // Tuần 28/9–4/10: 3 lần. Tuần 5/10–11/10: 3 lần. Tuần 12/10–18/10: 2 lần (hỏng).
        // Tuần 19/10–25/10 (hiện tại): 3 lần xong.
        foreach (var d in new[] { 28, 30 }.Select(x => new DateOnly(2026, 9, x)).Append(new DateOnly(2026, 10, 2)))
        {
            logs.Add(Done(habit, d));
        }

        foreach (var d in new[] { 5, 7, 9 }.Select(x => new DateOnly(2026, 10, x)))
        {
            logs.Add(Done(habit, d));
        }

        foreach (var d in new[] { 12, 14 }.Select(x => new DateOnly(2026, 10, x)))
        {
            logs.Add(Done(habit, d));
        }

        foreach (var d in new[] { 19, 20, 21 }.Select(x => new DateOnly(2026, 10, x)))
        {
            logs.Add(Done(habit, d));
        }

        var result = _sut.Calculate(habit, logs, Today);

        result.Unit.Should().Be(StreakUnit.Week);
        result.Current.Should().Be(1, "tuần 12–18/10 chỉ đạt 2/3 nên chuỗi đứt, chỉ còn tuần hiện tại");
        result.Best.Should().Be(2);
    }

    [Fact]
    public void TimesPerWeek_CurrentWeekNotYetMet_DoesNotBreak()
    {
        var habit = NewHabit(frequency: new HabitFrequency.TimesPerWeek(3), start: new DateOnly(2026, 10, 5));
        var logs = new List<DayLog>
        {
            Done(habit, new DateOnly(2026, 10, 5)),
            Done(habit, new DateOnly(2026, 10, 6)),
            Done(habit, new DateOnly(2026, 10, 7)),
            Done(habit, new DateOnly(2026, 10, 12)),
            Done(habit, new DateOnly(2026, 10, 13)),
            Done(habit, new DateOnly(2026, 10, 14)),
            Done(habit, new DateOnly(2026, 10, 19)) // tuần hiện tại mới 1/3
        };

        var result = _sut.Calculate(habit, logs, Today);

        result.Current.Should().Be(2);
        result.AtRiskToday.Should().BeTrue();
    }

    [Fact]
    public void TimesPerWeek_FirstPartialWeek_RequiresOnlyAvailableDays()
    {
        // Bắt đầu thứ Bảy 17/10 → tuần đầu chỉ còn 2 ngày, yêu cầu tối đa 2 dù mục tiêu 4.
        var habit = NewHabit(frequency: new HabitFrequency.TimesPerWeek(4), start: new DateOnly(2026, 10, 17));
        var logs = new List<DayLog>
        {
            Done(habit, new DateOnly(2026, 10, 17)),
            Done(habit, new DateOnly(2026, 10, 18))
        };

        var result = _sut.Calculate(habit, logs, new DateOnly(2026, 10, 18));

        result.Current.Should().Be(1);
    }

    [Fact]
    public void TimesPerWeek_RespectsConfiguredWeekStart()
    {
        // Tuần bắt đầu Chủ nhật: 18/10 (CN) thuộc tuần 18–24, cùng tuần với 21/10.
        var sunday = new StreakCalculator(DayOfWeek.Sunday);
        var habit = NewHabit(frequency: new HabitFrequency.TimesPerWeek(2), start: new DateOnly(2026, 10, 18));
        var logs = new List<DayLog>
        {
            Done(habit, new DateOnly(2026, 10, 18)),
            Done(habit, new DateOnly(2026, 10, 21))
        };

        sunday.Calculate(habit, logs, Today).Current.Should().Be(1);

        // Tuần bắt đầu Thứ Hai: 18/10 (CN) thuộc tuần trước chỉ có 1 ngày khả dụng → đủ; 21/10 tuần này 1/2 chưa đủ.
        var monday = new StreakCalculator(DayOfWeek.Monday);
        var m = monday.Calculate(habit, logs, Today);
        m.Current.Should().Be(1);
        m.AtRiskToday.Should().BeTrue();
    }

    // ---- X lần / tháng ----

    [Fact]
    public void TimesPerMonth_FebruaryLeapYear_UsesActualMonthLength()
    {
        // 2028 nhuận: tháng 2 có 29 ngày.
        var habit = NewHabit(frequency: new HabitFrequency.TimesPerMonth(2), start: new DateOnly(2028, 1, 1));
        var logs = new List<DayLog>
        {
            Done(habit, new DateOnly(2028, 1, 3)), Done(habit, new DateOnly(2028, 1, 4)),
            Done(habit, new DateOnly(2028, 2, 28)), Done(habit, new DateOnly(2028, 2, 29)),
            Done(habit, new DateOnly(2028, 3, 1)), Done(habit, new DateOnly(2028, 3, 2))
        };

        var result = _sut.Calculate(habit, logs, new DateOnly(2028, 3, 15));

        result.Unit.Should().Be(StreakUnit.Month);
        result.Current.Should().Be(3);
    }

    [Fact]
    public void TimesPerMonth_MissedMonth_BreaksStreak()
    {
        var habit = NewHabit(frequency: new HabitFrequency.TimesPerMonth(2), start: new DateOnly(2026, 8, 1));
        var logs = new List<DayLog>
        {
            Done(habit, new DateOnly(2026, 8, 5)), Done(habit, new DateOnly(2026, 8, 6)),
            Done(habit, new DateOnly(2026, 9, 5)), // tháng 9 chỉ 1/2
            Done(habit, new DateOnly(2026, 10, 1)), Done(habit, new DateOnly(2026, 10, 2))
        };

        var result = _sut.Calculate(habit, logs, Today);

        result.Current.Should().Be(1);
        result.Best.Should().Be(1);
    }

    // ---- Mỗi N ngày ----

    [Fact]
    public void EveryNDays_CountsWindows()
    {
        var anchor = new DateOnly(2026, 10, 1);
        var habit = NewHabit(frequency: new HabitFrequency.EveryNDays(3, anchor), start: anchor);
        // Cửa sổ: [1–3] [4–6] [7–9] [10–12] [13–15] [16–18] [19–21] [22–24]
        var logs = new List<DayLog>
        {
            Done(habit, new DateOnly(2026, 10, 2)),
            Done(habit, new DateOnly(2026, 10, 6)),
            Done(habit, new DateOnly(2026, 10, 8)),
            Done(habit, new DateOnly(2026, 10, 12)),
            // [13–15] bỏ trống
            Done(habit, new DateOnly(2026, 10, 17)),
            Done(habit, new DateOnly(2026, 10, 19)),
        };

        var result = _sut.Calculate(habit, logs, Today);

        result.Unit.Should().Be(StreakUnit.Window);
        result.Current.Should().Be(2, "cửa sổ [22–24] là hiện tại chưa xong nên không đứt; [19–21] và [16–18] xong");
        result.Best.Should().Be(4);
        result.AtRiskToday.Should().BeTrue();
    }

    [Fact]
    public void EveryNDays_AnchorAfterStart_HandlesNegativeWindowIndex()
    {
        var habit = NewHabit(
            frequency: new HabitFrequency.EveryNDays(7, new DateOnly(2026, 10, 15)),
            start: new DateOnly(2026, 10, 1));

        var logs = new List<DayLog>
        {
            Done(habit, new DateOnly(2026, 10, 3)),  // cửa sổ [8/10–14/10]? không: [1/10–7/10] là chỉ số -2
            Done(habit, new DateOnly(2026, 10, 10)),
            Done(habit, new DateOnly(2026, 10, 16)),
            Done(habit, new DateOnly(2026, 10, 22))
        };

        // Cửa sổ: [24/9–30/9] chỉ số -3 (trước ngày bắt đầu nên bị cắt), [1–7], [8–14], [15–21], [22–28].
        var result = _sut.Calculate(habit, logs, Today);

        result.Current.Should().BeGreaterThanOrEqualTo(3);
    }

    // ---- Quit ----

    [Fact]
    public void Quit_CountsDaysSinceLastSlip()
    {
        var habit = NewHabit(HabitKind.Quit, start: Today.AddDays(-20));
        var logs = new List<DayLog>
        {
            WithStatus(habit, Today.AddDays(-12), DayStatus.Failed, 1),
            WithStatus(habit, Today.AddDays(-4), DayStatus.Failed, 2)
        };

        var result = _sut.Calculate(habit, logs, Today);

        result.Current.Should().Be(4, "từ 19/10 đến 22/10");
        result.Best.Should().Be(8, "khoảng dài nhất là từ ngày bắt đầu (2/10) tới trước lần lỡ đầu (10/10)");
        result.AtRiskToday.Should().BeFalse();
    }

    [Fact]
    public void Quit_NoSlips_StreakIsDaysSinceStart()
    {
        var habit = NewHabit(HabitKind.Quit, start: Today.AddDays(-9));

        var result = _sut.Calculate(habit, [], Today);

        result.Current.Should().Be(10);
        result.Best.Should().Be(10);
    }

    [Fact]
    public void Quit_SlipToday_ResetsToZero()
    {
        var habit = NewHabit(HabitKind.Quit, start: Today.AddDays(-9));
        var logs = new List<DayLog> { WithStatus(habit, Today, DayStatus.Failed, 1) };

        var result = _sut.Calculate(habit, logs, Today);

        result.Current.Should().Be(0);
        result.Best.Should().Be(9);
    }

    // ---- Tính chất ----

    [Fact]
    public void RandomizedData_CurrentNeverExceedsBest_AndNeverNegative()
    {
        var random = new Random(20261105);

        for (var i = 0; i < 400; i++)
        {
            var frequency = RandomFrequency(random);
            var kind = random.Next(5) == 0 ? HabitKind.Quit : HabitKind.Boolean;
            var habit = NewHabit(kind, frequency, start: Today.AddDays(-random.Next(1, 400)));
            var logs = RandomLogs(random, habit);

            var result = _sut.Calculate(habit, logs, Today);

            result.Current.Should().BeGreaterThanOrEqualTo(0);
            result.Best.Should().BeGreaterThanOrEqualTo(result.Current, $"case {i}: {frequency}, kind {kind}");
        }
    }

    [Fact]
    public void ThreeYearsOfData_CalculatesQuickly()
    {
        var habit = NewHabit(start: Today.AddDays(-1095));
        var logs = DoneRange(habit, Today.AddDays(-1095), Today.AddDays(-1));

        _sut.Calculate(habit, logs, Today); // làm nóng JIT

        var watch = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < 20; i++)
        {
            _sut.Calculate(habit, logs, Today);
        }

        watch.Stop();

        (watch.Elapsed.TotalMilliseconds / 20).Should().BeLessThan(5, "mục tiêu SPEC là < 1 ms; ngưỡng 5 ms chống nhiễu CI");
    }

    internal static HabitFrequency RandomFrequency(Random random) => random.Next(5) switch
    {
        0 => new HabitFrequency.Daily(),
        1 => new HabitFrequency.WeekDays((HabitGrid.Core.Models.DayOfWeekMask)random.Next(1, 128)),
        2 => new HabitFrequency.TimesPerWeek(random.Next(1, 7)),
        3 => new HabitFrequency.TimesPerMonth(random.Next(1, 28)),
        _ => new HabitFrequency.EveryNDays(random.Next(2, 31), Today.AddDays(-random.Next(0, 200)))
    };

    internal static List<DayLog> RandomLogs(Random random, Habit habit)
    {
        var logs = new List<DayLog>();
        var density = random.NextDouble();

        for (var d = habit.StartDate; d <= Today; d = d.AddDays(1))
        {
            var roll = random.NextDouble();
            if (roll > density)
            {
                continue;
            }

            logs.Add(habit.Kind == HabitKind.Quit
                ? WithStatus(habit, d, DayStatus.Failed, random.Next(1, 4))
                : random.Next(12) == 0
                    ? WithStatus(habit, d, random.Next(2) == 0 ? DayStatus.Skipped : DayStatus.Frozen)
                    : Done(habit, d));
        }

        return logs;
    }
}
