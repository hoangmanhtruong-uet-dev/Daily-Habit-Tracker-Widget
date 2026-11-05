namespace HabitGrid.Platform.Desktop;

/// <summary>
/// Hình chữ nhật cửa sổ theo toạ độ màn hình.
/// </summary>
public readonly record struct WindowBounds(int Left, int Top, int Width, int Height)
{
    public int Right => Left + Width;

    public int Bottom => Top + Height;
}

/// <summary>
/// Thao tác cửa sổ ở mức Win32, dùng cho việc kéo-thả widget.
/// <para>
/// Vị trí cửa sổ luôn được xử lý theo toạ độ MÀN HÌNH, kể cả khi widget đã được
/// reparent thành child của WorkerW (lúc đó toạ độ Win32 của cửa sổ là tương đối với cha).
/// </para>
/// </summary>
public static class NativeWindow
{
    /// <summary>Lấy hình chữ nhật của cửa sổ theo toạ độ màn hình.</summary>
    public static WindowBounds GetScreenBounds(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !WindowInterop.GetWindowRect(hwnd, out var rect))
        {
            return new WindowBounds(0, 0, 0, 0);
        }

        return new WindowBounds(rect.Left, rect.Top, rect.Width, rect.Height);
    }

    /// <summary>
    /// Di chuyển cửa sổ tới một vị trí tuyệt đối trên màn hình.
    /// Tự động quy đổi sang toạ độ tương đối với cửa sổ cha nếu widget đang được ghim vào WorkerW.
    /// </summary>
    public static void MoveToScreenPosition(IntPtr hwnd, int screenLeft, int screenTop, int width, int height)
    {
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var parent = WindowInterop.GetParent(hwnd);
        var x = screenLeft;
        var y = screenTop;

        if (parent != IntPtr.Zero)
        {
            var point = new WindowInterop.NativePoint { X = screenLeft, Y = screenTop };
            WindowInterop.ScreenToClient(parent, ref point);

            x = point.X;
            y = point.Y;
        }

        WindowInterop.SetWindowPos(
            hwnd,
            IntPtr.Zero,
            x,
            y,
            width,
            height,
            WindowInterop.SWP_NOZORDER | WindowInterop.SWP_NOACTIVATE);
    }

    /// <summary>Vị trí con trỏ chuột hiện tại theo toạ độ màn hình.</summary>
    public static (int X, int Y) GetCursorPosition()
    {
        return WindowInterop.GetCursorPos(out var point) ? (point.X, point.Y) : (0, 0);
    }

    /// <summary>Đăng ký một thông điệp hệ thống dùng để theo dõi (ví dụ "TaskbarCreated").</summary>
    public static uint RegisterMessage(string message) => WindowInterop.RegisterWindowMessage(message);

    /// <summary>Lấy cửa sổ cha hiện tại. Dùng để kiểm chứng widget đã được ghim vào lớp desktop.</summary>
    public static IntPtr GetParent(IntPtr hwnd) => WindowInterop.GetParent(hwnd);

    /// <summary>True nếu cửa sổ còn tồn tại.</summary>
    public static bool IsValid(IntPtr hwnd) => hwnd != IntPtr.Zero && WindowInterop.IsWindowVisible(hwnd);
}
