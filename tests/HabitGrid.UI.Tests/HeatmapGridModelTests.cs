using FluentAssertions;
using HabitGrid.Core.Models;
using HabitGrid.Core.Services;
using HabitGrid.UI.Controls;
using Xunit;

namespace HabitGrid.UI.Tests;

/// <summary>
/// Bộ kiểm thử tính mức màu và trạng thái ô của lưới heatmap.
/// </summary>
public class HeatmapGridModelTests
{
    private static readonly DateOnly Today = new(2026, 10, 22);
    private static readonly HeatmapShades Shades = new("#EBEDF0", "#9BE9A8", "#40C463", "#30A14E", "#216E39");

    [Fact]
    public void GetLevel_BooleanHabit_WithValueOne_ShouldBeComplete()
    {
        var model = CreateModel(CreateBooleanHabit(), new Dictionary<DateOnly, int> { [Today] = 1 });

        model.GetLevel(Today).Should().Be(HeatmapLevel.Complete);
    }

    [Fact]
    public void GetLevel_BooleanHabit_WithoutValue_ShouldBeEmpty()
    {
        var model = CreateModel(CreateBooleanHabit(), new Dictionary<DateOnly, int>());

        model.GetLevel(Today).Should().Be(HeatmapLevel.Empty);
    }

    [Theory]
    [InlineData(0, HeatmapLevel.Empty)]
    [InlineData(1, HeatmapLevel.Low)]
    [InlineData(2, HeatmapLevel.Medium)]
    [InlineData(3, HeatmapLevel.Medium)]
    [InlineData(4, HeatmapLevel.High)]
    [InlineData(7, HeatmapLevel.High)]
    [InlineData(8, HeatmapLevel.Complete)]
    [InlineData(12, HeatmapLevel.Complete)]
    public void GetLevel_CountHabit_ShouldFollowRatioBands(int value, HeatmapLevel expected)
    {
        var model = CreateModel(CreateCountHabit(target: 8), new Dictionary<DateOnly, int> { [Today] = value });

        model.GetLevel(Today).Should().Be(expected);
    }

    [Fact]
    public void GetShadeHex_ShouldMapLevelToPalette()
    {
        var model = CreateModel(CreateCountHabit(target: 8), new Dictionary<DateOnly, int>
        {
            [Today] = 8,
            [Today.AddDays(-1)] = 4,
            [Today.AddDays(-2)] = 2,
            [Today.AddDays(-3)] = 1
        });

        model.GetShadeHex(Today).Should().Be("#216E39");
        model.GetShadeHex(Today.AddDays(-1)).Should().Be("#30A14E");
        model.GetShadeHex(Today.AddDays(-2)).Should().Be("#40C463");
        model.GetShadeHex(Today.AddDays(-3)).Should().Be("#9BE9A8");
        model.GetShadeHex(Today.AddDays(-4)).Should().Be("#EBEDF0");
    }

    [Fact]
    public void IsFuture_ShouldBeTrueOnlyAfterToday()
    {
        var model = CreateModel(CreateBooleanHabit(), new Dictionary<DateOnly, int>());

        model.IsFuture(Today).Should().BeFalse();
        model.IsFuture(Today.AddDays(1)).Should().BeTrue();
        model.IsFuture(Today.AddDays(-1)).Should().BeFalse();
    }

    [Fact]
    public void IsScheduled_ShouldFollowHabitSchedule()
    {
        var habit = CreateBooleanHabit();
        habit.ScheduleMask = DayOfWeekMask.Monday;
        var model = CreateModel(habit, new Dictionary<DateOnly, int>());

        model.IsScheduled(new DateOnly(2026, 10, 19)).Should().BeTrue("19/10/2026 là thứ Hai");
        model.IsScheduled(new DateOnly(2026, 10, 20)).Should().BeFalse();
    }

    [Fact]
    public void GetTooltipText_Boolean_ShouldDescribeCompletion()
    {
        var model = CreateModel(CreateBooleanHabit(), new Dictionary<DateOnly, int> { [Today] = 1 });

        model.GetTooltipText(Today).Should().Contain("Đã xong");
    }

    [Fact]
    public void GetTooltipText_Boolean_NotCompleted_ShouldSayNotCompleted()
    {
        var model = CreateModel(CreateBooleanHabit(), new Dictionary<DateOnly, int>());

        model.GetTooltipText(Today).Should().Contain("Chưa hoàn thành");
    }

    [Fact]
    public void GetTooltipText_Count_ShouldShowValueAndPercentage()
    {
        var model = CreateModel(CreateCountHabit(target: 8), new Dictionary<DateOnly, int> { [Today] = 5 });

        var text = model.GetTooltipText(Today);

        text.Should().Contain("5/8");
        text.Should().Contain("62%");
    }

    [Fact]
    public void GetValue_WithMissingDay_ShouldReturnZero()
    {
        var model = CreateModel(CreateBooleanHabit(), new Dictionary<DateOnly, int>());

        model.GetValue(Today).Should().Be(0);
    }

    private static Habit CreateBooleanHabit() => new()
    {
        Name = "Đọc sách",
        Kind = HabitKind.Boolean,
        StartDate = Today.AddDays(-100)
    };

    private static Habit CreateCountHabit(int target) => new()
    {
        Name = "Uống nước",
        Kind = HabitKind.Count,
        Target = target,
        StartDate = Today.AddDays(-100)
    };

    private static HeatmapGridModel CreateModel(Habit habit, Dictionary<DateOnly, int> values)
    {
        var layout = new HeatmapLayout(WidgetSize.Medium, Today);
        return new HeatmapGridModel(habit, values, Shades, Today, layout);
    }
}
