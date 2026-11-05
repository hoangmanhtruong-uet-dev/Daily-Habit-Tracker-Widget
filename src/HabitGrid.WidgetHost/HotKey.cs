using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace HabitGrid.WidgetHost;

/// <summary>Phím tắt toàn cục qua RegisterHotKey trên cửa sổ chỉ-nhận-thông-điệp: Win+Alt+&lt;phím&gt;, trùng thì thử Ctrl+Alt+&lt;phím&gt;.</summary>
public sealed class HotKey : IDisposable
{
    private const int WmHotKey = 0x0312;
    private const uint ModAlt = 0x1, ModControl = 0x2, ModWin = 0x8, ModNoRepeat = 0x4000;
    private static readonly IntPtr MessageOnlyParent = new(-3);

    private readonly HwndSource _source;
    private readonly int _id;

    /// <param name="letter">Chữ cái A–Z (mặc định H cho Quick check-in; F cho Focus).</param>
    public HotKey(char letter = 'H')
    {
        letter = char.ToUpperInvariant(letter);
        _id = 0x4800 + letter;
        var key = (uint)letter;
        _source = new HwndSource(new HwndSourceParameters("HabitGridHotKeyWindow" + letter) { ParentWindow = MessageOnlyParent, WindowStyle = 0 });
        _source.AddHook(OnMessage);

        if (RegisterHotKey(_source.Handle, _id, ModWin | ModAlt | ModNoRepeat, key))
        {
            Description = $"Win+Alt+{letter}";
        }
        else if (RegisterHotKey(_source.Handle, _id, ModControl | ModAlt | ModNoRepeat, key))
        {
            Description = $"Ctrl+Alt+{letter}";
        }
    }

    /// <summary>Tên phím đã đăng ký được; null nếu cả hai đều bị chiếm.</summary>
    public string? Description { get; }

    public event EventHandler? Pressed;

    public void Dispose()
    {
        if (Description is not null)
        {
            UnregisterHotKey(_source.Handle, _id);
        }

        _source.Dispose();
    }

    private IntPtr OnMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotKey && wParam.ToInt32() == _id)
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
