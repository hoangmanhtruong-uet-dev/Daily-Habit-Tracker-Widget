using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using HabitGrid.Core.Models;
using HabitGrid.Core.Services;
using HabitGrid.Platform.Desktop;
using HabitGrid.UI.Widgets;

namespace WidgetHostSpike;

/// <summary>
/// Dựng N widget v1 (WPF) với dữ liệu giả rồi lấy mẫu RAM/CPU của chính tiến trình này.
/// Dùng: WidgetHostSpike.exe [số widget=3] [giây đo=60] [tệp kết quả] [size: Small|Medium|Large]
/// </summary>
public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        var count = args.Length > 0 ? int.Parse(args[0], CultureInfo.InvariantCulture) : 3;
        var seconds = args.Length > 1 ? int.Parse(args[1], CultureInfo.InvariantCulture) : 60;
        var output = args.Length > 2 ? args[2] : "spike-result.md";
        var size = args.Length > 3 ? Enum.Parse<WidgetSize>(args[3], true) : WidgetSize.Large;

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var process = Process.GetCurrentProcess();
        var samples = new List<(double T, double Ws, double Priv, double Cpu)>();
        var windows = new List<WidgetWindow>();
        var screen = new WindowsScreenProvider();
        var host = new DesktopHostService(screen);
        var today = DateOnly.FromDateTime(DateTime.Now);
        var generator = new PaletteGenerator();
        var random = new Random(7);
        var pinNotes = new List<string>();

        for (var i = 0; i < count; i++)
        {
            var habit = new Habit { Name = $"Habit {i + 1}", PaletteId = "default", StartDate = today.AddDays(-400) };
            var values = new Dictionary<DateOnly, int>();

            for (var d = 0; d < 380; d++)
            {
                if (random.Next(100) < 65)
                {
                    values[today.AddDays(-d)] = 1;
                }
            }

            var vm = new WidgetViewModel(habit, values, generator.GetPreset(habit.PaletteId, true), today, size, true);
            var window = new WidgetWindow(screen);
            window.Bind(vm);
            window.UseDesktopHost(host);
            window.ApplyThemeColors(true);
            window.Show();
            window.SetScreenPosition(40 + i * 40, 40 + i * 40);
            var pin = window.ApplyPinning(DesktopPinningMode.DesktopLevel);
            pinNotes.Add($"{i + 1}: fallback={pin.UsedFallback} {pin.Message}");
            windows.Add(window);
        }

        var started = Stopwatch.StartNew();
        var lastCpu = process.TotalProcessorTime;
        var lastAt = started.Elapsed;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };

        timer.Tick += (_, _) =>
        {
            process.Refresh();
            var cpuNow = process.TotalProcessorTime;
            var now = started.Elapsed;
            var cpu = (cpuNow - lastCpu).TotalSeconds / (now - lastAt).TotalSeconds / Environment.ProcessorCount * 100;
            lastCpu = cpuNow;
            lastAt = now;
            samples.Add((now.TotalSeconds, process.WorkingSet64 / 1048576.0, process.PrivateMemorySize64 / 1048576.0, cpu));

            if (now.TotalSeconds >= seconds)
            {
                timer.Stop();
                Write(output, count, size, seconds, samples, pinNotes);
                app.Shutdown();
            }
        };

        timer.Start();
        app.Run();
        return 0;
    }

    private static void Write(string path, int count, WidgetSize size, int seconds, List<(double T, double Ws, double Priv, double Cpu)> samples, List<string> pinNotes)
    {
        // Bỏ 15 giây đầu (khởi động/JIT) khi tính CPU nhàn rỗi.
        var steady = samples.Where(s => s.T >= 15).DefaultIfEmpty(samples[^1]).ToList();
        var sb = new StringBuilder();
        sb.AppendLine($"# WPF widget spike: {count} widget {size}, {seconds}s");
        sb.AppendLine();
        sb.AppendLine($"- OS: {Environment.OSVersion}, CPU cores: {Environment.ProcessorCount}, runtime: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
        sb.AppendLine($"- Cuối: working set {samples[^1].Ws:F1} MB, private {samples[^1].Priv:F1} MB");
        sb.AppendLine($"- Đỉnh working set: {samples.Max(s => s.Ws):F1} MB");
        sb.AppendLine($"- CPU nhàn rỗi trung bình (sau 15 s): {steady.Average(s => s.Cpu):F2}% (tổng trên mọi nhân)");
        sb.AppendLine($"- Ghim: {string.Join("; ", pinNotes)}");
        sb.AppendLine();
        sb.AppendLine("| t (s) | Working set MB | Private MB | CPU % |");
        sb.AppendLine("|---|---|---|---|");

        foreach (var s in samples)
        {
            sb.AppendLine($"| {s.T:F0} | {s.Ws:F1} | {s.Priv:F1} | {s.Cpu:F2} |");
        }

        System.IO.File.WriteAllText(path, sb.ToString());
    }
}
