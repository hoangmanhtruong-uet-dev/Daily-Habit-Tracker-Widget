using FluentAssertions;
using HabitGrid.Core.Domain;
using HabitGrid.Presentation.ViewModels;

namespace HabitGrid.Presentation.Tests;

public class TodayBentoTests
{
    [Fact]
    public async Task Week_HasSevenDays_MondayFirst_WithTodayMarked_AndFutureLocked()
    {
        using var f = new AppFixture();
        await f.AddAsync("A", startDaysAgo: 30);
        var vm = f.NewToday();

        await vm.LoadAsync();

        // 2026-10-22 là thứ Năm → tuần 19/10 … 25/10.
        vm.Week.Should().HaveCount(7);
        vm.Week[0].Day.Should().Be(new DateOnly(2026, 10, 19));
        vm.Week[6].Day.Should().Be(new DateOnly(2026, 10, 25));
        vm.Week.Single(d => d.IsToday).Day.Should().Be(AppFixture.Today);
        vm.Week.Where(d => d.Day > AppFixture.Today).Should().OnlyContain(d => d.IsFuture && d.Fraction == null);
        vm.Week.Single(d => d.IsSelected).Day.Should().Be(AppFixture.Today);
    }

    [Fact]
    public async Task Week_FractionIsDoneOverScheduled_ExcludingQuitHabits()
    {
        using var f = new AppFixture();
        var a = await f.AddAsync("A", startDaysAgo: 30);
        await f.AddAsync("B", startDaysAgo: 30);
        var quit = await f.AddAsync("Q", HabitKind.Quit, startDaysAgo: 30);
        await f.LogAsync(a, new DateOnly(2026, 10, 20), 1);
        await f.Logs.UpsertAsync(DayLogRules.Normalize(quit, new DayLog { HabitId = quit.Id, Day = new DateOnly(2026, 10, 20), Value = 2 }));
        var vm = f.NewToday();

        await vm.LoadAsync();

        vm.Week[1].Fraction.Should().Be(0.5, "1 trong 2 thói quen có thể theo dõi xong, thói quen cần bỏ không tính");
        vm.Week[0].Fraction.Should().Be(0);
    }

    [Fact]
    public async Task SelectDay_LoadsThatDay_AndCheckInWritesToIt()
    {
        using var f = new AppFixture();
        var habit = await f.AddAsync("Đọc sách", startDaysAgo: 30);
        var vm = f.NewToday();
        await vm.LoadAsync();
        var yesterday = AppFixture.Today.AddDays(-1);

        await vm.SelectDayAsync(yesterday);
        vm.SelectedDay.Should().Be(yesterday);
        vm.IsViewingPast.Should().BeTrue();
        vm.Week.Single(d => d.IsSelected).Day.Should().Be(yesterday);

        await vm.CheckInAsync(vm.Groups.Single().Items.Single());

        (await f.Logs.GetAsync(habit.Id, yesterday))!.Status.Should().Be(DayStatus.Done);
        (await f.Logs.GetAsync(habit.Id, AppFixture.Today)).Should().BeNull("chỉ sửa ngày đang xem");
        vm.Completed.Should().ContainSingle();

        await vm.SelectDayAsync(AppFixture.Today);
        vm.IsViewingPast.Should().BeFalse();
        vm.Completed.Should().BeEmpty();
    }

    [Fact]
    public async Task SelectDay_InTheFuture_IsIgnored()
    {
        using var f = new AppFixture();
        await f.AddAsync("A");
        var vm = f.NewToday();
        await vm.LoadAsync();

        await vm.SelectDayAsync(AppFixture.Today.AddDays(2));

        vm.SelectedDay.Should().Be(AppFixture.Today);
    }

