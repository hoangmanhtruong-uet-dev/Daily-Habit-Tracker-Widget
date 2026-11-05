using System.Diagnostics;
using HabitGrid.Platform.Desktop;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace HabitGrid.Poc.WinUI;

public sealed partial class PocWindow : Window
{
    private readonly DesktopHostService _host = new();
    private readonly IntPtr _hwnd;
    private int _clicks;

    public PocWindow()
    {
        InitializeComponent();
        _hwnd = WindowNative.GetWindowHandle(this);

        var appWindow = AppWindow;
        appWindow.IsShownInSwitchers = false;
        if (appWindow.Presenter is OverlappedPresenter p)
        {
            p.IsResizable = false;
            p.IsMaximizable = false;
            p.IsMinimizable = false;
            p.SetBorderAndTitleBar(false, false);
        }

        appWindow.MoveAndResize(new Windows.Graphics.RectInt32(200, 200, 360, 220));

        var mode = Environment.GetEnvironmentVariable("POC_MODE") switch
        {
            "bottom" => DesktopPinningMode.BottomMost,
            "top" => DesktopPinningMode.AlwaysOnTop,
            _ => DesktopPinningMode.DesktopLevel
        };

        var result = _host.Attach(_hwnd, mode);
        StatusText.Text = $"Requested={result.RequestedMode} Applied={result.AppliedMode} Ok={result.Success}";
        Log($"hwnd=0x{_hwnd:X} {StatusText.Text} {result.Message}");
    }

    private void OnClick(object sender, RoutedEventArgs e)
    {
        _clicks++;
        ClickButton.Content = $"Click test: {_clicks}";
        Log($"click {_clicks}");
    }

    private void OnRepin(object sender, RoutedEventArgs e)
    {
        var r = _host.EnsurePinned(_hwnd);
        Log($"repin => {r.AppliedMode}");
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private static void Log(string line)
    {
        var path = Path.Combine(Path.GetTempPath(), "habitgrid-poc.log");
        File.AppendAllText(path, $"{DateTime.Now:HH:mm:ss.fff} {line}{Environment.NewLine}");
        Debug.WriteLine(line);
    }
}
