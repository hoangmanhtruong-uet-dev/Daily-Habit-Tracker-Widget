using Microsoft.UI.Xaml;

namespace HabitGrid.Poc.WinUI;

public partial class App : Application
{
    private Window? _window;

    public App() => InitializeComponent();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new PocWindow();
        _window.Activate();
    }
}
