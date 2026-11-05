using FluentAssertions;
using HabitGrid.Core.Domain;
using HabitGrid.Core.Domain.Focus;
using HabitGrid.Core.Domain.Gamification;
using HabitGrid.Core.Domain.Reminders;
using Xunit;
using static HabitGrid.Core.Tests.Domain.DomainTestData;
using DayOfWeekMask = HabitGrid.Core.Models.DayOfWeekMask;

namespace HabitGrid.Core.Tests.Reminders;

public class SmartReminderTests
{
    private static DateTime LocalUtc(DateOnly day, int hour, int minute) =>
        DateTime.SpecifyKind(day.ToDateTime(new TimeOnly(hour, minute)), DateTimeKind.Local).ToUniversalTime();

    [Theory]
    [InlineData(TimeOfDay.Morning, 8, 0)]
    [InlineData(TimeOfDay.Afternoon, 14, 0)]
    [InlineData(TimeOfDay.Evening, 20, 0)]
    [InlineData(TimeOfDay.Anytime, 19, 0)]
    public void FewerThanSevenSamples_UsesTheDefaultForTimeOfDay(TimeOfDay time, int hour, int minute)
    {
        var habit = NewHabit();
        habit.TimeOfDay = time;
        var logs = Enumerable.Range(1, 6).Select(i => Done(habit, Today.AddDays(-i)) with { CheckedAt = LocalUtc(Today.AddDays(-i), 7, 0) }).ToList();

        SmartReminder.Suggest(habit, logs, Today).Should().Be(new TimeOnly(hour, minute));
    }

    [Fact]
    public void TenSamples_Uses40thPercentileMinus15Minutes()
    {
        var habit = NewHabit();
        // Giờ check-in 7:00, 7:10, …, 8:30 (10 mẫu). Hạng ceil(0.4·10) = 4 → 7:30 → trừ 15 phút = 7:15.
        var logs = Enumerable.Range(0, 10)
            .Select(i => Done(habit, Today.AddDays(-(i + 1))) with { CheckedAt = LocalUtc(Today.AddDays(-(i + 1)), 7 + (i * 10) / 60, (i * 10) % 60) })
            .ToList();

        SmartReminder.Suggest(habit, logs, Today).Should().Be(new TimeOnly(7, 15));
    }

    [Fact]
    public void SamplesOlderThan21Days_TodayAndOtherHabits_AreIgnored()
    {
        var habit = NewHabit();
        habit.TimeOfDay = TimeOfDay.Evening;
        var other = NewHabit();
        var logs = new List<DayLog>();
        logs.AddRange(Enumerable.Range(22, 10).Select(i => Done(habit, Today.AddDays(-i)) with { CheckedAt = LocalUtc(Today.AddDays(-i), 6, 0) }));
        logs.AddRange(Enumerable.Range(1, 10).Select(i => Done(other, Today.AddDays(-i)) with { CheckedAt = LocalUtc(Today.AddDays(-i), 6, 0) }));
        logs.Add(Done(habit, Today) with { CheckedAt = LocalUtc(Today, 6, 0) });

        SmartReminder.Suggest(habit, logs, Today).Should().Be(new TimeOnly(20, 0));
    }
}

public class ReminderPlannerTests
{
    // Hôm nay 2026-10-22 (thứ Năm), lúc 06:00.
    private static readonly DateTime Now = Today.ToDateTime(new TimeOnly(6, 0));

    private static Habit WithReminder(int hour, int minute = 0, bool followUp = false, bool smart = false, DayOfWeekMask? days = null)
    {
        var habit = NewHabit(start: Today.AddDays(-30));
        habit.Reminders = [new Reminder(new TimeOnly(hour, minute), days ?? DayOfWeekMask.AllWeek, smart, followUp)];
        return habit;
    }

    private static ReminderSettings NoExtras => new() { StreakRescue = false };

