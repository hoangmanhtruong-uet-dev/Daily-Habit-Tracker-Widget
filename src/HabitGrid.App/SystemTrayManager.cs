using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using HabitGrid.Core.Settings;
using HabitGrid.Platform.Desktop;

namespace HabitGrid.App;

/// <summary>
/// Quản lý biểu tượng khay hệ thống (System Tray NotifyIcon) và menu ngữ cảnh bằng Win32 API.
/// </summary>
public sealed class SystemTrayManager : IDisposable
{
    private readonly HwndSource _hwndSource;
    private readonly AppSettings _settings;
    private readonly Action _openDashboard;
    private readonly Action _openSettings;
    private readonly Action _toggleWidgets;
    private readonly Action _exitApp;
    private ContextMenu? _contextMenu;
    private MenuItem? _editModeItem;
    private bool _disposed;

    public SystemTrayManager(
        AppSettings settings,
        Action openDashboard,
        Action openSettings,
        Action toggleWidgets,
        Action exitApp)
    {
        _settings = settings;
        _openDashboard = openDashboard;
        _openSettings = openSettings;
        _toggleWidgets = toggleWidgets;
        _exitApp = exitApp;

        var parameters = new HwndSourceParameters("HabitGridTrayWindow")
        {
            HwndSourceHook = OnHook,
            WindowStyle = 0
        };

        _hwndSource = new HwndSource(parameters);
        InstallTrayIcon();
    }

    private void InstallTrayIcon()
    {
        var hIcon = GetApplicationIconHandle();

        var data = new WindowInterop.NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<WindowInterop.NOTIFYICONDATA>(),
            hWnd = _hwndSource.Handle,
            uID = 1001,
            uFlags = WindowInterop.NIF_MESSAGE | WindowInterop.NIF_ICON | WindowInterop.NIF_TIP,
            uCallbackMessage = WindowInterop.WM_TRAYICON,
            hIcon = hIcon,
            szTip = "HabitGrid — Lưới thói quen Desktop"
        };

        WindowInterop.Shell_NotifyIcon(WindowInterop.NIM_ADD, ref data);
    }

    private static IntPtr GetApplicationIconHandle()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath))
            {
                var hIcon = WindowInterop.ExtractIcon(IntPtr.Zero, exePath, 0);
                if (hIcon != IntPtr.Zero && hIcon != new IntPtr(1))
                {
                    return hIcon;
                }
            }
        }
        catch
        {
            // fallback
        }

        return WindowInterop.LoadIcon(IntPtr.Zero, new IntPtr(32512)); // IDI_APPLICATION
    }

    private IntPtr OnHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WindowInterop.WM_TRAYICON)
        {
            var eventId = lParam.ToInt32();
            if (eventId == WindowInterop.WM_LBUTTONUP)
            {
                Application.Current.Dispatcher.Invoke(_openDashboard);
                handled = true;
            }
            else if (eventId == WindowInterop.WM_RBUTTONUP)
            {
                Application.Current.Dispatcher.Invoke(ShowContextMenu);
                handled = true;
            }
        }

        return IntPtr.Zero;
    }

    private void ShowContextMenu()
    {
        _contextMenu ??= BuildContextMenu();

        if (_editModeItem != null)
        {
            _editModeItem.IsChecked = _settings.EditMode;
        }

        _contextMenu.IsOpen = false;
        _contextMenu.IsOpen = true;
    }

    private ContextMenu BuildContextMenu()
    {
        var menu = new ContextMenu();

        var openItem = new MenuItem
        {
            Header = "Mở bảng quản lý",
            FontWeight = FontWeights.Bold
        };
        openItem.Click += (_, _) => _openDashboard();
        menu.Items.Add(openItem);

        _editModeItem = new MenuItem
        {
            Header = "Chế độ chỉnh sửa Widget",
            IsCheckable = true,
            IsChecked = _settings.EditMode
        };
        _editModeItem.Click += (_, _) =>
        {
            _settings.EditMode = !_settings.EditMode;
            _editModeItem.IsChecked = _settings.EditMode;
        };
        menu.Items.Add(_editModeItem);

        var toggleItem = new MenuItem
        {
            Header = "Hiện / Ẩn tất cả Widget"
        };
        toggleItem.Click += (_, _) => _toggleWidgets();
        menu.Items.Add(toggleItem);

        menu.Items.Add(new Separator());

        var settingsItem = new MenuItem
        {
            Header = "Cài đặt"
        };
        settingsItem.Click += (_, _) => _openSettings();
        menu.Items.Add(settingsItem);

        menu.Items.Add(new Separator());

        var exitItem = new MenuItem
        {
            Header = "Thoát hoàn toàn"
        };
        exitItem.Click += (_, _) => _exitApp();
        menu.Items.Add(exitItem);

        return menu;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        var data = new WindowInterop.NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<WindowInterop.NOTIFYICONDATA>(),
            hWnd = _hwndSource.Handle,
            uID = 1001
        };

        WindowInterop.Shell_NotifyIcon(WindowInterop.NIM_DELETE, ref data);
        _hwndSource.Dispose();
    }
}
