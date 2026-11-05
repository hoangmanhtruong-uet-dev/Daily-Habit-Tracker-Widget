using System.Diagnostics;
using System.IO;
using System.Windows;
using HabitGrid.Core.Models;
using HabitGrid.Data.Backup;
using HabitGrid.UI.Services;
using Microsoft.Win32;

namespace HabitGrid.App;

/// <summary>
/// Thực hiện các hành động của trang cài đặt (mở thư mục, xuất/nhập sao lưu, đặt lại vị trí...).
/// </summary>
public sealed class SettingsHost : ISettingsHost
{
    private readonly AppServices _services;

    public SettingsHost(AppServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
    }

    public async Task ResetWidgetPositionsAsync()
    {
        await _services.Widgets.ResetPositionsAsync();
    }

    public void OpenDataFolder()
    {
        var folder = Path.GetDirectoryName(_services.Database.DatabasePath);

        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        Directory.CreateDirectory(folder);

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            ArgumentList = { folder },
            UseShellExecute = true
        });
    }

    public async Task ExportJsonAsync()
    {
        var path = AskSavePath("HabitGrid-backup.json", "JSON (*.json)|*.json|All files (*.*)|*.*");

        if (path is null)
        {
            return;
        }

        var json = await _services.Widgets.ExportBackupJsonAsync();

        await File.WriteAllTextAsync(path, json);
    }

    public async Task ExportCsvAsync()
    {
        var habits = await _services.Habits.GetAllActiveAsync();

        if (habits.Count == 0)
        {
            MessageBox.Show("Chưa có thói quen nào để xuất.", "HabitGrid", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Xuất thói quen đầu tiên; giao diện sẽ bổ sung chọn nhiều thói quen ở gói sau.
        var habit = habits[0];

        var path = AskSavePath($"{SanitizeFileName(habit.Name)}.csv", "CSV (*.csv)|*.csv|All files (*.*)|*.*");

        if (path is null)
        {
            return;
        }

        var bytes = await _services.Widgets.ExportBackupCsvAsync(habit.Id);

        await File.WriteAllBytesAsync(path, bytes);
    }

    public async Task ImportJsonAsync()
    {
        var path = AskOpenPath("JSON (*.json)|*.json|All files (*.*)|*.*");

        if (path is null)
        {
            return;
        }

        var json = await File.ReadAllTextAsync(path);

        var mode = ImportMode.Merge;

        var result = MessageBox.Show(
            "Chọn 'Có' để GỘP dữ liệu vào bản hiện tại, 'Không' để THAY THẾ toàn bộ dữ liệu.",
            "Nhập bản sao lưu",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.No)
        {
            mode = ImportMode.Replace;
        }

        try
        {
            await _services.Widgets.ImportBackupJsonAsync(json, mode);

            MessageBox.Show("Đã nhập dữ liệu thành công.", "HabitGrid", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Không thể nhập dữ liệu: {ex.Message}\n\nDữ liệu hiện tại được giữ nguyên.",
                "HabitGrid",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    public void CopyVersionInfo(string versionText)
    {
        var info = $"HabitGrid {versionText} · .NET {Environment.Version} · Windows {Environment.OSVersion.Version}";

        try
        {
            Clipboard.SetText(info);
        }
        catch (Exception)
        {
            // Clipboard có thể bị khoá bởi tiến trình khác -> bỏ qua.
        }
    }

    public async Task ShowPaywallAsync()
    {
        await _services.Dialogs.ShowPaywallAsync(null);
    }

    private static string? AskSavePath(string fileName, string filter)
    {
        var dialog = new SaveFileDialog
        {
            FileName = fileName,
            Filter = filter,
            AddExtension = true
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private static string? AskOpenPath(string filter)
    {
        var dialog = new OpenFileDialog
        {
            Filter = filter
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();

        return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    }
}