    [Fact]
    public async Task PastDay_ShowsHabitsScheduledForThatDay()
    {
        using var f = new AppFixture();
        // Chỉ thứ Hai: 19/10 là thứ Hai, hôm nay (thứ Năm) không có lịch.
        await f.AddAsync("Thứ Hai", frequency: new HabitFrequency.WeekDays(HabitGrid.Core.Models.DayOfWeekMask.Monday), startDaysAgo: 30);
        var vm = f.NewToday();
        await vm.LoadAsync();
        vm.Groups.Should().BeEmpty();
        vm.NotToday.Should().ContainSingle();

        await vm.SelectDayAsync(new DateOnly(2026, 10, 19));

        vm.Groups.Single().Items.Should().ContainSingle();
        vm.NotToday.Should().BeEmpty();
    }

    [Fact]
    public async Task OverallStreak_CountsConsecutiveDaysWithAnyDone_AndSurvivesAnUnfinishedToday()
    {
        using var f = new AppFixture();
        var a = await f.AddAsync("A", startDaysAgo: 30);
        var b = await f.AddAsync("B", startDaysAgo: 30);
        await f.LogAsync(a, AppFixture.Today.AddDays(-1), 1);
        await f.LogAsync(b, AppFixture.Today.AddDays(-2), 1);
        await f.LogAsync(a, AppFixture.Today.AddDays(-3), 1);
        // -4 trống → chuỗi đứt
        await f.LogAsync(a, AppFixture.Today.AddDays(-5), 1);
        var vm = f.NewToday();

        await vm.LoadAsync();
        vm.OverallStreak.Should().Be(3, "3 ngày liền trước hôm nay; hôm nay chưa xong nhưng chuỗi vẫn sống");

        await vm.CheckInAsync(vm.Groups.Single().Items.First(i => i.Id == a.Id));
        vm.OverallStreak.Should().Be(4);
    }

    [Fact]
    public async Task OverallStreak_FrozenDayBridgesWithoutCounting()
    {
        using var f = new AppFixture();
        var a = await f.AddAsync("A", startDaysAgo: 30);
        await f.LogAsync(a, AppFixture.Today.AddDays(-1), 1);
        await f.Logs.UpsertAsync(new DayLog { HabitId = a.Id, Day = AppFixture.Today.AddDays(-2), Status = DayStatus.Frozen });
        await f.LogAsync(a, AppFixture.Today.AddDays(-3), 1);
        var vm = f.NewToday();

        await vm.LoadAsync();

        vm.OverallStreak.Should().Be(2);
    }

    [Fact]
    public async Task Skip_MarksTheDay_DoesNotBreakStreak_AndToggles()
    {
        using var f = new AppFixture();
        var habit = await f.AddAsync("A", startDaysAgo: 30);
        await f.LogAsync(habit, AppFixture.Today.AddDays(-1), 1);
        var vm = f.NewToday();
        await vm.LoadAsync();
        var item = vm.Groups.Single().Items.Single();

        await vm.SkipAsync(item);

        (await f.Logs.GetAsync(habit.Id, AppFixture.Today))!.Status.Should().Be(DayStatus.Skipped);
        item.IsSkipped.Should().BeTrue();
        item.Streak.Should().Be(1);
        vm.DoneCount.Should().Be(0);

        await vm.SkipAsync(item);
        (await f.Logs.GetAsync(habit.Id, AppFixture.Today)).Should().BeNull("bấm lần nữa để bỏ đánh dấu");
    }

    [Fact]
    public async Task Skip_BeyondTwoPerWeek_IsRefusedWithAMessage()
    {
        using var f = new AppFixture();
        var habit = await f.AddAsync("A", startDaysAgo: 30);
        await f.Logs.UpsertAsync(new DayLog { HabitId = habit.Id, Day = AppFixture.Today.AddDays(-1), Status = DayStatus.Skipped });
        await f.Logs.UpsertAsync(new DayLog { HabitId = habit.Id, Day = AppFixture.Today.AddDays(-2), Status = DayStatus.Skipped });
        var vm = f.NewToday();
        await vm.LoadAsync();

        await vm.SkipAsync(vm.Groups.Single().Items.Single());

        vm.ErrorMessage.Should().Be("skip.limit");
        (await f.Logs.GetAsync(habit.Id, AppFixture.Today)).Should().BeNull();
    }