    [Fact]
    public void FixedReminder_IsPlannedForTodayAndTomorrow_WithinThe48HourHorizon()
    {
        var habit = WithReminder(8);

        var plan = ReminderPlanner.Plan([habit], [], Now, NoExtras);

        plan.Select(p => p.At).Should().Equal(Today.ToDateTime(new TimeOnly(8, 0)), Today.AddDays(1).ToDateTime(new TimeOnly(8, 0)));
        plan.Should().OnlyContain(p => p.Kind == ToastKind.Reminder && p.HabitIds.Single() == habit.Id);
    }

    [Fact]
    public void PastTimesToday_AreNotPlanned()
    {
        var plan = ReminderPlanner.Plan([WithReminder(5)], [], Now, NoExtras with { QuietHoursEnabled = false });

        plan.Should().ContainSingle().Which.At.Date.Should().Be(Now.Date.AddDays(1));
    }

    [Fact]
    public void DoneSkippedOrFrozenToday_CancelsTodaysReminder()
    {
        var done = WithReminder(9);
        var skipped = WithReminder(9);
        var logs = new List<DayLog> { Done(done, Today), WithStatus(skipped, Today, DayStatus.Skipped) };

        var plan = ReminderPlanner.Plan([done, skipped], logs, Now, NoExtras);

        plan.Should().OnlyContain(p => DateOnly.FromDateTime(p.At) != Today);
    }

    [Fact]
    public void UnscheduledDays_AndReminderDayMask_AreRespected()
    {
        var mondaysOnly = NewHabit(frequency: new HabitFrequency.WeekDays(Mask(DayOfWeek.Monday)), start: Today.AddDays(-30));
        mondaysOnly.Reminders = [new Reminder(new TimeOnly(9, 0))];
        var fridayReminder = WithReminder(9, days: DayOfWeekMask.Friday);

        var plan = ReminderPlanner.Plan([mondaysOnly, fridayReminder], [], Now, NoExtras);

        plan.Should().ContainSingle().Which.At.DayOfWeek.Should().Be(DayOfWeek.Friday);
    }

    [Fact]
    public void FollowUp_IsTwoHoursLater_ButNeverAfter2230()
    {
        var morning = WithReminder(9, followUp: true);
        var late = WithReminder(21, followUp: true);
        var tooLate = WithReminder(22, 45, followUp: true);

        var plan = ReminderPlanner.Plan([morning, late, tooLate], [], Now, NoExtras with { QuietHoursEnabled = false })
            .Where(p => DateOnly.FromDateTime(p.At) == Today && p.Kind == ToastKind.FollowUp)
            .ToList();

        plan.Select(p => TimeOnly.FromDateTime(p.At)).Should().BeEquivalentTo([new TimeOnly(11, 0), new TimeOnly(22, 30)]);
    }

    [Fact]
    public void QuietHours_BlockReminders_UnlessDisabled()
    {
        var night = WithReminder(23, 30);

        ReminderPlanner.Plan([night], [], Now, NoExtras).Should().BeEmpty();
        ReminderPlanner.Plan([night], [], Now, NoExtras with { QuietHoursEnabled = false }).Should().HaveCount(2);
    }

    [Theory]
    [InlineData(23, 0, true)]
    [InlineData(2, 0, true)]
    [InlineData(6, 59, true)]
    [InlineData(7, 0, false)]
    [InlineData(22, 59, false)]
    public void IsQuiet_WrapsPastMidnight(int hour, int minute, bool quiet) =>
        new ReminderSettings().IsQuiet(new TimeOnly(hour, minute)).Should().Be(quiet);

    [Fact]
    public void SameTime_IsGroupedIntoOneToast()
    {
        var a = WithReminder(8);
        var b = WithReminder(8);

        var today = ReminderPlanner.Plan([a, b], [], Now, NoExtras).Where(p => DateOnly.FromDateTime(p.At) == Today).ToList();

        today.Should().ContainSingle().Which.HabitIds.Should().BeEquivalentTo([a.Id, b.Id]);
    }

