using FluentAssertions;
using HabitGrid.Core.Domain;
using HabitGrid.Presentation.Templates;
using HabitGrid.Presentation.ViewModels;

namespace HabitGrid.Presentation.Tests;

public class OnboardingTests
{
    private static async Task GoToHabitsStepAsync(OnboardingViewModel vm)
    {
        await vm.NextAsync();                      // Welcome → Goals
        vm.ToggleTopic(HabitTopic.Fitness);
        await vm.NextAsync();                      // Goals → Habits
    }

    [Fact]
    public async Task IsNeeded_OnlyWhenNotDoneAndNoHabits()
    {
        using var f = new AppFixture();
        (await OnboardingViewModel.IsNeededAsync(f.Settings, f.Habits)).Should().BeTrue();

        await f.AddAsync("Có sẵn");
        (await OnboardingViewModel.IsNeededAsync(f.Settings, f.Habits)).Should().BeFalse("đã có dữ liệu (ví dụ nhập từ v1)");
        (await f.Settings.GetAsync(SettingKeys.OnboardingDone)).Should().Be("1", "được đánh dấu để không hỏi lại");
    }

    [Fact]
    public void Topics_AreCappedAtThree_AndDriveTheSuggestions()
    {
        using var f = new AppFixture();
        var vm = f.NewOnboarding();

        vm.ToggleTopic(HabitTopic.Study).Should().BeTrue();
        vm.ToggleTopic(HabitTopic.Health).Should().BeTrue();
        vm.ToggleTopic(HabitTopic.Mind).Should().BeTrue();
        vm.ToggleTopic(HabitTopic.Quit).Should().BeFalse("chỉ tối đa 3 chủ đề");

        vm.Suggestions.Should().OnlyContain(t => t.Topic == HabitTopic.Study || t.Topic == HabitTopic.Health || t.Topic == HabitTopic.Mind);
        vm.Suggestions.Count.Should().BeGreaterThanOrEqualTo(24);

        vm.ToggleTopic(HabitTopic.Study).Should().BeTrue("bỏ chọn luôn được");
        vm.ToggleTopic(HabitTopic.Quit).Should().BeTrue();
        vm.Suggestions.Should().Contain(t => t.Topic == HabitTopic.Quit);
    }

    [Fact]
    public async Task NextIsBlockedUntilEachStepIsComplete()
    {
        using var f = new AppFixture();
        var vm = f.NewOnboarding();

        vm.CanGoNext.Should().BeTrue("bước chào luôn đi tiếp được");
        await vm.NextAsync();
        vm.Step.Should().Be(OnboardingStep.Goals);

        vm.CanGoNext.Should().BeFalse("chưa chọn chủ đề");
        (await vm.NextAsync()).Should().BeFalse();

        vm.ToggleTopic(HabitTopic.Health);
        await vm.NextAsync();
        vm.Step.Should().Be(OnboardingStep.Habits);
        vm.CanGoNext.Should().BeFalse("chưa chọn thói quen");

        vm.TogglePick(HabitTemplates.Find("health.water")!);
        vm.CanGoNext.Should().BeTrue();
        vm.Picked[0].Name = "  ";
        vm.CanGoNext.Should().BeFalse("tên trống");
        vm.Picked[0].Name = "Uống nước";
        await vm.NextAsync();

        vm.Step.Should().Be(OnboardingStep.Buddy);
        vm.BuddyName = "";
        vm.CanGoNext.Should().BeFalse();
        vm.BuddyName = "Mochi";
        await vm.NextAsync();

        vm.Step.Should().Be(OnboardingStep.Reminder);
        vm.IsLastStep.Should().BeTrue();
        (await vm.NextAsync()).Should().BeFalse("bước cuối dùng Finish");
    }

