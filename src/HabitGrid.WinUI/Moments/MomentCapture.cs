using System.Runtime.InteropServices.WindowsRuntime;
using HabitGrid.Core.Domain;
using HabitGrid.Presentation.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;

namespace HabitGrid.WinUI.Moments;

/// <summary>
/// Lấy ảnh khoảnh khắc: chụp bằng camera (CameraCaptureUI của Windows App SDK) hoặc chọn file; thu nhỏ còn tối đa 1600 px,
/// lưu JPEG (siêu dữ liệu EXIF như vị trí GPS bị bỏ vì ảnh được mã hóa lại).
/// </summary>
public static class MomentCapture
{
    public const uint MaxSide = 1600;

    public static async Task<StorageFile?> CaptureAsync()
    {
        if (App.MainWindowInstance is not { } window)
        {
            return null;
        }

        var camera = new Microsoft.Windows.Media.Capture.CameraCaptureUI(window.AppWindow.Id);
        camera.PhotoSettings.Format = Microsoft.Windows.Media.Capture.CameraCaptureUIPhotoFormat.Jpeg;
        camera.PhotoSettings.AllowCropping = true;
        return await camera.CaptureFileAsync(Microsoft.Windows.Media.Capture.CameraCaptureUIMode.Photo);
    }

    public static async Task<StorageFile?> PickAsync()
    {
        var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.PicturesLibrary, ViewMode = PickerViewMode.Thumbnail };
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".png");

        if (App.MainWindowInstance is { } window)
        {
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(window));
        }

        return await picker.PickSingleFileAsync();
    }

    /// <summary>Đọc, xoay theo EXIF, thu nhỏ và mã hóa lại thành JPEG.</summary>
    public static async Task<byte[]> NormalizeAsync(StorageFile file)
    {
        using var input = await file.OpenReadAsync();
        var decoder = await BitmapDecoder.CreateAsync(input);
        var scale = Math.Min(1.0, MaxSide / (double)Math.Max(decoder.OrientedPixelWidth, decoder.OrientedPixelHeight));
        var transform = new BitmapTransform
        {
            ScaledWidth = (uint)Math.Max(1, Math.Round(decoder.PixelWidth * scale)),
            ScaledHeight = (uint)Math.Max(1, Math.Round(decoder.PixelHeight * scale)),
            InterpolationMode = BitmapInterpolationMode.Fant
        };

        using var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, transform, ExifOrientationMode.RespectExifOrientation, ColorManagementMode.ColorManageToSRgb);
        using var output = new InMemoryRandomAccessStream();
        var options = new BitmapPropertySet { ["ImageQuality"] = new BitmapTypedValue(0.85, Windows.Foundation.PropertyType.Single) };
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, output, options);
        encoder.SetSoftwareBitmap(bitmap);
        await encoder.FlushAsync();

        var bytes = new byte[output.Size];
        output.Seek(0);
        await output.ReadAsync(bytes.AsBuffer(), (uint)bytes.Length, InputStreamOptions.None);
        return bytes;
    }

    /// <summary>Ảnh thu nhỏ để hiển thị (giải mã ở kích thước nhỏ cho nhẹ bộ nhớ).</summary>
    public static BitmapImage Thumbnail(string path, int width)
    {
        var image = new BitmapImage { DecodePixelWidth = width, DecodePixelType = DecodePixelType.Logical };
        _ = LoadAsync(image, path);
        return image;
    }

    /// <summary>Nạp qua luồng file (không dùng URI file:// vốn không chắc chạy ở app không đóng gói); file hỏng thì để trống.</summary>
    private static async Task LoadAsync(BitmapImage image, string path)
    {
        try
        {
            var bytes = await File.ReadAllBytesAsync(path);
            using var stream = new InMemoryRandomAccessStream();
            await stream.WriteAsync(bytes.AsBuffer());
            stream.Seek(0);
            await image.SetSourceAsync(stream);
        }
        catch (Exception)
        {
            // Ảnh bị xóa/hỏng: giữ ô trống, không làm hỏng trang.
        }
    }

    /// <summary>Đợi ảnh của một BitmapImage nạp xong (dùng trước khi render thẻ ra PNG).</summary>
    public static async Task<BitmapImage> LoadNowAsync(string path, int width)
    {
        var image = new BitmapImage { DecodePixelWidth = width, DecodePixelType = DecodePixelType.Logical };
        await LoadAsync(image, path);
        return image;
    }

    /// <summary>Lưu một phần tử giao diện thành PNG ở độ phân giải <paramref name="scale"/> lần (thẻ chia sẻ 1080 px ngang).</summary>
    public static async Task<byte[]> RenderPngAsync(UIElement element, int pixelWidth, int pixelHeight)
    {
        var bitmap = new RenderTargetBitmap();
        await bitmap.RenderAsync(element, pixelWidth, pixelHeight);
        var pixels = await bitmap.GetPixelsAsync();

        using var output = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, output);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, 96, 96, pixels.ToArray());
        await encoder.FlushAsync();

        var bytes = new byte[output.Size];
        output.Seek(0);
        await output.ReadAsync(bytes.AsBuffer(), (uint)bytes.Length, InputStreamOptions.None);
        return bytes;
    }
}
