using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Text;
using System.Windows;
using HabitGrid.Core.Repositories;
using HabitGrid.Core.Settings;
using HabitGrid.Data.V3;

namespace HabitGrid.WidgetHost;

/// <summary>Cài đặt v1 chỉ dùng cho menu khay (chế độ sắp xếp); giữ trong bộ nhớ.</summary>
internal sealed class MemorySettings : ISettingsRepository
{
    private readonly Dictionary<string, string> _values = [];

    public Task<string?> GetAsync(string key) => Task.FromResult(_values.GetValueOrDefault(key));

    public Task<T> GetAsync<T>(string key, T defaultValue) =>
        Task.FromResult(_values.TryGetValue(key, out var raw) ? (T)Convert.ChangeType(raw, typeof(T), System.Globalization.CultureInfo.InvariantCulture) : defaultValue);

    public Task SetAsync<T>(string key, T value)
    {
        _values[key] = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        return Task.CompletedTask;
    }
}

public static class Program
{
    private const string DataDirVariable = "HABITGRID_DATA_DIR";

    [STAThread]
    public static int Main(string[] args)
    {
        var selfTest = args.Contains("--selftest", StringComparer.OrdinalIgnoreCase);
        var customDir = Environment.GetEnvironmentVariable(DataDirVariable);
        var dataDir = string.IsNullOrWhiteSpace(customDir)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HabitGrid")
            : Path.GetFullPath(customDir);
        var dbPath = Path.Combine(dataDir, HabitGridV3.FileName);

        // Một WidgetHost cho mỗi thư mục dữ liệu.
        var key = "HabitGrid.WidgetHost." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(dataDir)))[..12];
        using var mutex = new Mutex(true, key, out var isFirst);

        if (!isFirst)
        {
            return 0;
        }

        // Chưa có CSDL v3 (app chính chưa chạy lần nào): không tự tạo, thoát im lặng.
        if (!File.Exists(dbPath))
        {
            return selfTest ? 3 : 0;
        }

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var database = HabitGridV3.Open(dbPath);
        var settings = new AppSettings(new MemorySettings());
        var controller = new WidgetController(database) { EditMode = () => settings.EditMode };
        var exitCode = 0;

        app.Startup += async (_, _) =>
        {
            try
            {
                await controller.StartAsync();

                if (selfTest)
                {
                    exitCode = await SelfTest.RunAsync(controller, dbPath);
                    app.Shutdown();
                    return;
                }

                var tray = new HabitGrid.App.SystemTrayManager(
                    settings,
                    OpenMainApp,
                    OpenMainApp,
                    controller.ToggleVisibility,
                    () => app.Shutdown());
                app.Exit += (_, _) => tray.Dispose();

                Microsoft.Win32.SystemEvents.PowerModeChanged += (_, e) =>
                {
                    if (e.Mode == Microsoft.Win32.PowerModes.Resume)
                    {
                        app.Dispatcher.InvokeAsync(controller.RefreshAsync);
                    }
                };
            }
            catch (Exception ex)
            {
                Log(dataDir, ex);
                exitCode = 2;
                app.Shutdown();
            }
        };

        app.Run();
        controller.Dispose();
        database.Dispose();
        return exitCode;
    }

    private static void OpenMainApp()
    {
        foreach (var candidate in new[]
        {
            Path.Combine(AppContext.BaseDirectory, "HabitGrid.WinUI.exe"),
            Path.Combine(AppContext.BaseDirectory, "..", "HabitGrid.WinUI", "HabitGrid.WinUI.exe")
        })
        {
            if (File.Exists(candidate))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(candidate) { UseShellExecute = true });
                return;
            }
        }
    }

    private static void Log(string dataDir, Exception ex)
    {
        try
        {
            var dir = Path.Combine(dataDir, "logs");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "widgethost.log"), $"{DateTime.Now:HH:mm:ss} {ex}\n");
        }
        catch
        {
            // Không để việc ghi log làm hỏng tiến trình.
        }
    }
}
