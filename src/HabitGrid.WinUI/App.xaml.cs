using System.Runtime.InteropServices;
using HabitGrid.Presentation.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace HabitGrid.WinUI;

public partial class App : Application
{
    public static AppHost Host { get; private set; } = null!;

    public static MainWindow? MainWindowInstance { get; private set; }

    public App()
    {
        InitializeComponent();
        RegisterGlobalErrorHandlers();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Mọi event handler async phải có try/catch: một lỗi khởi động không được làm app thoát im lặng.
        try
        {
            var arguments = Environment.GetCommandLineArgs();
            var smoke = arguments.Contains("--smoke", StringComparer.OrdinalIgnoreCase);

            // Smoke test ghi dữ liệu: từ chối NGAY, trước cả khi dựng host (host có thể tạo/nhập CSDL).
            if (smoke && !AppPaths.IsCustomDataDir)
            {
                // Không log, không hộp thoại: không được tạo bất cứ thứ gì trong thư mục dữ liệu thật.
                System.Diagnostics.Debug.WriteLine($"--smoke bắt buộc đặt {AppPaths.DataDirVariable}.");
                Environment.Exit(3);
                return;
            }

            // Single instance TRƯỚC khi mở CSDL để bản thứ hai không đụng vào dữ liệu.
            var main = AppInstance.FindOrRegisterForKey(AppPaths.InstanceKey);
            if (!main.IsCurrent)
            {
                await main.RedirectActivationToAsync(AppInstance.GetCurrent().GetActivatedEventArgs());
                Environment.Exit(0);
                return;
            }

            Host = await AppHost.CreateAsync(DispatcherQueue.GetForCurrentThread());

            var window = new MainWindow();
            MainWindowInstance = window;
            main.Activated += (_, _) => window.DispatcherQueue.TryEnqueue(window.BringToFront);
            window.Activate();

            if (smoke)
            {
                _ = SmokeRunner.RunAsync(window);
            }
        }
        catch (Exception ex)
        {
            ReportFatal(ex);
        }
    }

    private void RegisterGlobalErrorHandlers()
    {
        UnhandledException += (_, e) =>
        {
            // Ghi log và giữ app sống; trang lỗi thân thiện sẽ do shell hiển thị ở gói sau.
            LogSafe("UnhandledException", e.Exception);
            e.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            LogSafe("AppDomain.UnhandledException", e.ExceptionObject as Exception);

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            LogSafe("UnobservedTaskException", e.Exception);
            e.SetObserved();
        };
    }

    private static void LogSafe(string where, Exception? ex)
    {
        try
        {
            var logger = Host is null ? new FileLogger(AppPaths.LogDir) : Host.Get<IAppLogger>();
            logger.Error(where, ex);
        }
        catch
        {
            // không để lỗi ghi log che lỗi gốc
        }
    }

    private static void ReportFatal(Exception ex)
    {
        LogSafe("Fatal startup error", ex);
        MessageBox(
            IntPtr.Zero,
            $"HabitGrid không thể khởi động.\n\n{ex.Message}\n\nNhật ký: {AppPaths.LogDir}",
            "HabitGrid",
            0x10);
        Environment.Exit(1);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);
}
