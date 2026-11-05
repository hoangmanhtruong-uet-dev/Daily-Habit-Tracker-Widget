using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;

namespace HabitGrid.WinUI;

/// <summary>Xuất ảnh PNG của một phần tử giao diện (dùng trong smoke test để có ảnh chụp đính kèm báo cáo).</summary>
public static class Shot
{
    public static string Directory => Path.Combine(AppPaths.DataDir, "shots");

    private static Action ApplyBackground(UIElement element)
    {
        var dark = (element as FrameworkElement)?.ActualTheme != ElementTheme.Light;
        var brush = new Microsoft.UI.Xaml.Media.SolidColorBrush(dark
            ? Windows.UI.Color.FromArgb(255, 32, 32, 36)
            : Windows.UI.Color.FromArgb(255, 247, 247, 250));

        return Paint(element, brush);
    }

    private static Action Paint(UIElement element, Microsoft.UI.Xaml.Media.Brush brush)
    {
        switch (element)
        {
            case Microsoft.UI.Xaml.Controls.UserControl { Content: UIElement inner }:
                return Paint(inner, brush);
            case Microsoft.UI.Xaml.Controls.ScrollViewer { Content: UIElement scrolled }:
                return Paint(scrolled, brush);
            case Microsoft.UI.Xaml.Controls.Panel panel when panel.Background is null:
                panel.Background = brush;
                return () => panel.Background = null;
            case Microsoft.UI.Xaml.Controls.Control control when control.Background is null:
                control.Background = brush;
                return () => control.Background = null;
            default:
                return () => { };
        }
    }

    public static async Task<string> SaveAsync(UIElement element, string name)
    {
        System.IO.Directory.CreateDirectory(Directory);
        var path = Path.Combine(Directory, name + ".png");

        // RenderTargetBitmap không chụp nền Mica/hộp thoại nên chữ theo theme có thể thành chữ trắng trên nền trắng.
        // Tạm đặt nền đặc theo theme hiện tại khi chụp rồi trả lại.
        var restore = ApplyBackground(element);

        var bitmap = new RenderTargetBitmap();
        try
        {
            await bitmap.RenderAsync(element);
        }
        finally
        {
            restore();
        }

        var pixels = await bitmap.GetPixelsAsync();

        using var file = File.Create(path);
        using var stream = file.AsRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            (uint)bitmap.PixelWidth,
            (uint)bitmap.PixelHeight,
            96,
            96,
            pixels.ToArray());
        await encoder.FlushAsync();
        return path;
    }
}
