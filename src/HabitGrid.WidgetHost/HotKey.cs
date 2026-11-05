using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace HabitGrid.WidgetHost;

/// <summary>Phím tắt toàn cục qua RegisterHotKey trên cửa sổ chỉ-nhận-thông-điệp. Mặc định Win+Alt+H, trùng thì thử Ctrl+Alt+H.</summary>
public sealed class HotKey : IDisposable
{
    private const int WmHotKey = 0x0312;
    private const uint ModAlt = 0x1, ModControl = 0x2, ModWin = 0x8, ModNoRepeat = 0x4000;
    private const uint KeyH = 0x48;
    private static readonly IntPtr MessageOnlyParent = new(-3);

    private readonly HwndSource _source;
    private const int Id = 0x4847;

    public HotKey()
    {
        _source = new HwndSource(new HwndSourceParameters("HabitGridHotKeyWindow") { ParentWindow = MessageOnlyParent, WindowStyle = 0 });
        _source.AddHook(OnMessage);

        if (RegisterHotKey(_source.Handle, Id, ModWin | ModAlt | ModNoRepeat, KeyH))
        {
            Description = "Win+Alt+H";
        }
        else if (RegisterHotKey(_source.Handle, Id, ModControl | ModAlt | ModNoRepeat, KeyH))
        {
            Description = "Ctrl+Alt+H";
        }
    }

    /// <summary>Tên phím đã đăng ký được; null nếu cả hai đều bị chiếm.</summary>
    public string? Description { get; }

    public event EventHandler? Pressed;

    public void Dispose()
    {
        if (Description is not null)
        {
            UnregisterHotKey(_source.Handle, Id);
        }

        _source.Dispose();
    }

    private IntPtr OnMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotKey && wParam.ToInt32() == Id)
        {
            handled = true;
            Pressed?.Invoke(this, EventArgs.Empty);
        }

        return IntPtr.Zero;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
