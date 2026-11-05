using System.Runtime.InteropServices;
using HabitGrid.Presentation.Services;
using HabitGrid.Presentation.ViewModels;
using HabitGrid.WinUI.Pages;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinRT.Interop;

namespace HabitGrid.WinUI;

public sealed partial class MainWindow : Window
{
    private readonly ShellViewModel _shell = App.Host.Get<ShellViewModel>();
    private readonly Dictionary<string, NavigationViewItem> _items = [];

    public MainWindow()
    {
        InitializeComponent();

        Title = Ui.T("app.name");
        TitleText.Text = Ui.T("app.name");

        // Mica (Windows 11) tự lùi về nền thường trên máy không hỗ trợ.
        SystemBackdrop = new MicaBackdrop();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBar);
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1240, 800));

        BuildNavigation();
        Closed += (_, _) => Closing?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? Closing;

    public string CurrentPageKey => _shell.SelectedKey;

    private void BuildNavigation()
    {
        foreach (var item in _shell.MainItems)
        {
            var nav = CreateItem(item);
            _items[item.Key] = nav;
            Nav.MenuItems.Add(nav);
        }

        var settings = CreateItem(_shell.SettingsItem);
        _items[PageKeys.Settings] = settings;
        Nav.FooterMenuItems.Add(settings);

        Nav.SelectedItem = _items[PageKeys.Today];
    }

    private NavigationViewItem CreateItem(NavItem item) => new()
    {
        Content = Ui.T(item.TitleKey),
        Tag = item.Key,
        Icon = new FontIcon { Glyph = item.Glyph }
    };

    /// <summary>Chuyển tới trang theo khóa (dùng cho điều hướng và smoke test).</summary>
    public void NavigateTo(string key)
    {
        if (_items.TryGetValue(key, out var item))
        {
            Nav.SelectedItem = item;
        }
    }

    private void OnSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem { Tag: string key })
        {
            return;
        }

        _shell.SelectedKey = key;

        switch (key)
        {
            case PageKeys.Today:
                ContentFrame.Navigate(typeof(TodayPage), null, new Microsoft.UI.Xaml.Media.Animation.EntranceNavigationTransitionInfo());
                break;
            case PageKeys.Habits:
                ContentFrame.Navigate(typeof(HabitsPage), null, new Microsoft.UI.Xaml.Media.Animation.EntranceNavigationTransitionInfo());
                break;
            default:
                ContentFrame.Navigate(typeof(PlaceholderPage), key, new Microsoft.UI.Xaml.Media.Animation.EntranceNavigationTransitionInfo());
                break;
        }
    }

    /// <summary>Đưa cửa sổ lên trước (khi mở lần hai hoặc kích hoạt từ nơi khác).</summary>
    public void BringToFront()
    {
        var hwnd = WindowNative.GetWindowHandle(this);

        if (AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized } presenter)
        {
            presenter.Restore();
        }

        Activate();
        SetForegroundWindow(hwnd);
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
