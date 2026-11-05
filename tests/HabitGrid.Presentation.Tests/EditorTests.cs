using FluentAssertions;
using HabitGrid.Core.Domain;
using HabitGrid.Presentation.Templates;
using HabitGrid.Presentation.ViewModels;
using DayOfWeekMask = HabitGrid.Core.Models.DayOfWeekMask;

namespace HabitGrid.Presentation.Tests;

public class HabitEditorTests
{
    [Fact]
    public async Task New_WithoutName_IsInvalid_AndWithName_Saves()
    {
        using var f = new AppFixture();
        var editor = f.NewEditor();

        editor.Validate().Select(e => e.Code).Should().Contain("name.required");
        (await editor.SaveAsync()).Succeeded.Should().BeFalse();
        (await f.Habits.CountActiveAsync()).Should().Be(0);

        editor.Name = "  Đọc sách  ";
        var result = await editor.SaveAsync();

        result.Succeeded.Should().BeTrue();
        result.Habit!.Name.Should().Be("Đọc sách");
        result.Habit.StartDate.Should().Be(AppFixture.Today);
        (await f.Habits.CountActiveAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Save_PersistsEveryField()
    {
        using var f = new AppFixture();
        var editor = f.NewEditor();
        editor.Name = "Chạy bộ";
        editor.Description = "Quanh hồ buổi sáng";
        editor.Icon = HabitIcon.Emoji("🏃").Value;
        editor.ColorHue = "#F43F5E";
        editor.Kind = HabitKind.Duration;
        editor.Target = 30;
        editor.Unit = "phút";
        editor.FrequencyType = FrequencyType.TimesPerWeek;
        editor.TimesPerWeek = 4;
        editor.TimeOfDay = TimeOfDay.Morning;
        editor.AskMood = true;
        editor.AllowNotes = false;
        editor.AllowFreeze = false;
        editor.TagsText = "sức khỏe, Sức khỏe , cardio";
        editor.AddReminder(new TimeOnly(6, 30));
        editor.Reminders[0].Smart = true;
        editor.Reminders[0].FollowUp = true;
        editor.Reminders[0].Days = DayOfWeekMask.Monday | DayOfWeekMask.Thursday;

        var saved = (await editor.SaveAsync()).Habit!;
        var loaded = (await f.Habits.GetByIdAsync(saved.Id))!;

        loaded.Description.Should().Be("Quanh hồ buổi sáng");
        loaded.Icon.Value.Should().Be("emoji:🏃");
        loaded.ColorHue.Should().Be("#F43F5E");
        loaded.Kind.Should().Be(HabitKind.Duration);
        loaded.Target.Should().Be(new HabitTarget(30, "phút"));
        loaded.Frequency.Should().Be(new HabitFrequency.TimesPerWeek(4));
        loaded.TimeOfDay.Should().Be(TimeOfDay.Morning);
        loaded.AskMood.Should().BeTrue();
        loaded.AllowNotes.Should().BeFalse();
        loaded.AllowFreeze.Should().BeFalse();
        loaded.Tags.Should().Equal("sức khỏe", "cardio");
        loaded.Reminders.Should().Equal(new Reminder(new TimeOnly(6, 30), DayOfWeekMask.Monday | DayOfWeekMask.Thursday, true, true));
    }

    [Theory]
    [InlineData(FrequencyType.Daily)]
    [InlineData(FrequencyType.WeekDays)]
    [InlineData(FrequencyType.TimesPerWeek)]
    [InlineData(FrequencyType.TimesPerMonth)]
    [InlineData(FrequencyType.EveryNDays)]
    public async Task EveryFrequencyType_SavesAndReloadsIntoTheEditor(FrequencyType type)
    {
        using var f = new AppFixture();
        var editor = f.NewEditor();
        editor.Name = "X";
        editor.FrequencyType = type;
        editor.WeekDays = DayOfWeekMask.Tuesday | DayOfWeekMask.Saturday;
        editor.TimesPerWeek = 5;
        editor.TimesPerMonth = 12;
        editor.IntervalDays = 7;

        var saved = (await editor.SaveAsync()).Habit!;

        var again = f.NewEditor();
        again.LoadFrom((await f.Habits.GetByIdAsync(saved.Id))!);

        again.FrequencyType.Should().Be(type);
        again.BuildFrequency().Should().Be(saved.Frequency);

        if (type == FrequencyType.WeekDays)
        {
            again.WeekDays.Should().Be(DayOfWeekMask.Tuesday | DayOfWeekMask.Saturday);
        }
    }

    [Fact]
    public async Task Edit_KeepsIdentityAndSortKey_AndUpdatesTheRow()
    {
        using var f = new AppFixture();
        var first = await f.AddAsync("A");
        var habit = await f.AddAsync("B");
        var original = (await f.Habits.GetByIdAsync(habit.Id))!;

        var editor = f.NewEditor();
        editor.LoadFrom(original);
        editor.IsEditing.Should().BeTrue();
        editor.Name = "B đã đổi tên";
        editor.Kind = HabitKind.Count;
        editor.Target = 5;
        var result = await editor.SaveAsync();

        result.Succeeded.Should().BeTrue();
        var reloaded = (await f.Habits.GetByIdAsync(habit.Id))!;
        reloaded.Id.Should().Be(habit.Id);
        reloaded.SortKey.Should().Be(original.SortKey);
        reloaded.CreatedAt.Should().Be(original.CreatedAt);
        reloaded.Name.Should().Be("B đã đổi tên");
        reloaded.Target.Value.Should().Be(5);
        (await f.Habits.GetAllAsync()).Should().HaveCount(2);
        (await f.Habits.GetByIdAsync(first.Id))!.Name.Should().Be("A");
    }

    [Fact]
    public async Task Edit_WithNoChanges_DoesNotAlterTheHabit()
    {
        using var f = new AppFixture();
        var editor = f.NewEditor();
        editor.Name = "Phức tạp";
        editor.Kind = HabitKind.Count;
        editor.Target = 8;
        editor.Unit = "ly";
        editor.FrequencyType = FrequencyType.WeekDays;
        editor.WeekDays = DayOfWeekMask.Monday | DayOfWeekMask.Friday;
        editor.AddReminder();
        editor.TagsText = "a, b";
        var saved = (await editor.SaveAsync()).Habit!;
        var before = (await f.Habits.GetByIdAsync(saved.Id))!;

        var edit = f.NewEditor();
        edit.LoadFrom(before);
        await edit.SaveAsync();

        var after = (await f.Habits.GetByIdAsync(saved.Id))!;
        after.Should().BeEquivalentTo(before, o => o.Excluding(h => h.Version).Excluding(h => h.UpdatedAt));
    }

    [Fact]
    public void ChangingKind_AdjustsTarget_AndBooleanIsAlwaysOne()
    {
        using var f = new AppFixture();
        var editor = f.NewEditor();

        editor.Kind = HabitKind.Duration;
        editor.Target.Should().Be(20);
        editor.HasNumericTarget.Should().BeTrue();

        editor.Target = 45;
        editor.Kind = HabitKind.Count;
        editor.Target.Should().Be(45, "đổi giữa hai loại số thì giữ mục tiêu");

        editor.Kind = HabitKind.Boolean;
        editor.Target.Should().Be(1);
        editor.HasNumericTarget.Should().BeFalse();
    }

    [Fact]
    public void WeekDays_Toggle_AndEmptySelectionIsInvalid()
    {
        using var f = new AppFixture();
        var editor = f.NewEditor();
        editor.Name = "X";
        editor.FrequencyType = FrequencyType.WeekDays;
        editor.WeekDays = DayOfWeekMask.None;

        editor.Validate().Select(e => e.Code).Should().Contain("frequency.days.empty");

        editor.ToggleWeekDay(DayOfWeek.Saturday);
        editor.WeekDays.Should().Be(DayOfWeekMask.Saturday);
        editor.Validate().Should().BeEmpty();

        editor.ToggleWeekDay(DayOfWeek.Saturday);
        editor.WeekDays.Should().Be(DayOfWeekMask.None);
    }

    [Theory]
    [InlineData(FrequencyType.TimesPerWeek, 0)]
    [InlineData(FrequencyType.TimesPerWeek, 7)]
    [InlineData(FrequencyType.TimesPerMonth, 28)]
    [InlineData(FrequencyType.EveryNDays, 1)]
    [InlineData(FrequencyType.EveryNDays, 31)]
    public void OutOfRangeFrequencyValues_AreRejected(FrequencyType type, int value)
    {
        using var f = new AppFixture();
        var editor = f.NewEditor();
        editor.Name = "X";
        editor.FrequencyType = type;
        editor.TimesPerWeek = value;
        editor.TimesPerMonth = value;
        editor.IntervalDays = value;

        editor.Validate().Should().NotBeEmpty();
    }

    [Fact]
    public void Reminders_DefaultTimeFollowsTimeOfDay_AndAreCappedAtFive()
    {
        using var f = new AppFixture();
        var editor = f.NewEditor();
        editor.Name = "X";

        editor.TimeOfDay = TimeOfDay.Morning;
        editor.AddReminder()!.Time.Should().Be(new TimeOnly(8, 0));
        editor.TimeOfDay = TimeOfDay.Evening;
        editor.AddReminder()!.Time.Should().Be(new TimeOnly(20, 0));

        while (editor.CanAddReminder)
        {
            editor.AddReminder();
        }

        editor.Reminders.Should().HaveCount(HabitValidator.MaxReminders);
        editor.AddReminder().Should().BeNull();

        editor.RemoveReminder(editor.Reminders[0]);
        editor.CanAddReminder.Should().BeTrue();
        editor.Validate().Should().BeEmpty();
    }

    [Fact]
    public void TooManyTags_AndLongDescription_AreRejected()
    {
        using var f = new AppFixture();
        var editor = f.NewEditor();
        editor.Name = "X";
        editor.TagsText = "a,b,c,d,e,f";
        editor.Description = new string('d', 201);

        editor.Validate().Select(e => e.Code).Should().Contain(["tags.too_many", "description.too_long"]);
    }

    [Fact]
    public async Task FreePlan_AllowsFiveActiveHabits_SixthIsRejected_ArchivingFreesASlot()
    {
        using var f = new AppFixture();
        var free = new FreeEntitlements();

        for (var i = 1; i <= 5; i++)
        {
            var editor = f.NewEditor(free);
            editor.Name = $"H{i}";
            (await editor.SaveAsync()).Succeeded.Should().BeTrue();
        }

        var sixth = f.NewEditor(free);
        sixth.Name = "H6";
        var rejected = await sixth.SaveAsync();
        rejected.Succeeded.Should().BeFalse();
        rejected.Errors.Select(e => e.Code).Should().Contain("limit.habits");

        var creation = new HabitCreationService(f.Habits, f.Days, free);
        (await creation.CreateAsync(new HabitDraft("H6", HabitIcon.Default))).Errors
            .Select(e => e.Code).Should().Contain("limit.habits");

        await f.Habits.ArchiveAsync((await f.Habits.GetAllActiveAsync())[0].Id);
        (await sixth.SaveAsync()).Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task FreePlan_EditingExistingHabit_IsNeverBlocked()
    {
        using var f = new AppFixture();
        var free = new FreeEntitlements();
        for (var i = 1; i <= 5; i++)
        {
            var editor = f.NewEditor(free);
            editor.Name = $"H{i}";
            await editor.SaveAsync();
        }

        var edit = f.NewEditor(free);
        edit.LoadFrom((await f.Habits.GetAllActiveAsync())[0]);
        edit.Name = "Đổi tên";

        (await edit.SaveAsync()).Succeeded.Should().BeTrue();
    }

    [Fact]
    public void ValidationText_CoversEveryCodeTheEditorCanProduce()
    {
        var localizer = new HabitGrid.Presentation.Services.Localizer();
        string[] codes =
        [
            "name.required", "name.too_long", "description.too_long", "color.invalid", "target.range", "target.unit_too_long",
            "frequency.days.empty", "frequency.times.range", "frequency.interval.range", "tags.too_many", "tags.invalid",
            "reminders.too_many", "limit.habits"
        ];

        foreach (var language in new[] { "vi", "en" })
        {
            localizer.SetLanguage(language);

            foreach (var code in codes)
            {
                localizer[$"validation.{code}"].Should().NotStartWith("[", $"{language}: thiếu bản dịch cho {code}");
            }
        }
    }
}

public class HabitTemplatesTests
{
    [Fact]
    public void Library_HasAtLeast60Templates_AcrossAllEightTopics()
    {
        HabitTemplates.All.Count.Should().BeGreaterThanOrEqualTo(60);

        foreach (var topic in Enum.GetValues<HabitTopic>())
        {
            HabitTemplates.All.Count(t => t.Topic == topic).Should().BeGreaterThanOrEqualTo(8, topic.ToString());
        }
    }

    [Fact]
    public void Ids_AreUnique_AndNamesExistInBothLanguages_WithinLengthLimit()
    {
        HabitTemplates.All.Select(t => t.Id).Should().OnlyHaveUniqueItems();

        foreach (var t in HabitTemplates.All)
        {
            t.NameVi.Should().NotBeNullOrWhiteSpace(t.Id);
            t.NameEn.Should().NotBeNullOrWhiteSpace(t.Id);
            t.NameVi.Length.Should().BeLessThanOrEqualTo(HabitValidator.MaxNameLength, t.Id);
            t.NameEn.Length.Should().BeLessThanOrEqualTo(HabitValidator.MaxNameLength, t.Id);
            t.Emoji.Should().NotBeNullOrWhiteSpace(t.Id);

            if (t.Kind is HabitKind.Count or HabitKind.Duration)
            {
                t.Target.Should().BeGreaterThan(0, t.Id);
                t.UnitVi.Should().NotBeNullOrWhiteSpace(t.Id);
                t.UnitEn.Should().NotBeNullOrWhiteSpace(t.Id);
            }
        }
    }

    [Fact]
    public async Task EveryTemplate_ProducesAHabitThatPassesValidation_InBothLanguages()
    {
        using var f = new AppFixture();

        foreach (var language in new[] { "vi", "en" })
        {
            foreach (var template in HabitTemplates.All)
            {
                var result = await f.Creation.CreateAsync(template.ToDraft(language, AppFixture.Today));

                result.Succeeded.Should().BeTrue($"{template.Id}/{language}: {string.Join(",", result.Errors.Select(e => e.Code))}");
            }
        }
    }

    [Fact]
    public void EveryTopic_AndStringKey_HasATranslation()
    {
        var localizer = new HabitGrid.Presentation.Services.Localizer();

        foreach (var language in new[] { "vi", "en" })
        {
            localizer.SetLanguage(language);

            foreach (var topic in Enum.GetValues<HabitTopic>())
            {
                localizer[$"topic.{topic.ToString().ToLowerInvariant()}"].Should().NotStartWith("[", topic.ToString());
            }
        }
    }

    [Fact]
    public void ToFrequency_UsesTodayAsAnchorForEveryNDays()
    {
        var plants = HabitTemplates.Find("home.plants")!;

        plants.ToFrequency(AppFixture.Today).Should().Be(new HabitFrequency.EveryNDays(2, AppFixture.Today));
        HabitTemplates.Find("fit.gym")!.ToFrequency(AppFixture.Today).Should().Be(new HabitFrequency.TimesPerWeek(3));
    }
}