    [Fact]
    public async Task Skip_CanBeUndone()
    {
        using var f = new AppFixture();
        var habit = await f.AddAsync("A", startDaysAgo: 30);
        var vm = f.NewToday();
        await vm.LoadAsync();

        await vm.SkipAsync(vm.Groups.Single().Items.Single());
        await vm.UndoAsync();

        (await f.Logs.GetAsync(habit.Id, AppFixture.Today)).Should().BeNull();
    }

    [Fact]
    public async Task Cards_AreReusedAcrossLoads_SoTheUiCanAnimateChanges()
    {
        using var f = new AppFixture();
        await f.AddAsync("A");
        var vm = f.NewToday();
        await vm.LoadAsync();
        var item = vm.Groups.Single().Items.Single();
        var changes = new List<string>();
        item.PropertyChanged += (_, e) => changes.Add(e.PropertyName!);

        await vm.CheckInAsync(item);

        vm.Groups.Single().Items.Single().Should().BeSameAs(item);
        changes.Should().Contain(nameof(HabitTodayItem.Status));
        changes.Should().Contain(nameof(HabitTodayItem.IsDone));
    }

    [Fact]
    public async Task GroupsAreNotRebuilt_WhenOnlyAValueChanges()
    {
        using var f = new AppFixture();
        await f.AddAsync("A");
        await f.AddAsync("B");
        var vm = f.NewToday();
        await vm.LoadAsync();
        var rebuilds = 0;
        vm.Groups.CollectionChanged += (_, _) => rebuilds++;

        await vm.CheckInAsync(vm.Groups.Single().Items[0]);

        rebuilds.Should().Be(0, "đổi giá trị không được làm dựng lại cả lưới (mất hiệu ứng, nhấp nháy)");
    }

    [Fact]
    public async Task EditingAHabit_UpdatesTheSameCard()
    {
        using var f = new AppFixture();
        var habit = await f.AddAsync("Tên cũ");
        var vm = f.NewToday();
        await vm.LoadAsync();
        var item = vm.Groups.Single().Items.Single();

        var editor = f.NewEditor();
        editor.LoadFrom((await f.Habits.GetByIdAsync(habit.Id))!);
        editor.Name = "Tên mới";
        await editor.SaveAsync();
        await vm.LoadAsync();

        vm.Groups.Single().Items.Single().Should().BeSameAs(item);
        item.Name.Should().Be("Tên mới");
    }

    [Theory]
    [InlineData(0, 0, BuddyMood.Sleepy)]
    [InlineData(4, 0, BuddyMood.Sleepy)]
    [InlineData(4, 1, BuddyMood.Okay)]
    [InlineData(4, 3, BuddyMood.Happy)]
    [InlineData(4, 4, BuddyMood.Ecstatic)]
    public async Task BuddyMood_FollowsProgress(int total, int done, BuddyMood expected)
    {
        using var f = new AppFixture();
        var habits = new List<Habit>();
        for (var i = 0; i < total; i++)
        {
            habits.Add(await f.AddAsync($"H{i}", startDaysAgo: 5));
        }

        for (var i = 0; i < done; i++)
        {
            await f.LogAsync(habits[i], AppFixture.Today, 1);
        }

        var vm = f.NewToday();
        await vm.LoadAsync();

        vm.BuddyMood.Should().Be(expected);
    }

    [Fact]
    public async Task Profile_FeedsBuddyNameXpLevelAndFreezeTokens()
    {
        using var f = new AppFixture();
        await f.Profile.SaveAsync((await f.Profile.GetAsync()) with { BuddyName = "Mochi", Xp = 420, Level = 5, FreezeTokens = 2 });
        var vm = new TodayViewModel(f.Habits, f.Logs, f.Days, profile: f.Profile);

        await vm.LoadAsync();

        vm.BuddyName.Should().Be("Mochi");
        vm.Xp.Should().Be(420);
        vm.Level.Should().Be(5);
        vm.FreezeTokens.Should().Be(2);
    }
}
