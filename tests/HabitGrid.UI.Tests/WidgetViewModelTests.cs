using FluentAssertions;
using HabitGrid.Core.Models;
using HabitGrid.Core.Services;
using HabitGrid.UI.Controls;
using HabitGrid.UI.Widgets;
using Xunit;

namespace HabitGrid.UI.Tests;

/// <summary>
/// Bộ kiểm thử ViewModel của widget.
/// </summary>
public class WidgetViewModelTests
{
    private static readonly DateOnly Today = new(2026, 10, 22);
    private static readonly HeatmapShades Shades = new("#EBEDF0", "#9BE9A8", "#40C463", "#30A14E", "#216E39");

    [Fact]
    public void ViewModel_ShouldExposeHabitNameAndStreak()
    {
        var habit = CreateHabit();
        var values = new Dictionary<DateOnly, int>
        {
            [Today.AddDays(-2)] = 1,
            [Today.AddDays(-1)] = 1,
            [Today] = 1
        };

        var viewModel = CreateViewModel(habit, values);

        viewModel.DisplayName.Should().Be("Đọc sách");
        viewModel.CurrentStreak.Should().Be(3);
        viewModel.StreakText.Should().Be("3 ngày");
    }

    [Fact]
    public void ViewModel_WithoutStreak_ShouldShowFallbackText()
    {
        var viewModel = CreateViewModel(CreateHabit(), new Dictionary<DateOnly, int>());

        viewModel.StreakText.Should().Be("Chưa có streak");
    }

    [Fact]
    public void RequestMark_ShouldRaiseEventWithDate()
    {
        var viewModel = CreateViewModel(CreateHabit(), new Dictionary<DateOnly, int>());
        DateOnly? marked = null;

        viewModel.MarkRequested += (_, day) => marked = day;
        viewModel.RequestMark(Today);

        marked.Should().Be(Today);
    }

    [Fact]
    public void RequestContextMenu_ShouldRaiseEventWithDate()
    {
        var viewModel = CreateViewModel(CreateHabit(), new Dictionary<DateOnly, int>());
        DateOnly? requested = null;

        viewModel.ContextMenuRequested += (_, day) => requested = day;
        viewModel.RequestContextMenu(Today.AddDays(-1));

        requested.Should().Be(Today.AddDays(-1));
    }

    [Fact]
    public void RequestClose_ShouldRaiseEvent()
    {
        var viewModel = CreateViewModel(CreateHabit(), new Dictionary<DateOnly, int>());
        var closed = false;

        viewModel.CloseRequested += (_, _) => closed = true;
        viewModel.RequestClose();

        closed.Should().BeTrue();
    }

    [Fact]
    public void UpdateValues_ShouldRefreshGridModelAndStreak()
    {
        var viewModel = CreateViewModel(CreateHabit(), new Dictionary<DateOnly, int>());

        viewModel.UpdateValues(new Dictionary<DateOnly, int>
        {
            [Today] = 1,
            [Today.AddDays(-1)] = 1
        });

        viewModel.CurrentStreak.Should().Be(2);
        viewModel.GridModel.GetLevel(Today).Should().Be(HeatmapLevel.Complete);
        viewModel.Values.Should().HaveCount(2);
    }

    [Fact]
    public void ViewModel_WithReadOnlyHabit_ShouldExposeReadOnlyState()
    {
        var habit = CreateHabit();
        habit.IsReadOnly = true;

        var viewModel = CreateViewModel(habit, new Dictionary<DateOnly, int>());

        viewModel.IsReadOnly.Should().BeTrue();
        viewModel.GridModel.IsReadOnly.Should().BeTrue();
    }

    [Fact]
    public void ViewModel_ShouldUseRequestedSizePreset()
    {
        var viewModel = CreateViewModel(CreateHabit(), new Dictionary<DateOnly, int>(), WidgetSize.Large);

        viewModel.Layout.TotalWeeks.Should().Be(52);
        viewModel.Layout.Size.Should().Be(WidgetSize.Large);
    }

    private static Habit CreateHabit() => new()
    {
        Name = "Đọc sách",
        Kind = HabitKind.Boolean,
        StartDate = Today.AddDays(-100)
    };

    private static WidgetViewModel CreateViewModel(
        Habit habit,
        Dictionary<DateOnly, int> values,
        WidgetSize preset = WidgetSize.Medium)
    {
        return new WidgetViewModel(
            habit,
            values,
            Shades,
            Today,
            preset,
            isDarkTheme: false,
            streakCalculator: new StreakCalculator(new ScheduleCalculator()));
    }
}
