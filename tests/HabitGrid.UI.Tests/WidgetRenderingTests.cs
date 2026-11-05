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
/// Kiểm chứng nội dung hiển thị của widget được vẽ thật (không phải cửa sổ trống).
/// </summary>
public class WidgetRenderingTests
{
    private static readonly DateOnly Today = new(2026, 10, 22);
    private static readonly HeatmapShades Shades = new("#EBEDF0", "#9BE9A8", "#40C463", "#30A14E", "#216E39");

    [Fact]
    public void WidgetWindow_ShouldRenderHeaderGridAndCells()
    {
        RunSta(() =>
        {
            var habit = new Habit
            {
                Name = "Đọc sách",
                Icon = "Book24Regular",
                Kind = HabitKind.Boolean,
                StartDate = Today.AddDays(-120)
            };

            var values = new Dictionary<DateOnly, int>();

            for (var day = 0; day < 60; day++)
            {
                if (day % 5 != 0)
                {
                    values[Today.AddDays(-day)] = 1;
                }
            }

            var viewModel = new WidgetViewModel(habit, values, Shades, Today, WidgetSize.Medium);

            var window = new WidgetWindow();
            window.Bind(viewModel);

            // WPF Window chưa hiển thị không thể render trực tiếp vào bitmap,
            // nên render phần nội dung (RootBorder) bên trong nó.
            var content = (FrameworkElement)window.Content;

            content.Measure(new Size(480, 220));
            content.Arrange(new Rect(0, 0, 480, 220));
            content.UpdateLayout();

            var bitmap = new RenderTargetBitmap(480, 220, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(content);

            var pixels = new byte[480 * 220 * 4];
            bitmap.CopyPixels(pixels, 480 * 4, 0);

            var distinctColors = new HashSet<int>();
            var opaquePixels = 0;

            // PixelFormats.Pbgra32 lưu byte theo thứ tự B, G, R, A.
            for (var i = 0; i < pixels.Length; i += 4)
            {
                if (pixels[i + 3] > 0)
                {
                    opaquePixels++;
                    distinctColors.Add((pixels[i + 2] << 16) | (pixels[i + 1] << 8) | pixels[i]);
                }
            }

            opaquePixels.Should().BeGreaterThan(5000, "widget phải vẽ được nội dung, không phải màn hình trống");

            distinctColors.Count.Should().BeGreaterThanOrEqualTo(3,
                "phải thấy nhiều màu khác nhau: nền, ô rỗng, ô đã đánh dấu và chữ");

            // Ô đã đánh dấu phải dùng màu Level4 (#216E39) của bảng màu.
            var level4 = Color.FromRgb(0x21, 0x6E, 0x39);
            var foundLevel4 = false;
            var foundEmpty = false;
            var empty = Color.FromRgb(0xEB, 0xED, 0xF0);

            for (var i = 0; i < pixels.Length; i += 4)
            {
                if (pixels[i + 3] < 200)
                {
                    continue;
                }

                var r = pixels[i + 2];
                var g = pixels[i + 1];
                var b = pixels[i];

                if (Math.Abs(r - level4.R) < 8 && Math.Abs(g - level4.G) < 8 && Math.Abs(b - level4.B) < 8)
                {
                    foundLevel4 = true;
                }

                if (Math.Abs(r - empty.R) < 4 && Math.Abs(g - empty.G) < 4 && Math.Abs(b - empty.B) < 4)
                {
                    foundEmpty = true;
                }
            }

            foundLevel4.Should().BeTrue("các ô đã đánh dấu phải được tô màu Level4 của bảng màu heatmap");
            foundEmpty.Should().BeTrue("các ô chưa đánh dấu phải được tô màu ô rỗng của bảng màu");
        });
    }

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
