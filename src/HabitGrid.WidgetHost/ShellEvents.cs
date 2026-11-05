using System.Windows.Interop;
using HabitGrid.Platform.Desktop;

namespace HabitGrid.WidgetHost;

/// <summary>
/// Nghe sự kiện hệ thống: Explorer khởi động lại (TaskbarCreated) và đổi cấu hình màn hình.
/// Phải là cửa sổ top-level ẩn: cửa sổ message-only (HWND_MESSAGE) KHÔNG nhận thông điệp broadcast như TaskbarCreated.
/// </summary>
public sealed class ShellEvents : IDisposable
{
    private const int WmDisplayChange = 0x007E;

    private readonly HwndSource _source;
    private readonly uint _taskbarCreated;

    public ShellEvents()
    {
        _taskbarCreated = NativeWindow.RegisterMessage("TaskbarCreated");
        _source = new HwndSource(new HwndSourceParameters("HabitGridHostShellEvents") { WindowStyle = 0, Width = 0, Height = 0 });
        _source.AddHook(OnMessage);
    }

    /// <summary>Explorer vừa khởi động lại: WorkerW cũ (cha của widget) đã bị hủy cùng các widget.</summary>
    public event EventHandler? ExplorerRestarted;

    /// <summary>Cắm/rút màn hình hoặc đổi độ phân giải.</summary>
    public event EventHandler? DisplayChanged;

    public void Dispose()
    {
        _source.RemoveHook(OnMessage);
        _source.Dispose();
    }

    private IntPtr OnMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (_taskbarCreated != 0 && msg == (int)_taskbarCreated)
        {
            ExplorerRestarted?.Invoke(this, EventArgs.Empty);
        }
        else if (msg == WmDisplayChange)
        {
            DisplayChanged?.Invoke(this, EventArgs.Empty);
        }

        return IntPtr.Zero;
    }
}
