using FluentAssertions;
using HabitGrid.Core.Domain;
using HabitGrid.Core.Domain.Monetization;
using HabitGrid.Core.Domain.Routines;
using HabitGrid.Presentation.Services;

namespace HabitGrid.Presentation.Tests;

public class RoutineTests
{
    private static readonly DateOnly Today = AppFixture.Today;

    private static RoutineService Create(AppFixture f, bool plus = false)
    {
        var live = new LiveEntitlements();
        live.Set(plus);
        return new RoutineService(f.Habits, f.Logs, new HabitGrid.Data.V3.RoutineRepository(f.Database, f.Clock, f.Bus), live, f.Days, f.Settings);
    }

    private static RoutineDraft Draft(string name, params Habit[] habits) =>
        new(name, HabitIcon.Emoji("🌅"), TimeOfDay.Morning, habits.Select(h => h.Id).ToList());

    [Theory]
    [InlineData("", 2, 0, false, true, RoutineError.NameRequired)]
    [InlineData("   ", 2, 0, false, true, RoutineError.NameRequired)]
    [InlineData("Một", 1, 0, false, true, RoutineError.TooFewHabits)]
    [InlineData("Một", 13, 0, true, true, RoutineError.TooManyHabits)]
    [InlineData("Sáng", 2, 2, false, true, RoutineError.LimitReached)]
    [InlineData("Sáng", 2, 2, true, true, RoutineError.None)]
    [InlineData("Sáng", 2, 2, false, false, RoutineError.None)]
    [InlineData("Sáng", 3, 1, false, true, RoutineError.None)]
    public void Validate_FollowsTheRules(string name, int habits, int existing, bool plus, bool isNew, RoutineError expected) =>
        RoutineRules.Validate(name, habits, existing, plus, isNew).Should().Be(expected);

    [Fact]
    public void Validate_RejectsALongName() =>
        RoutineRules.Validate(new string('x', 31), 2, 0, false, true).Should().Be(RoutineError.NameTooLong);

    [Fact]
    public async Task Create_AssignsMembersInOrder_AndListsThem()
    {
        using var f = new AppFixture();
        var a = await f.AddAsync("Uống nước", startDaysAgo: 5);
        var b = await f.AddAsync("Tập giãn cơ", startDaysAgo: 5);
        var c = await f.AddAsync("Đọc sách", startDaysAgo: 5);
        var service = Create(f);

        var result = await service.CreateAsync(Draft("Sáng", c, a));

        result.Succeeded.Should().BeTrue();
        var routine = (await service.ListAsync()).Single();
        routine.Name.Should().Be("Sáng");
        routine.HabitIds.Should().Equal(c.Id, a.Id);
        (await f.Habits.GetByIdAsync(b.Id))!.RoutineId.Should().BeNull();
        (await service.AvailableHabitsAsync()).Select(h => h.Id).Should().Equal(b.Id);
    }

