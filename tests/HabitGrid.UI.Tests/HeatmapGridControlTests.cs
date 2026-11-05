using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FluentAssertions;
using HabitGrid.Core.Models;
using HabitGrid.Core.Services;
using HabitGrid.UI.Controls;
using HabitGrid.UI.Widgets;
using Xunit;

namespace HabitGrid.UI.Tests;

/// <summary>
/// Kiểm thử smoke cho phần WPF: control tự vẽ phải đo được và render được thật.
/// Các test này chạy trên luồng STA riêng vì WPF yêu cầu apartment state.
/// </summary>
public class HeatmapGridControlTests
{
    private static readonly DateOnly Today = new(2026, 10, 22);
    private static readonly HeatmapShades Shades = new("#EBEDF0", "#9BE9A8", "#40C463", "#30A14E", "#216E39");

    [Fact]
    public void Control_ShouldMeasureToLayoutSize()
    {
        RunSta(() =>
        {
            var model = CreateModel();
            var control = new HeatmapGridControl { Model = model };

            control.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            control.DesiredSize.Width.Should().BeApproximately(model.Layout.TotalWidth, 0.01);
            control.DesiredSize.Height.Should().BeApproximately(model.Layout.TotalHeight, 0.01);
        });
    }

    [Fact]
    public void Control_ShouldRenderToBitmapWithoutError()
    {
        RunSta(() =>
        {
            var model = CreateModel();
            var control = new HeatmapGridControl
            {
                Model = model,
                Width = 500,
                Height = 200
            };

            control.Measure(new Size(500, 200));
            control.Arrange(new Rect(0, 0, 500, 200));
            control.UpdateLayout();

            var bitmap = new RenderTargetBitmap(500, 200, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(control);

            bitmap.PixelWidth.Should().Be(500);
            bitmap.PixelHeight.Should().Be(200);

            // Lưới phải thực sự được vẽ ra: đếm số pixel có alpha > 0.
            var pixels = new byte[500 * 200 * 4];
            bitmap.CopyPixels(pixels, 500 * 4, 0);

            var paintedPixels = 0;
            for (var i = 3; i < pixels.Length; i += 4)
            {
                if (pixels[i] > 0)
                {
                    paintedPixels++;
                }
            }

            paintedPixels.Should().BeGreaterThan(1000, "các ô và nhãn phải được vẽ thật, không phải control rỗng");
        });
    }

    [Fact]
    public void Control_LargePreset_ShouldRenderFastEnoughFor60Fps()
    {
        RunSta(() =>
        {
            var layout = new HeatmapLayout(WidgetSize.Large, Today);
            var habit = new Habit { Name = "Đọc sách", StartDate = Today.AddDays(-400) };
            var model = new HeatmapGridModel(habit, new Dictionary<DateOnly, int>(), Shades, Today, layout);

            var control = new HeatmapGridControl
            {
                Model = model,
                Width = 820,
                Height = 240
            };

            control.Measure(new Size(820, 240));
            control.Arrange(new Rect(0, 0, 820, 240));
            control.UpdateLayout();

            var bitmap = new RenderTargetBitmap(820, 240, 96, 96, PixelFormats.Pbgra32);

            // Chạy nóng 1 lần cho JIT rồi đo 50 lần render thật.
            bitmap.Render(control);

            var stopwatch = Stopwatch.StartNew();
            const int iterations = 50;

            for (var i = 0; i < iterations; i++)
            {
                control.InvalidateVisual();
                bitmap.Render(control);
            }

            stopwatch.Stop();

            var averageMilliseconds = stopwatch.Elapsed.TotalMilliseconds / iterations;

            averageMilliseconds.Should().BeLessThan(
                16.0,
                "một frame 60 FPS có 16,6ms; lưới 364 ô phải vẽ nhanh hơn ngưỡng này");
        });
    }

    [Fact]
    public void Control_LargePreset_ShouldRenderAllCellsWithoutError()
    {
        RunSta(() =>
        {
            var layout = new HeatmapLayout(WidgetSize.Large, Today);
            var habit = new Habit { Name = "Đọc sách", StartDate = Today.AddDays(-400) };
            var model = new HeatmapGridModel(habit, new Dictionary<DateOnly, int>(), Shades, Today, layout);

            var control = new HeatmapGridControl
            {
                Model = model,
                Width = 820,
                Height = 240
            };

            control.Measure(new Size(820, 240));
            control.Arrange(new Rect(0, 0, 820, 240));
            control.UpdateLayout();

            var bitmap = new RenderTargetBitmap(820, 240, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(control);

            bitmap.PixelWidth.Should().Be(820);
        });
    }

    [Fact]
    public void WidgetWindow_BindViewModel_ShouldApplyPresetSize()
    {
        RunSta(() =>
        {
            var habit = new Habit { Name = "Đọc sách", StartDate = Today.AddDays(-100) };
            var viewModel = new WidgetViewModel(
                habit,
                new Dictionary<DateOnly, int>(),
                Shades,
                Today,
                WidgetSize.Large);

            var window = new WidgetWindow();
            window.Bind(viewModel);

            var spec = WidgetSizeSpec.Get(WidgetSize.Large);
            window.Width.Should().Be(spec.WindowWidth);
            window.Height.Should().Be(spec.WindowHeight);
            window.ViewModel.Should().BeSameAs(viewModel);
        });
    }

    [Fact]
    public void WidgetWindow_SetEditMode_ShouldToggleEditLayer()
    {
        RunSta(() =>
        {
            var habit = new Habit { Name = "Đọc sách", StartDate = Today.AddDays(-100) };
            var viewModel = new WidgetViewModel(
                habit,
                new Dictionary<DateOnly, int>(),
                Shades,
                Today,
                WidgetSize.Small);

            var window = new WidgetWindow();
            window.Bind(viewModel);

            window.SetEditMode(true);
            window.SetEditMode(false);
        });
    }

    private static HeatmapGridModel CreateModel()
    {
        var layout = new HeatmapLayout(WidgetSize.Medium, Today);
        var habit = new Habit { Name = "Đọc sách", StartDate = Today.AddDays(-100) };

        var values = new Dictionary<DateOnly, int>
        {
            [Today] = 1,
            [Today.AddDays(-1)] = 1,
            [Today.AddDays(-2)] = 1
        };

        return new HeatmapGridModel(habit, values, Shades, Today, layout);
    }

    /// <summary>Chạy hành động trên một luồng STA riêng và ném lại lỗi nếu có.</summary>
    private static void RunSta(Action action)
    {
        Exception? captured = null;

        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                captured = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (captured is not null)
        {
            throw new InvalidOperationException("Test WPF thất bại trên luồng STA.", captured);
        }
    }
}