    [Fact]
    public async Task Picks_AreCappedAtThree_CanBeToggledAndCustomAdded()
    {
        using var f = new AppFixture();
        var vm = f.NewOnboarding();
        await GoToHabitsStepAsync(vm);

        vm.TogglePick(HabitTemplates.Find("fit.gym")!).Should().BeTrue();
        vm.TogglePick(HabitTemplates.Find("fit.run")!).Should().BeTrue();
        vm.AddCustom("Pilates nhẹ").Should().BeTrue();
        vm.TogglePick(HabitTemplates.Find("fit.yoga")!).Should().BeFalse("tối đa 3");
        vm.AddCustom("Thêm nữa").Should().BeFalse();

        vm.IsPicked(HabitTemplates.Find("fit.gym")!).Should().BeTrue();
        vm.TogglePick(HabitTemplates.Find("fit.gym")!);
        vm.IsPicked(HabitTemplates.Find("fit.gym")!).Should().BeFalse();
        vm.AddCustom("   ").Should().BeFalse();
        vm.Picked.Should().HaveCount(2);
    }

    [Fact]
    public async Task Finish_CreatesHabits_WithTemplateFrequencyAndReminder_SavesBuddy_AndMarksDone()
    {
        using var f = new AppFixture();
        var vm = f.NewOnboarding();
        await GoToHabitsStepAsync(vm);
        vm.TogglePick(HabitTemplates.Find("fit.gym")!);
        vm.TogglePick(HabitTemplates.Find("health.water")!);
        vm.Picked[1].Target = 6;
        vm.AddCustom("Pilates");
        await vm.NextAsync();
        vm.BuddyName = "Mochi";
        vm.BuddyColor = "#EC4899";
        await vm.NextAsync();
        vm.ReminderChoice = ReminderChoice.Evening;

        var result = await vm.FinishAsync();

        result.Succeeded.Should().BeTrue();
        result.Created.Should().HaveCount(3);

        var habits = await f.Habits.GetAllActiveAsync();
        habits.Select(h => h.Name).Should().Equal("Tập gym", "Uống nước", "Pilates");
        habits[0].Frequency.Should().Be(new HabitFrequency.TimesPerWeek(3));
        habits[1].Target.Should().Be(new HabitTarget(6, "ly"));
        habits[2].Kind.Should().Be(HabitKind.Boolean);
        habits.Should().OnlyContain(h => h.Reminders.Count == 1 && h.Reminders[0].Time == new TimeOnly(20, 0));
        habits.Select(h => h.ColorHue).Should().OnlyHaveUniqueItems("mỗi thói quen một màu");

        var profile = await f.Profile.GetAsync();
        profile.BuddyName.Should().Be("Mochi");
        profile.BuddyColor.Should().Be("#EC4899");

        (await f.Settings.GetAsync(SettingKeys.OnboardingDone)).Should().Be("1");
        (await f.Settings.GetAsync(SettingKeys.OnboardingState)).Should().BeNull("tiến độ tạm được dọn");
        (await OnboardingViewModel.IsNeededAsync(f.Settings, f.Habits)).Should().BeFalse();
    }

    [Theory]
    [InlineData(ReminderChoice.None, null)]
    [InlineData(ReminderChoice.Morning, "08:00")]
    [InlineData(ReminderChoice.Custom, "21:45")]
    public async Task ReminderChoice_MapsToTheRightTime(ReminderChoice choice, string? expected)
    {
        using var f = new AppFixture();
        var vm = f.NewOnboarding();
        await GoToHabitsStepAsync(vm);
        vm.TogglePick(HabitTemplates.Find("fit.stretch")!);
        vm.ReminderChoice = choice;
        vm.CustomReminderTime = new TimeOnly(21, 45);

        await vm.FinishAsync();

        var habit = (await f.Habits.GetAllActiveAsync()).Single();
        if (expected is null)
        {
            habit.Reminders.Should().BeEmpty();
        }
        else
        {
            habit.Reminders.Should().ContainSingle().Which.Time.Should().Be(TimeOnly.Parse(expected));
        }
    }

