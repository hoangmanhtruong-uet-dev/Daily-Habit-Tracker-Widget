using System.Windows;
using FluentAssertions;
using HabitGrid.UI.Controls;
using Xunit;

using HabitGrid.Core.Models;
namespace HabitGrid.UI.Tests;

/// <summary>
/// Bộ kiểm thử bố trí lưới heatmap: kích thước, ánh xạ ngày, hit-test toán học.
/// </summary>
public class HeatmapLayoutTests
{
    /// <summary>2026-10-22 là thứ Năm.</summary>
    private static readonly DateOnly Today = new(2026, 10, 22);

    [Theory]
    [InlineData(WidgetSize.Small, 13, 14)]
    [InlineData(WidgetSize.Medium, 26, 14)]
    [InlineData(WidgetSize.Large, 52, 12)]
    public void Layout_ShouldUseSpecSizeTable(WidgetSize preset, int expectedWeeks, double expectedCellSize)
    {
        var layout = new HeatmapLayout(preset, Today);

        layout.TotalWeeks.Should().Be(expectedWeeks);
        layout.CellSize.Should().Be(expectedCellSize);
        layout.CellGap.Should().Be(3);
    }

    [Fact]
    public void Layout_SmallPreset_ShouldHideDayLabels()
    {
        new HeatmapLayout(WidgetSize.Small, Today).ShowDayLabels.Should().BeFalse();
        new HeatmapLayout(WidgetSize.Medium, Today).ShowDayLabels.Should().BeTrue();
        new HeatmapLayout(WidgetSize.Large, Today).ShowDayLabels.Should().BeTrue();
    }

    [Fact]
    public void Layout_LargePreset_ShouldContain364Cells()
    {
        var layout = new HeatmapLayout(WidgetSize.Large, Today);

        (layout.TotalWeeks * HeatmapLayout.DayRows).Should().Be(364);
    }

    [Fact]
    public void GetDate_LastColumnRowZero_ShouldBeCurrentWeekStart()
    {
        var layout = new HeatmapLayout(WidgetSize.Medium, Today, DayOfWeek.Monday);

        layout.CurrentWeekStart.Should().Be(new DateOnly(2026, 10, 19));
        layout.GetDate(layout.TotalWeeks - 1, 0).Should().Be(new DateOnly(2026, 10, 19));
    }

    [Fact]
    public void GetDate_LastColumn_ShouldContainToday()
    {
        var layout = new HeatmapLayout(WidgetSize.Medium, Today, DayOfWeek.Monday);

        var todayRow = -1;

        for (var row = 0; row < HeatmapLayout.DayRows; row++)
        {
            if (layout.GetDate(layout.TotalWeeks - 1, row) == Today)
            {
                todayRow = row;
            }
        }

        todayRow.Should().Be(3, "thứ Năm là hàng thứ 4 khi tuần bắt đầu từ thứ Hai");
    }

    [Fact]
    public void GetDate_SundayWeekStart_ShouldShiftRows()
    {
        var layout = new HeatmapLayout(WidgetSize.Medium, Today, DayOfWeek.Sunday);

        layout.CurrentWeekStart.Should().Be(new DateOnly(2026, 10, 18));
        layout.GetDate(layout.TotalWeeks - 1, 0).Should().Be(new DateOnly(2026, 10, 18));
        layout.GetDate(layout.TotalWeeks - 1, 4).Should().Be(Today, "thứ Năm là hàng thứ 5 khi tuần bắt đầu từ Chủ Nhật");
    }

    [Fact]
    public void GetDate_ShouldBeContiguousAcrossColumns()
    {
        var layout = new HeatmapLayout(WidgetSize.Small, Today);

        for (var column = 1; column < layout.TotalWeeks; column++)
        {
            for (var row = 0; row < HeatmapLayout.DayRows; row++)
            {
                layout.GetDate(column, row).Should().Be(layout.GetDate(column - 1, row).AddDays(7));
            }
        }
    }

    [Fact]
    public void GetCellBounds_ShouldFollowOriginAndPitch()
    {
        var layout = new HeatmapLayout(WidgetSize.Medium, Today);

        var bounds = layout.GetCellBounds(weekColumn: 3, dayRow: 2);

        bounds.X.Should().Be(layout.GridOriginX + (3 * (layout.CellSize + layout.CellGap)));
        bounds.Y.Should().Be(layout.GridOriginY + (2 * (layout.CellSize + layout.CellGap)));
        bounds.Width.Should().Be(layout.CellSize);
        bounds.Height.Should().Be(layout.CellSize);
    }

    [Fact]
    public void HitTest_InsideCell_ShouldReturnMatchingCell()
    {
        var layout = new HeatmapLayout(WidgetSize.Medium, Today);
        var bounds = layout.GetCellBounds(5, 2);

        var cell = layout.HitTest(new Point(bounds.X + 7, bounds.Y + 7));

        cell.Should().NotBeNull();
        cell!.WeekColumnIndex.Should().Be(5);
        cell.DayRowIndex.Should().Be(2);
        cell.Date.Should().Be(layout.GetDate(5, 2));
    }

