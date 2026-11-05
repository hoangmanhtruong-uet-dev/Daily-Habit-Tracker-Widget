using System.Windows.Interop;
using HabitGrid.Platform.Desktop;

namespace HabitGrid.App;

/// <summary>
/// Lắng nghe các sự kiện hệ thống ảnh hưởng tới widget:
/// - Explorer khởi động lại (TaskbarCreated): phải ghim lại widget.
/// - Đổi độ phân giải / rút màn hình (WM_DISPLAYCHANGE): phải tính lại vị trí.
/// - Đổi DPI (WM_DPICHANGED): WPF tự co giãn, nhưng vị trí cần chỉnh lại.
/// </summary>
public sealed class SystemEventMonitor : IDisposable
{
    private const int WM_DISPLAYCHANGE = 0x007E;
    private const int WM_DPICHANGED = 0x02E0;

    /// <summary>Cửa sổ con của ẩn (message-only window) — không hiện ra desktop.</summary>
    private static readonly IntPtr HWND_MESSAGE = new(-3);

    private readonly HwndSource _source;
    private readonly uint _taskbarCreatedMessage;
    private bool _disposed;

    public SystemEventMonitor()
    {
        _taskbarCreatedMessage = NativeWindow.RegisterMessage("TaskbarCreated");

        var parameters = new HwndSourceParameters("HabitGridSystemEvents")
        {
            HwndSourceHook = OnMessage,
            ParentWindow = HWND_MESSAGE,
            WindowStyle = 0
        };

        _source = new HwndSource(parameters);
        _source.AddHook(OnMessage);
    }

    /// <summary>Được gọi khi Explorer khởi động lại và cần ghim lại widget.</summary>
    public event EventHandler? ExplorerRestarted;

    /// <summary>Được gọi khi cấu hình màn hình thay đổi.</summary>
    public event EventHandler? DisplayChanged;

    /// <summary>Được gọi khi DPI của màn hình chứa widget thay đổi.</summary>
    public event EventHandler? DpiChanged;

    private IntPtr OnMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == (int)_taskbarCreatedMessage && _taskbarCreatedMessage != 0)
        {
            ExplorerRestarted?.Invoke(this, EventArgs.Empty);
            handled = true;
        }
        else if (msg == WM_DISPLAYCHANGE)
        {
            DisplayChanged?.Invoke(this, EventArgs.Empty);
            handled = true;
        }
        else if (msg == WM_DPICHANGED)
        {
            DpiChanged?.Invoke(this, EventArgs.Empty);
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _source.RemoveHook(OnMessage);
        _source.Dispose();
    }
}