    [Fact]
    public async Task Progress_IsSaved_AndRestoredByANewInstance()
    {
        using var f = new AppFixture();
        var first = f.NewOnboarding();
        await GoToHabitsStepAsync(first);
        first.TogglePick(HabitTemplates.Find("fit.run")!);
        first.AddCustom("Việc riêng");
        await first.NextAsync(); // sang Buddy, lưu tiến độ
        first.BuddyName = "Bông";

        await first.NextAsync(); // sang Reminder, lưu lại cùng tên Buddy

        var second = f.NewOnboarding();
        await second.RestoreAsync();

        second.Step.Should().Be(OnboardingStep.Reminder);
        second.SelectedTopics.Should().Equal(HabitTopic.Fitness);
        second.Picked.Select(p => p.Name).Should().Equal("Chạy bộ", "Việc riêng");
        second.Picked[0].Template!.Id.Should().Be("fit.run");
        second.BuddyName.Should().Be("Bông");
        second.Suggestions.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Restore_WithCorruptState_IsIgnoredAndCleared()
    {
        using var f = new AppFixture();
        await f.Settings.SetAsync(SettingKeys.OnboardingState, "{not json");
        var vm = f.NewOnboarding();

        await vm.RestoreAsync();

        vm.Step.Should().Be(OnboardingStep.Welcome);
        (await f.Settings.GetAsync(SettingKeys.OnboardingState)).Should().BeNull();
    }

    [Fact]
    public async Task Back_GoesToThePreviousStep_AndNeverBelowWelcome()
    {
        using var f = new AppFixture();
        var vm = f.NewOnboarding();
        await vm.NextAsync();
        vm.CanGoBack.Should().BeTrue();

        await vm.BackAsync();
        vm.Step.Should().Be(OnboardingStep.Welcome);
        vm.CanGoBack.Should().BeFalse();

        await vm.BackAsync();
        vm.Step.Should().Be(OnboardingStep.Welcome);
    }

    [Fact]
    public async Task Skip_MarksDone_WithoutCreatingAnything()
    {
        using var f = new AppFixture();
        var vm = f.NewOnboarding();

        await vm.SkipAsync();

        (await f.Habits.CountActiveAsync()).Should().Be(0);
        (await OnboardingViewModel.IsNeededAsync(f.Settings, f.Habits)).Should().BeFalse();
    }

    [Fact]
    public async Task EnglishLanguage_UsesEnglishNamesAndUnits()
    {
        using var f = new AppFixture();
        f.Localizer.SetLanguage("en");
        var vm = f.NewOnboarding();
        await GoToHabitsStepAsync(vm);
        vm.TogglePick(HabitTemplates.Find("fit.pushup")!);
        await vm.FinishAsync();

        var habit = (await f.Habits.GetAllActiveAsync()).Single();
        habit.Name.Should().Be("Push-ups");
        habit.Target.Unit.Should().Be("reps");
    }

    [Fact]
    public async Task Finish_WhenFreeLimitWouldBeExceeded_ReturnsErrorInsteadOfPartialData()
    {
        using var f = new AppFixture();
        var limited = new LimitedEntitlements(maxHabits: 1);
        var vm = f.NewOnboarding(limited);
        await GoToHabitsStepAsync(vm);
        vm.TogglePick(HabitTemplates.Find("fit.gym")!);
        vm.TogglePick(HabitTemplates.Find("fit.run")!);

        var result = await vm.FinishAsync();

        result.Succeeded.Should().BeFalse();
        result.Errors.Select(e => e.Code).Should().Contain("limit.habits");
        result.Created.Should().HaveCount(1, "thói quen đầu đã tạo trước khi chạm giới hạn, báo rõ cho người dùng");
        (await f.Settings.GetAsync(SettingKeys.OnboardingDone)).Should().BeNull("chưa hoàn tất nên chưa đánh dấu xong");
    }

    private sealed class LimitedEntitlements : IEntitlements
    {
        public LimitedEntitlements(int maxHabits) => MaxActiveHabits = maxHabits;

        public int MaxActiveHabits { get; }

        public int MaxRemindersPerHabit => 5;

        public bool CanUseAiCoach => false;

        public bool CanUseTheme(string themeId) => false;
    }
}