    [Fact]
    public void DailyCap_MergesTheOverflowIntoTheLastToast()
    {
        var habits = Enumerable.Range(0, 9).Select(i => WithReminder(8 + i)).ToList();

        var today = ReminderPlanner.Plan(habits, [], Now, NoExtras).Where(p => DateOnly.FromDateTime(p.At) == Today).ToList();

        today.Should().HaveCount(6);
        today[^1].HabitIds.Should().HaveCount(4, "thói quen thứ 6–9 gộp vào toast thứ 6");
        today.SelectMany(p => p.HabitIds).Should().BeEquivalentTo(habits.Select(h => h.Id));
    }

    [Fact]
    public void StreakRescue_At21_ForStreaksOfThreeOrMore_NotDoneToday()
    {
        var onStreak = NewHabit(start: Today.AddDays(-30));
        var shortStreak = NewHabit(start: Today.AddDays(-30));
        var doneToday = NewHabit(start: Today.AddDays(-30));
        var logs = new List<DayLog>();
        logs.AddRange(DoneRange(onStreak, Today.AddDays(-4), Today.AddDays(-1)));
        logs.AddRange(DoneRange(shortStreak, Today.AddDays(-1), Today.AddDays(-1)));
        logs.AddRange(DoneRange(doneToday, Today.AddDays(-5), Today));

        var rescue = ReminderPlanner.Plan([onStreak, shortStreak, doneToday], logs, Now)
            .Where(p => p.Kind == ToastKind.StreakRescue)
            .ToList();

        rescue.Should().ContainSingle();
        rescue[0].At.Should().Be(Today.ToDateTime(new TimeOnly(21, 0)));
        rescue[0].HabitIds.Should().Equal(onStreak.Id);
    }

    [Fact]
    public void MorningSummary_ListsTodaysHabits_WhenEnabled()
    {
        var a = NewHabit(start: Today.AddDays(-3));
        var b = NewHabit(start: Today.AddDays(-3));

        var plan = ReminderPlanner.Plan([a, b], [], Now, NoExtras with { MorningSummary = true });

        var summary = plan.First(p => p.Kind == ToastKind.MorningSummary);
        summary.At.Should().Be(Today.ToDateTime(new TimeOnly(7, 30)));
        summary.HabitIds.Should().BeEquivalentTo([a.Id, b.Id]);
    }

    [Fact]
    public void ArchivedDeletedAndQuitHabits_GetNoReminders()
    {
        var archived = WithReminder(8);
        archived.ArchivedAt = DateTime.UtcNow;
        var deleted = WithReminder(8);
        deleted.DeletedAt = DateTime.UtcNow;
        var quit = WithReminder(8);
        quit.Kind = HabitKind.Quit;

        ReminderPlanner.Plan([archived, deleted, quit], [], Now, NoExtras).Should().BeEmpty();
    }

    [Fact]
    public void Ids_AreStableAndShort()
    {
        var plan1 = ReminderPlanner.Plan([WithReminder(8)], [], Now, NoExtras);
        var plan2 = ReminderPlanner.Plan([WithReminder(8)], [], Now, NoExtras);

        plan1.Select(p => p.Id).Should().Equal(plan2.Select(p => p.Id));
        plan1.Should().OnlyContain(p => p.Id.Length <= 16);
    }
}

