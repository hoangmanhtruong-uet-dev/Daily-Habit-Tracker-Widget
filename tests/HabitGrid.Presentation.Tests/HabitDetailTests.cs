using FluentAssertions;
using HabitGrid.Core.Domain;
using HabitGrid.Presentation.ViewModels;

namespace HabitGrid.Presentation.Tests;

public class HabitDetailTests
{
    private static HabitDetailViewModel NewVm(AppFixture f) => new(f.Habits, f.Logs, f.Days);

    [Fact]
    public async Task Load_ComputesStreak_Rate_Total_AndHeatmap()
    {
        using var f = new AppFixture();
        var h = await f.AddAsync("Đọc sách", startDaysAgo: 100);
        for (var i = 0; i < 5; i++) await f.LogAsync(h, AppFixture.Today.AddDays(-i), 1);
        var vm = NewVm(f);

        await vm.LoadAsync(h.Id);

        vm.Habit!.Id.Should().Be(h.Id);
        vm.CurrentStreak.Should().Be(5);
        vm.BestStreak.Should().BeGreaterThanOrEqualTo(5);
        vm.TotalDone.Should().Be(5);
        vm.Rate30.Should().Be((int)Math.Round(5 / 30.0 * 100));
        vm.Heatmap!.Weeks.Should().HaveCount(HabitDetailViewModel.HeatmapWeeks);
        vm.Months.Should().HaveCount(HabitDetailViewModel.MonthsShown);
        vm.Weekdays.Should().HaveCount(7);
        vm.Weekdays[0].Day.Should().Be(DayOfWeek.Monday);
    }

    [Fact]
    public async Task RateDelta_ComparesWithPrevious30Days()
    {
        using var f = new AppFixture();
        var h = await f.AddAsync("A", startDaysAgo: 100);
        for (var i = 0; i < 30; i++) await f.LogAsync(h, AppFixture.Today.AddDays(-i), 1);
        for (var i = 30; i < 45; i++) await f.LogAsync(h, AppFixture.Today.AddDays(-i), 1);
        var vm = NewVm(f);

        await vm.LoadAsync(h.Id);

        vm.Rate30.Should().Be(100);
        vm.RateDelta.Should().Be(50, "30 ngày trước đó chỉ đạt 50%");
    }

    [Fact]
    public async Task Load_UnknownHabit_SetsNotFound()
    {
        using var f = new AppFixture();
        var vm = NewVm(f);

        await vm.LoadAsync(Guid.NewGuid());

        vm.NotFound.Should().BeTrue();
        vm.Habit.Should().BeNull();
    }

    [Fact]
    public async Task SaveDay_WritesValueNoteMood_AndRefreshes_AndJournalShowsIt()
    {
        using var f = new AppFixture();
        var h = await f.AddAsync("A", startDaysAgo: 30);
        var vm = NewVm(f);
        await vm.LoadAsync(h.Id);
        var day = AppFixture.Today.AddDays(-2);

        var ok = await vm.SaveDayAsync(day, 1, false, "  Rất tốt  ", 5);

        ok.Should().BeTrue();
        var saved = await vm.GetDayAsync(day);
        saved!.Status.Should().Be(DayStatus.Done);
        saved.Note.Should().Be("Rất tốt");
        saved.Mood.Should().Be(5);
        vm.Journal.Should().ContainSingle(j => j.Day == day && j.Note == "Rất tốt");
        vm.Heatmap!.Cells.Single(c => c.Day == day).Level.Should().Be(4);
    }

    [Fact]
    public async Task SaveDay_EmptyEntry_DeletesTheLog()
    {
        using var f = new AppFixture();
        var h = await f.AddAsync("A", startDaysAgo: 30);
        await f.LogAsync(h, AppFixture.Today.AddDays(-1), 1);
        var vm = NewVm(f);
        await vm.LoadAsync(h.Id);

        await vm.SaveDayAsync(AppFixture.Today.AddDays(-1), 0, false, null, null);

        (await vm.GetDayAsync(AppFixture.Today.AddDays(-1))).Should().BeNull();
    }

    [Fact]
    public async Task SaveDay_RejectsFutureAndBeforeStart()
    {
        using var f = new AppFixture();
        var h = await f.AddAsync("A", startDaysAgo: 5);
        var vm = NewVm(f);
        await vm.LoadAsync(h.Id);

        (await vm.SaveDayAsync(AppFixture.Today.AddDays(1), 1, false, null, null)).Should().BeFalse();
        (await vm.SaveDayAsync(AppFixture.Today.AddDays(-20), 1, false, null, null)).Should().BeFalse();
        vm.ErrorMessage.Should().Be("day.invalid");
    }

    [Fact]
    public async Task SaveDay_Skip_IsLimitedToTwoPerWeek()
    {
        using var f = new AppFixture();
        var h = await f.AddAsync("A", startDaysAgo: 30);
        var vm = NewVm(f);
        await vm.LoadAsync(h.Id);

        (await vm.SaveDayAsync(AppFixture.Today.AddDays(-1), 0, true, null, null)).Should().BeTrue();
        (await vm.SaveDayAsync(AppFixture.Today.AddDays(-2), 0, true, null, null)).Should().BeTrue();
        (await vm.SaveDayAsync(AppFixture.Today.AddDays(-3), 0, true, null, null)).Should().BeFalse();

        vm.ErrorMessage.Should().Be("skip.limit");
    }

    [Fact]
    public async Task List_FiltersArchived_Unarchives_AndMovesByStep()
    {
        using var f = new AppFixture();
        var a = await f.AddAsync("A", startDaysAgo: 20);
        var b = await f.AddAsync("B", startDaysAgo: 20);
        await f.LogAsync(a, AppFixture.Today, 1);
        var vm = new HabitsViewModel(f.Habits, f.Logs, f.Days);
        await vm.LoadAsync();

        vm.Items.Should().HaveCount(2);
        vm.Items[0].Heatmap!.Weeks.Should().HaveCount(HabitsViewModel.HeatmapWeeks);

        await vm.MoveByAsync(vm.Items[0], 1);
        vm.Items.Select(i => i.Name).Should().Equal("B", "A");

        await vm.ArchiveAsync(vm.Items[0]);
        vm.ArchivedCount.Should().Be(1);
        vm.Items.Should().ContainSingle();

        vm.ShowArchived = true;
        await vm.LoadAsync();
        vm.Items.Should().ContainSingle(i => i.IsArchived);

        await vm.UnarchiveAsync(vm.Items[0]);
        vm.Items.Should().BeEmpty();
        vm.ShowArchived = false;
        await vm.LoadAsync();
        vm.Items.Should().HaveCount(2);
    }
}
