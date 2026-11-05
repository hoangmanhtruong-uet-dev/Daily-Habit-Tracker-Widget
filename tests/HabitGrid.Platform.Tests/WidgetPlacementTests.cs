using FluentAssertions;
using HabitGrid.Platform.Desktop;
using Xunit;

namespace HabitGrid.Platform.Tests;

/// <summary>
/// Bộ kiểm thử toán học vị trí widget: tương đối/tuyệt đối, snap lưới 8px, giữ trong vùng làm việc.
/// </summary>
public class WidgetPlacementTests
{
    private static ScreenDescriptor CreateScreen(
        int left = 0,
        int top = 0,
        int width = 1920,
        int height = 1080,
        double dpiScale = 1.0,
        string devicePath = @"\\.\DISPLAY1")
    {
        return new ScreenDescriptor(devicePath, new ScreenRect(left, top, width, height), dpiScale, true);
    }

    [Fact]
    public void ToRelative_AtTopLeft_ShouldReturnOrigin()
    {
        var screen = CreateScreen();

        var relative = WidgetPlacement.ToRelative(screen, left: 0, top: 0, widgetWidth: 400, widgetHeight: 200);

        relative.X.Should().Be(0);
        relative.Y.Should().Be(0);
    }

    [Fact]
    public void ToRelative_AtBottomRight_ShouldReturnOne()
    {
        var screen = CreateScreen();

        var relative = WidgetPlacement.ToRelative(screen, left: 1920 - 400, top: 1080 - 200, widgetWidth: 400, widgetHeight: 200);

        relative.X.Should().Be(1);
        relative.Y.Should().Be(1);
    }

    [Fact]
    public void ToRelative_ShouldComputeRatioPerSpecFormula()
    {
        var screen = CreateScreen();

        // X_rel = (Left - Screen.Left) / (Screen.Width - Widget.Width) = 1520 / 1520 = 1
        var relative = WidgetPlacement.ToRelative(screen, left: 1520, top: 440, widgetWidth: 400, widgetHeight: 200);

        relative.X.Should().Be(1);
        relative.Y.Should().Be(0.5);
    }

    [Fact]
    public void ToRelative_WithOffsetScreen_ShouldAccountForScreenOrigin()
    {
        // Màn hình phụ nằm bên phải màn hình chính.
        var screen = CreateScreen(left: 1920, top: 0, width: 2560, height: 1440);

        var relative = WidgetPlacement.ToRelative(screen, left: 1920, top: 0, widgetWidth: 400, widgetHeight: 200);

        relative.X.Should().Be(0);
        relative.Y.Should().Be(0);
    }

    [Fact]
    public void ToRelative_OutsideScreen_ShouldClampToUnitRange()
    {
        var screen = CreateScreen();

        var relative = WidgetPlacement.ToRelative(screen, left: -500, top: 5000, widgetWidth: 400, widgetHeight: 200);

        relative.X.Should().BeInRange(0, 1);
        relative.Y.Should().BeInRange(0, 1);
        relative.X.Should().Be(0);
        relative.Y.Should().Be(1);
    }

    [Fact]
    public void FromRelative_AfterResolutionChange_ShouldKeepWidgetOnScreen()
    {
        var original = CreateScreen(width: 3840, height: 2160);
        var widgetWidth = 400;
        var widgetHeight = 200;

        // Widget nằm ở góc phải trên màn hình 4K.
        var relative = WidgetPlacement.ToRelative(original, left: 3440, top: 1960, widgetWidth, widgetHeight);

        // Người dùng đổi sang màn hình 1080p.
        var smaller = CreateScreen(width: 1920, height: 1080);
        var (left, top) = WidgetPlacement.FromRelative(smaller, relative, widgetWidth, widgetHeight);

        left.Should().BeGreaterThanOrEqualTo(0);
        top.Should().BeGreaterThanOrEqualTo(0);
        left.Should().BeLessThanOrEqualTo(1920 - widgetWidth);
        top.Should().BeLessThanOrEqualTo(1080 - widgetHeight);
    }

