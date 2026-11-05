using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using HabitGrid.Core.Models;
using HabitGrid.Platform.Desktop;

namespace HabitGrid.App;

/// <summary>
/// Bảng điều khiển của Giai đoạn 0 (POC ghim widget).
/// Cho phép chọn thói quen, đổi 3 chế độ ghim và ghi lại kết quả 7 tiêu chí POC.
/// </summary>
public partial class PocWindow : Window
{
    private readonly AppServices _services;
    private readonly List<Habit> _habits = new();
    private bool _suppressEvents;

    public PocWindow()
    {
        InitializeComponent();

        _services = App.Services;

        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        EnvironmentText.Text = BuildEnvironmentText();

        _services.Widgets.StatusChanged += OnStatusChanged;
        _services.SystemEvents.ExplorerRestarted += OnExplorerRestarted;
        _services.SystemEvents.DisplayChanged += OnDisplayChanged;

        await ReloadAsync();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _services.Widgets.StatusChanged -= OnStatusChanged;
        _services.SystemEvents.ExplorerRestarted -= OnExplorerRestarted;
        _services.SystemEvents.DisplayChanged -= OnDisplayChanged;
    }

    private async Task ReloadAsync()
    {
        _suppressEvents = true;

        var habits = await _services.Habits.GetAllIncludingArchivedAsync();

        _habits.Clear();
        _habits.AddRange(habits);

        HabitList.ItemsSource = _habits;

        // Đồng bộ trạng thái checkbox với dữ liệu đã lưu.
        await Task.Yield();
        _suppressEvents = false;

        await _services.Widgets.LoadAsync();
        UpdateStatus($"Đã nạp {_habits.Count} thói quen.");
    }

    private async void OnCreateDemoClick(object sender, RoutedEventArgs e)
    {
        await _services.Widgets.CreateDemoHabitsAsync();
        await ReloadAsync();
    }

    private async void OnReloadClick(object sender, RoutedEventArgs e)
    {
        await ReloadAsync();
    }

    private async void OnApplyPinningClick(object sender, RoutedEventArgs e)
    {
        var mode = GetSelectedMode();
        await _services.Widgets.ApplyPinningAsync(mode);
    }

    private void OnReattachClick(object sender, RoutedEventArgs e)
    {
        _services.Widgets.ReattachAll();
    }

    private void OnEditModeChanged(object sender, RoutedEventArgs e)
    {
        _services.Widgets.SetEditMode(EditModeCheckBox.IsChecked == true);
    }