    [Fact]
    public async Task FreeIsLimitedToTwoRoutines_PlusIsNot()
    {
        using var f = new AppFixture();
        var habits = new List<Habit>();
        for (var i = 0; i < 8; i++) habits.Add(await f.AddAsync($"H{i}", startDaysAgo: 5));
        var free = Create(f);

        (await free.CreateAsync(Draft("R1", habits[0], habits[1]))).Succeeded.Should().BeTrue();
        (await free.CreateAsync(Draft("R2", habits[2], habits[3]))).Succeeded.Should().BeTrue();
        (await free.CreateAsync(Draft("R3", habits[4], habits[5]))).Error.Should().Be(RoutineError.LimitReached);
        (await Create(f, plus: true).CreateAsync(Draft("R3", habits[4], habits[5]))).Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task AHabitCannotBeInTwoRoutines_QuitHabitsAndUnknownIdsAreRejected()
    {
        using var f = new AppFixture();
        var a = await f.AddAsync("A", startDaysAgo: 5);
        var b = await f.AddAsync("B", startDaysAgo: 5);
        var c = await f.AddAsync("C", startDaysAgo: 5);
        var quit = await f.AddAsync("Quit", HabitKind.Quit, startDaysAgo: 5);
        var service = Create(f, plus: true);
        await service.CreateAsync(Draft("R1", a, b));

        (await service.CreateAsync(Draft("R2", b, c))).Error.Should().Be(RoutineError.UnknownHabit);
        (await service.CreateAsync(Draft("R2", c, quit))).Error.Should().Be(RoutineError.UnknownHabit);
        (await service.CreateAsync(new RoutineDraft("R2", HabitIcon.Default, TimeOfDay.Anytime, [c.Id, Guid.NewGuid()]))).Error.Should().Be(RoutineError.UnknownHabit);
        (await service.CreateAsync(new RoutineDraft("R2", HabitIcon.Default, TimeOfDay.Anytime, [c.Id, c.Id]))).Error.Should().Be(RoutineError.UnknownHabit);
    }

    [Fact]
    public async Task Update_ReordersRenames_AndReleasesRemovedHabits()
    {
        using var f = new AppFixture();
        var a = await f.AddAsync("A", startDaysAgo: 5);
        var b = await f.AddAsync("B", startDaysAgo: 5);
        var c = await f.AddAsync("C", startDaysAgo: 5);
        var service = Create(f);
        var created = (await service.CreateAsync(Draft("Sáng", a, b))).Routine!;

        var result = await service.UpdateAsync(created.Id, Draft("Sáng sớm", c, a));

        result.Succeeded.Should().BeTrue();
        var routine = (await service.ListAsync()).Single();
        routine.Name.Should().Be("Sáng sớm");
        routine.HabitIds.Should().Equal(c.Id, a.Id);
        (await f.Habits.GetByIdAsync(b.Id))!.RoutineId.Should().BeNull("B bị gỡ khỏi routine");
        (await service.AvailableHabitsAsync(created.Id)).Select(h => h.Id).Should().BeEquivalentTo([a.Id, b.Id, c.Id], "đang sửa: thành viên hiện tại cộng thói quen chưa thuộc routine nào");
        (await service.AvailableHabitsAsync()).Select(h => h.Id).Should().Equal(b.Id);
        (await service.UpdateAsync(Guid.NewGuid(), Draft("X", a, b))).Error.Should().Be(RoutineError.NotFound);
    }

    [Fact]
    public async Task Delete_DetachesTheHabits_NotDeletingThem()
    {
        using var f = new AppFixture();
        var a = await f.AddAsync("A", startDaysAgo: 5);
        var b = await f.AddAsync("B", startDaysAgo: 5);
        var service = Create(f);
        var routine = (await service.CreateAsync(Draft("Sáng", a, b))).Routine!;

        await service.DeleteAsync(routine.Id);

        (await service.ListAsync()).Should().BeEmpty();
        (await f.Habits.GetAllActiveAsync()).Should().HaveCount(2);
        (await f.Habits.GetByIdAsync(a.Id))!.RoutineId.Should().BeNull();
    }

    [Fact]
    public async Task Progress_CountsOnlyDueSteps_AndTracksDone()
    {
        using var f = new AppFixture();
        var a = await f.AddAsync("A", startDaysAgo: 5);
        var b = await f.AddAsync("B", startDaysAgo: 5);
        var mondays = await f.AddAsync("Thứ Hai", frequency: new HabitFrequency.WeekDays(HabitGrid.Core.Models.DayOfWeekMask.Monday), startDaysAgo: 5);
        var service = Create(f);
        await service.CreateAsync(Draft("Sáng", a, mondays, b));
        await f.LogAsync(a, Today, 1);

        var progress = (await service.ProgressAsync()).Single();

        progress.Total.Should().Be(2, "thói quen chỉ thứ Hai không đến hạn hôm thứ Năm");
        progress.Done.Should().Be(1);
        progress.IsComplete.Should().BeFalse();
        progress.Next!.HabitId.Should().Be(b.Id);
        progress.Fraction.Should().Be(0.5);
    }

    [Fact]
    public async Task CompleteAll_MarksOnlyUndoneActiveSteps_AndLeavesFrozenAlone()
    {
        using var f = new AppFixture();
        var a = await f.AddAsync("A", startDaysAgo: 5);
        var water = await f.AddAsync("Nước", HabitKind.Count, target: 8, startDaysAgo: 5);
        var paused = await f.AddAsync("Nghỉ", startDaysAgo: 5);
        var service = Create(f, plus: true);
        await service.CreateAsync(Draft("Sáng", a, water, paused));
        await f.Logs.UpsertAsync(new DayLog { HabitId = paused.Id, Day = Today, Status = DayStatus.Frozen });
        await f.LogAsync(water, Today, 3);

        var marked = await service.CompleteAllAsync((await service.ListAsync()).Single().Id);

        marked.Should().Be(2);
        (await f.Logs.GetAsync(a.Id, Today))!.Status.Should().Be(DayStatus.Done);
        (await f.Logs.GetAsync(water.Id, Today))!.Value.Should().Be(8);
        (await f.Logs.GetAsync(paused.Id, Today))!.Status.Should().Be(DayStatus.Frozen);
        (await service.ProgressAsync()).Single().IsComplete.Should().BeTrue();
        (await service.CompleteAllAsync((await service.ListAsync()).Single().Id)).Should().Be(0, "gọi lại không làm gì thêm");
    }

    [Fact]
    public async Task ReadOnlyHabits_AreNotStepsAnymore()
    {
        using var f = new AppFixture();
        var a = await f.AddAsync("A", startDaysAgo: 5);
        var b = await f.AddAsync("B", startDaysAgo: 5);
        var service = Create(f);
        await service.CreateAsync(Draft("Sáng", a, b));
        await f.Settings.SetAsync(ReadOnlyHabits.Key, ReadOnlyHabits.Format([b.Id]));

        var steps = (await service.ProgressAsync()).Single().Steps;

        steps.Should().ContainSingle().Which.HabitId.Should().Be(a.Id, "bước chỉ đọc bị loại khỏi routine");
    }
}