    [Fact]
    public void FromRelative_ShouldRoundTripWithToRelative()
    {
        var screen = CreateScreen();
        var widgetWidth = 400;
        var widgetHeight = 200;

        var relative = WidgetPlacement.ToRelative(screen, left: 300, top: 150, widgetWidth, widgetHeight);
        var (left, top) = WidgetPlacement.FromRelative(screen, relative, widgetWidth, widgetHeight);

        left.Should().BeInRange(299, 301);
        top.Should().BeInRange(149, 151);
    }

    [Fact]
    public void FromRelative_WithSecondaryScreen_ShouldUseScreenOrigin()
    {
        var screen = CreateScreen(left: 1920, top: 200, width: 1280, height: 1024);

        var (left, top) = WidgetPlacement.FromRelative(screen, RelativePosition.From(0, 0), 400, 200);

        left.Should().Be(1920);
        top.Should().Be(200);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(7, 8)]
    [InlineData(9, 8)]
    [InlineData(12, 16)]
    [InlineData(13, 16)]
    [InlineData(-1, 0)]
    [InlineData(-4, -8)]
    public void SnapToGrid_ShouldRoundToNearestMultipleOfEight(int value, int expected)
    {
        WidgetPlacement.SnapToGrid(value, origin: 0).Should().Be(expected);
    }

    [Fact]
    public void SnapToGrid_WithOffsetOrigin_ShouldSnapRelativeToOrigin()
    {
        // 1924 cách lưới 1920 đúng 4px -> làm tròn ra xa 0 nên bám vào 1928.
        WidgetPlacement.SnapToGrid(1924, origin: 1920).Should().Be(1928);
        WidgetPlacement.SnapToGrid(1916, origin: 1920).Should().Be(1912);
    }

    [Fact]
    public void Snap_ShouldKeepWidgetInsideWorkArea()
    {
        var screen = CreateScreen();

        var (left, top) = WidgetPlacement.Snap(left: -400, top: 5000, screen, widgetWidth: 400, widgetHeight: 200);

        left.Should().BeGreaterThanOrEqualTo(screen.WorkArea.Left + WidgetPlacement.EdgeMargin);
        top.Should().BeLessThanOrEqualTo(screen.WorkArea.Bottom - 200 - WidgetPlacement.EdgeMargin);
    }

    [Fact]
    public void Snap_ShouldRespectEdgeMargin()
    {
        var screen = CreateScreen();

        var (left, top) = WidgetPlacement.Snap(left: 0, top: 0, screen, widgetWidth: 400, widgetHeight: 200);

        left.Should().Be(screen.WorkArea.Left + WidgetPlacement.EdgeMargin);
        top.Should().Be(screen.WorkArea.Top + WidgetPlacement.EdgeMargin);
    }

    [Fact]
    public void Snap_WithWidgetLargerThanWorkArea_ShouldAlignToTopLeft()
    {
        var screen = CreateScreen(width: 320, height: 240);

        var (left, top) = WidgetPlacement.Snap(left: 100, top: 100, screen, widgetWidth: 800, widgetHeight: 600);

        left.Should().Be(screen.WorkArea.Left);
        top.Should().Be(screen.WorkArea.Top);
    }

    [Fact]
    public void Snap_OnSecondaryScreen_ShouldUseThatScreenOrigin()
    {
        var screen = CreateScreen(left: 1920, top: 0, width: 2560, height: 1440);

        var (left, top) = WidgetPlacement.Snap(left: 1925, top: 12, screen, widgetWidth: 400, widgetHeight: 200);

        left.Should().Be(1920 + WidgetPlacement.EdgeMargin);
        top.Should().Be(WidgetPlacement.EdgeMargin);
    }

    [Fact]
    public void RelativePosition_From_ShouldClampToUnitRange()
    {
        var position = RelativePosition.From(-1.5, 3.2);

        position.X.Should().Be(0);
        position.Y.Should().Be(1);
    }
}