    private async void OnWidgetVisibilityChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressEvents || sender is not CheckBox { IsLoaded: true } checkBox || checkBox.Tag is not Guid habitId)
        {
            return;
        }

        await _services.Widgets.SetWidgetVisibleAsync(habitId, checkBox.IsChecked == true);
    }

    private async void OnSizeChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressEvents || sender is not ComboBox { IsLoaded: true } comboBox || comboBox.Tag is not Guid habitId)
        {
            return;
        }

        if (comboBox.SelectedItem is not ComboBoxItem item || item.Tag is not string sizeText)
        {
            return;
        }

        var size = (WidgetSize)int.Parse(sizeText);
        var state = await _services.WidgetStates.GetAsync(habitId) ?? new WidgetState { HabitId = habitId };
        state.Size = size;

        await _services.WidgetStates.UpsertAsync(state);
        UpdateStatus($"Đã đổi cỡ widget. Áp dụng lại để thấy thay đổi.");
    }

    private void OnSaveReportClick(object sender, RoutedEventArgs e)
    {
        var path = ResolveReportPath();
        var report = BuildReport();

        File.WriteAllText(path, report);

        UpdateStatus($"Đã lưu báo cáo POC tại: {path}");
    }

    private DesktopPinningMode GetSelectedMode()
    {
        if (ModeBottomMost.IsChecked == true)
        {
            return DesktopPinningMode.BottomMost;
        }

        if (ModeAlwaysOnTop.IsChecked == true)
        {
            return DesktopPinningMode.AlwaysOnTop;
        }

        return DesktopPinningMode.DesktopLevel;
    }

    private void OnStatusChanged(object? sender, string message) => UpdateStatus(message);

    private void OnExplorerRestarted(object? sender, EventArgs e) =>
        Dispatcher.Invoke(() => UpdateStatus("Explorer khởi động lại — đã ghim lại widget."));

    private async void OnDisplayChanged(object? sender, EventArgs e) =>
        await Dispatcher.InvokeAsync(async () => await _services.Widgets.RepositionAllAsync());

    private void UpdateStatus(string message) => StatusText.Text = message;

    private static string BuildEnvironmentText()
    {
        var screens = new WindowsScreenProvider().GetScreens();
        var screenText = string.Join(", ", screens.Select(s =>
            $"{s.DevicePath} [{s.WorkArea.Width}x{s.WorkArea.Height} @ {s.DpiScale:P0}]"));

        return $"Windows {Environment.OSVersion.Version} • .NET {Environment.Version} • Màn hình: {screenText}";
    }

    private string BuildReport()
    {
        var builder = new System.Text.StringBuilder();

        builder.AppendLine("# Báo cáo POC — Ghim Widget Desktop");
        builder.AppendLine();
        builder.AppendLine($"- Ngày: {DateTime.Now:yyyy-MM-dd HH:mm}");
        builder.AppendLine($"- Máy: {Environment.MachineName}");
        builder.AppendLine($"- Hệ điều hành: Windows {Environment.OSVersion.Version}");
        builder.AppendLine($"- Chế độ ghim đang thử: {_services.Widgets.PinningMode}");
        builder.AppendLine();

        var screens = new WindowsScreenProvider().GetScreens();
        builder.AppendLine("## Màn hình");
        builder.AppendLine();
        builder.AppendLine("| Thiết bị | Vùng làm việc | DPI | Chính |");
        builder.AppendLine("|---|---|---|---|");

        foreach (var screen in screens)
        {
            builder.AppendLine($"| {screen.DevicePath} | {screen.WorkArea.Width}x{screen.WorkArea.Height} | {screen.DpiScale:P0} | {(screen.IsPrimary ? "Có" : "Không")} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Kết quả 7 tiêu chí");
        builder.AppendLine();

        var criteria = new (CheckBox Box, string Text)[]
        {
            (Criterion1, "Widget hiển thị trên hình nền, dưới cửa sổ thường"),
            (Criterion2, "Nhận được click/hover"),
            (Criterion3, "Sống sót sau Win+D"),
            (Criterion4, "Sống sót sau khởi động lại Explorer"),
            (Criterion5, "Đúng vị trí khi đổi DPI/đa màn hình"),
            (Criterion6, "Không hiện ở Alt+Tab/taskbar"),
            (Criterion7, "Không nhấp nháy, tài nguyên & tốc độ ổn định")
        };

        foreach (var (box, text) in criteria)
        {
            builder.AppendLine($"- [{(box.IsChecked == true ? "x" : " ")}] {text}");
        }

        builder.AppendLine();
        builder.AppendLine("## Quyết định");
        builder.AppendLine();

        var passed = criteria.Count(c => c.Box.IsChecked == true);

        if (passed >= 6)
        {
            builder.AppendLine("Đạt tiêu chí GO: phát hành v1.0 với **chế độ 1 (Desktop-level)** làm mặc định.");
        }
        else if (passed >= 3)
        {
            builder.AppendLine("Chưa đủ điều kiện GO với chế độ 1. Cần kiểm thử lại trên bản Windows khác.");
        }
        else
        {
            builder.AppendLine("**NO-GO cho chế độ 1.** Phát hành v1.0 với **chế độ 2 (Bottom-most)** làm mặc định và ghi rõ trong mô tả Store.");
        }

        return builder.ToString();
    }

    private static string ResolveReportPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "HabitGrid.sln")))
            {
                var docsPath = Path.Combine(directory.FullName, "docs");
                Directory.CreateDirectory(docsPath);
                return Path.Combine(docsPath, "poc-report.md");
            }

            directory = directory.Parent;
        }

        return Path.Combine(AppContext.BaseDirectory, "poc-report.md");
    }
}
