namespace HabitGrid.Platform.Desktop;

/// <summary>
/// Hình chữ nhật theo toạ độ thiết bị (pixel vật lý), không phụ thuộc WPF.
/// </summary>
public readonly record struct ScreenRect(int Left, int Top, int Width, int Height)
{
    public int Right => Left + Width;

    public int Bottom => Top + Height;
}

/// <summary>
/// Thông tin một màn hình hiển thị.
/// </summary>
/// <param name="DevicePath">Định danh màn hình, ví dụ "\\.\DISPLAY1".</param>
/// <param name="WorkArea">Vùng làm việc (không tính taskbar) theo pixel vật lý.</param>
/// <param name="DpiScale">Tỷ lệ DPI của màn hình (1.0 = 100%).</param>
/// <param name="IsPrimary">True nếu là màn hình chính.</param>
public sealed record ScreenDescriptor(string DevicePath, ScreenRect WorkArea, double DpiScale, bool IsPrimary);
