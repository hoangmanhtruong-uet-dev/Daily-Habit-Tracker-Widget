namespace HabitGrid.Platform.Desktop;

/// <summary>
/// Nguồn cung cấp thông tin các màn hình đang hoạt động.
/// Tách interface để phần toán học vị trí có thể kiểm thử mà không cần màn hình thật.
/// </summary>
public interface IScreenProvider
{
    /// <summary>Danh sách tất cả màn hình đang hoạt động.</summary>
    IReadOnlyList<ScreenDescriptor> GetScreens();

    /// <summary>Màn hình chứa toạ độ tuyệt đối (pixel vật lý) đã cho.</summary>
    ScreenDescriptor? GetScreenAt(int x, int y);

    /// <summary>Màn hình theo định danh thiết bị.</summary>
    ScreenDescriptor? GetScreenByDevicePath(string devicePath);

    /// <summary>Màn hình chính.</summary>
    ScreenDescriptor GetPrimaryScreen();
}