public class FocusTimerTests
{
    private static readonly DateTime T0 = new(2026, 10, 22, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Pomodoro_CompletesAfter25Minutes_ThenBreaks_ThenGoesIdle()
    {
        var timer = new FocusTimer(FocusMode.Pomodoro, TimeSpan.FromMinutes(25), TimeSpan.FromMinutes(5), null);
        timer.Start(T0);

        timer.Tick(T0.AddMinutes(24)).Should().BeNull();
        timer.Remaining(T0.AddMinutes(24)).Should().Be(TimeSpan.FromMinutes(1));

        var result = timer.Tick(T0.AddMinutes(25).AddSeconds(3));
        result.Should().NotBeNull();
        result!.Minutes.Should().Be(25);
        result.Completed.Should().BeTrue();
        result.EndedAtUtc.Should().Be(T0.AddMinutes(25));
        timer.Phase.Should().Be(FocusPhase.Break);

        timer.Tick(T0.AddMinutes(26)).Should().BeNull("kết quả chỉ trả về một lần");
        timer.Tick(T0.AddMinutes(30).AddSeconds(1));
        timer.Phase.Should().Be(FocusPhase.Idle);
    }

    [Fact]
    public void Pause_StopsTheClock()
    {
        var timer = new FocusTimer(FocusMode.Countdown, TimeSpan.FromMinutes(10), TimeSpan.Zero, null);
        timer.Start(T0);
        timer.Pause(T0.AddMinutes(4));

        timer.Tick(T0.AddMinutes(30)).Should().BeNull();
        timer.Elapsed(T0.AddMinutes(30)).Should().Be(TimeSpan.FromMinutes(4));

        timer.Resume(T0.AddMinutes(30));
        var result = timer.Tick(T0.AddMinutes(36));

        result!.Minutes.Should().Be(10);
        timer.Phase.Should().Be(FocusPhase.Finished, "đếm ngược không có pha nghỉ");
    }

    [Fact]
    public void Toggle_StartsPausesAndResumes()
    {
        var timer = new FocusTimer(FocusMode.Pomodoro, TimeSpan.FromMinutes(25), TimeSpan.FromMinutes(5), null);

        timer.Toggle(T0);
        timer.IsRunning.Should().BeTrue();
        timer.Toggle(T0.AddMinutes(1));
        timer.IsPaused.Should().BeTrue();
        timer.Toggle(T0.AddMinutes(2));
        timer.IsRunning.Should().BeTrue();
    }

    [Fact]
    public void Stopwatch_RunsUntilStopped_AndCreditsWholeMinutes()
    {
        var habit = Guid.NewGuid();
        var timer = new FocusTimer(FocusMode.Stopwatch, TimeSpan.Zero, TimeSpan.Zero, habit);
        timer.Start(T0);

        timer.Tick(T0.AddHours(3)).Should().BeNull();
        timer.Remaining(T0.AddHours(3)).Should().BeNull();

        var result = timer.Stop(T0.AddMinutes(47).AddSeconds(50));
        result!.Minutes.Should().Be(47);
        result.HabitId.Should().Be(habit);
        timer.Phase.Should().Be(FocusPhase.Idle);
    }

    [Fact]
    public void StoppingEarly_UnderOneMinute_RecordsNothing()
    {
        var timer = new FocusTimer(FocusMode.Pomodoro, TimeSpan.FromMinutes(25), TimeSpan.FromMinutes(5), null);
        timer.Start(T0);

        timer.Stop(T0.AddSeconds(50)).Should().BeNull();
    }

    [Fact]
    public void State_RoundTrips_SoATimerSurvivesARestart()
    {
        var timer = new FocusTimer(FocusMode.Pomodoro, TimeSpan.FromMinutes(25), TimeSpan.FromMinutes(5), null);
        timer.Start(T0);
        timer.Pause(T0.AddMinutes(10));

        var restored = FocusTimer.Restore(timer.State);
        restored.Resume(T0.AddMinutes(20));

        restored.Tick(T0.AddMinutes(35)).Should().NotBeNull();
    }
}

public class FocusXpTests
{
    [Fact]
    public void FocusSessionsOf25MinutesOrMore_GiveFiveXp()
    {
        var day = Today.ToDateTime(new TimeOnly(10, 0));
        FocusRecord Session(int minutes) => new(Guid.NewGuid(), null, day.AddMinutes(-minutes).ToUniversalTime(), day.ToUniversalTime(), minutes, FocusMode.Pomodoro);

        var xp = XpEngine.Compute([], [], Today, focus: [Session(25), Session(24), Session(50)]);

        xp.Awards.Where(a => a.Kind == "focus").Should().HaveCount(2).And.OnlyContain(a => a.Amount == 5);
        xp.TotalXp.Should().Be(10);
    }
}
