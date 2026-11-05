using FluentAssertions;
using HabitGrid.Core.Domain;
using HabitGrid.Presentation.Services;

namespace HabitGrid.Presentation.Tests;

public class QuickCheckInModelTests
{
    private static QuickCheckInModel NewModel(AppFixture f) => new(f.Habits, f.Logs);

    [Theory]
    [InlineData("Đọc sách", "doc", true)]
    [InlineData("Đọc sách", "DOC SACH", true)]
    [InlineData("Đọc sách", "sách đọc", true)]
    [InlineData("Uống nước", "nuoc", true)]
    [InlineData("Uống nước", "gym", false)]
    [InlineData("Tập gym", "", true)]
    [InlineData("Tập gym", null, true)]
    [InlineData("Ngủ sớm", "ngu som", true)]
    public void Matches_IgnoresCaseAndDiacritics(string name, string? query, bool expected) =>
        QuickCheckInModel.Matches(name, query).Should().Be(expected);

    [Fact]
    public async Task Load_ListsOnlyHabitsDueToday_ExcludingQuitAndNotStarted()
    {
        using var f = new AppFixture();
        await f.AddAsync("Daily", startDaysAgo: 10);
        await f.AddAsync("Quit", HabitKind.Quit, startDaysAgo: 10);
        // 2026-10-22 là thứ Năm: thói quen chỉ thứ Hai không đến hạn.
        await f.AddAsync("Mondays", frequency: new HabitFrequency.WeekDays(HabitGrid.Core.Models.DayOfWeekMask.Monday), startDaysAgo: 10);
        var model = NewModel(f);

        await model.LoadAsync(AppFixture.Today);

        model.Items.Select(i => i.Name).Should().Equal("Daily");
    }

    [Fact]
    public async Task Counts_FractionAndFilter_PutPendingFirst()
    {
        using var f = new AppFixture();
        var a = await f.AddAsync("An", startDaysAgo: 5);
        await f.AddAsync("Bình", startDaysAgo: 5);
        await f.LogAsync(a, AppFixture.Today, 1);
        var model = NewModel(f);

        await model.LoadAsync(AppFixture.Today);

        model.DueCount.Should().Be(2);
        model.DoneCount.Should().Be(1);
        model.Fraction.Should().Be(0.5);
        model.Filter(null).Select(i => i.Name).Should().Equal("Bình", "An");
        model.Filter("binh").Should().ContainSingle(i => i.Name == "Bình");
    }

    [Fact]
    public async Task Fraction_IsOne_WhenNothingDue()
    {
        using var f = new AppFixture();
        var model = NewModel(f);

        await model.LoadAsync(AppFixture.Today);

        model.Fraction.Should().Be(1);
    }

    [Fact]
    public async Task Toggle_ChecksAndReflectsAfterReload()
    {
        using var f = new AppFixture();
        await f.AddAsync("A", startDaysAgo: 3);
        var model = NewModel(f);
        await model.LoadAsync(AppFixture.Today);

        await model.ToggleAsync(model.Items[0]);
        await model.LoadAsync(AppFixture.Today);

        model.Items[0].IsDone.Should().BeTrue();
    }

    [Fact]
    public async Task Adjust_StepsCountUpAndDown_AndClampsAtZero()
    {
        using var f = new AppFixture();
        await f.AddAsync("Water", HabitKind.Count, target: 4, startDaysAgo: 3);
        var model = NewModel(f);
        await model.LoadAsync(AppFixture.Today);

        await model.AdjustAsync(model.Items[0], +1);
        await model.LoadAsync(AppFixture.Today);
        model.Items[0].Value.Should().Be(1);

        await model.AdjustAsync(model.Items[0], +1);
        await model.LoadAsync(AppFixture.Today);
        model.Items[0].Value.Should().Be(2);

        await model.AdjustAsync(model.Items[0], -5);
        await model.LoadAsync(AppFixture.Today);
        model.Items[0].Value.Should().Be(0);
        model.Items[0].IsDone.Should().BeFalse();
    }

    [Fact]
    public async Task Adjust_BooleanPlusChecks_MinusUnchecks()
    {
        using var f = new AppFixture();
        await f.AddAsync("A", startDaysAgo: 3);
        var model = NewModel(f);
        await model.LoadAsync(AppFixture.Today);

        await model.AdjustAsync(model.Items[0], +1);
        await model.LoadAsync(AppFixture.Today);
        model.Items[0].IsDone.Should().BeTrue();

        await model.AdjustAsync(model.Items[0], -1);
        await model.LoadAsync(AppFixture.Today);
        model.Items[0].IsDone.Should().BeFalse();
    }
}