    [Fact]
    public void HitTest_InGapBetweenCells_ShouldReturnNull()
    {
        var layout = new HeatmapLayout(WidgetSize.Medium, Today);
        var bounds = layout.GetCellBounds(5, 2);

        // 1px nằm trong khoảng trống giữa ô này và ô kế tiếp.
        var cell = layout.HitTest(new Point(bounds.Right + 1, bounds.Y + 5));

        cell.Should().BeNull();
    }

    [Fact]
    public void HitTest_OutsideGrid_ShouldReturnNull()
    {
        var layout = new HeatmapLayout(WidgetSize.Medium, Today);

        layout.HitTest(new Point(-5, -5)).Should().BeNull();
        layout.HitTest(new Point(layout.TotalWidth + 50, 10)).Should().BeNull();
        layout.HitTest(new Point(10, layout.TotalHeight + 50)).Should().BeNull();
    }

    [Fact]
    public void HitTest_ShouldFlagTodayAndFuture()
    {
        var layout = new HeatmapLayout(WidgetSize.Medium, Today);

        var todayCell = layout.HitTest(CenterOf(layout, layout.TotalWeeks - 1, 3));
        todayCell.Should().NotBeNull();
        todayCell!.IsToday.Should().BeTrue();
        todayCell.IsFuture.Should().BeFalse();

        var futureCell = layout.HitTest(CenterOf(layout, layout.TotalWeeks - 1, 6));
        futureCell.Should().NotBeNull();
        futureCell!.IsFuture.Should().BeTrue();
        futureCell.IsInteractive.Should().BeFalse();
    }

    [Fact]
    public void HitTest_ShouldUseScheduleCallback()
    {
        var layout = new HeatmapLayout(WidgetSize.Medium, Today);
        var cell = layout.HitTest(CenterOf(layout, 3, 2), _ => false);

        cell.Should().NotBeNull();
        cell!.IsScheduled.Should().BeFalse();
    }

    [Fact]
    public void GetMonthLabels_ShouldStartAtFirstColumnAndIncrease()
    {
        var layout = new HeatmapLayout(WidgetSize.Large, Today);

        var labels = layout.GetMonthLabels();

        labels.Should().NotBeEmpty();
        labels[0].ColumnIndex.Should().Be(0);

        for (var i = 1; i < labels.Count; i++)
        {
            labels[i].ColumnIndex.Should().BeGreaterThan(labels[i - 1].ColumnIndex);
        }
    }

    [Fact]
    public void GetMonthLabels_ShouldMarkColumnWhereMonthChanges()
    {
        var layout = new HeatmapLayout(WidgetSize.Large, Today);

        foreach (var label in layout.GetMonthLabels().Where(l => l.ColumnIndex > 0))
        {
            var previousMonthDate = layout.GetDate(label.ColumnIndex - 1, 0);

            previousMonthDate.Month.Should().NotBe(label.Month, $"cột {label.ColumnIndex} phải là cột bắt đầu tháng mới");
        }
    }

    [Fact]
    public void GetDayLabelRows_ShouldShowTuesdayThursdaySaturday()
    {
        var layout = new HeatmapLayout(WidgetSize.Medium, Today, DayOfWeek.Monday);

        var rows = layout.GetDayLabelRows();

        rows.Should().HaveCount(3);
        rows[1].Should().Be(DayOfWeek.Tuesday);
        rows[3].Should().Be(DayOfWeek.Thursday);
        rows[5].Should().Be(DayOfWeek.Saturday);
    }

    [Fact]
    public void GetDayLabelRows_WithSundayStart_ShouldShiftRows()
    {
        var layout = new HeatmapLayout(WidgetSize.Medium, Today, DayOfWeek.Sunday);

        var rows = layout.GetDayLabelRows();

        rows[2].Should().Be(DayOfWeek.Tuesday);
        rows[4].Should().Be(DayOfWeek.Thursday);
        rows[6].Should().Be(DayOfWeek.Saturday);
    }

    [Fact]
    public void TotalSize_ShouldCoverGridAndLabels()
    {
        var layout = new HeatmapLayout(WidgetSize.Large, Today);

        layout.TotalWidth.Should().Be(layout.GridOriginX + layout.GridWidth);
        layout.TotalHeight.Should().Be(layout.GridOriginY + layout.GridHeight);
        layout.GridHeight.Should().Be((7 * (layout.CellSize + layout.CellGap)) - layout.CellGap);
    }

    private static Point CenterOf(HeatmapLayout layout, int column, int row)
    {
        var bounds = layout.GetCellBounds(column, row);
        return new Point(bounds.X + (bounds.Width / 2), bounds.Y + (bounds.Height / 2));
    }
}
