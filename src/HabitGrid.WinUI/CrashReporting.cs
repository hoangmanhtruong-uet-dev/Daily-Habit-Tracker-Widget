using System.Reflection;
using HabitGrid.Core.Domain;
using HabitGrid.Presentation.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HabitGrid.WinUI;

/// <summary>
/// Nối <see cref="CrashReportStore"/> vào app: ghi báo lỗi khi có ngoại lệ không bắt được, và lần mở sau hỏi người dùng
/// có muốn gửi file cho nhà phát triển không (tự gửi, app không tải lên đâu cả).
/// </summary>
public static class CrashReporting
{
    public static CrashReportStore Store { get; } = new(AppPaths.LogDir);

    public static string AppVersion =>
        typeof(CrashReporting).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(CrashReporting).Assembly.GetName().Version?.ToString()
        ?? "unknown";

    /// <summary>Ghi báo lỗi kèm 40 dòng log gần nhất. Không bao giờ ném lỗi.</summary>
    public static void Record(string where, Exception? exception)
    {
        try
        {
            Store.Write(exception, where, AppVersion, RecentLog());
        }
        catch
        {
            // Đang xử lý sự cố: không được gây thêm lỗi.
        }
    }

    /// <summary>Có báo lỗi mới từ lần chạy trước thì hỏi một lần; người dùng xem xong là đánh dấu đã xem.</summary>
    public static async Task PromptIfNeededAsync(XamlRoot root)
    {
        var settings = App.Host.Get<ISettingsStore>();
        var ack = CrashReportStore.ParseAck(await settings.GetAsync(CrashReportStore.AckKey));
        var pending = Store.PendingSince(ack);
        await settings.SetAsync(CrashReportStore.AckKey, CrashReportStore.FormatAck(DateTime.UtcNow));

        if (pending.Count == 0)
        {
            return;
        }

        var latest = pending[0];
        var dialog = new ContentDialog
        {
            XamlRoot = root,
            Title = Ui.T("crash.title"),
            Content = new TextBlock { Text = Ui.Loc.Format("crash.body", pending.Count), TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = Ui.T("crash.open"),
            SecondaryButtonText = Ui.T("crash.copy"),
            CloseButtonText = Ui.T("plus.close"),
            DefaultButton = ContentDialogButton.Primary
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            OpenFolder(latest.Path);
        }
        else if (result == ContentDialogResult.Secondary)
        {
            var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
            package.SetText(await File.ReadAllTextAsync(latest.Path));
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
        }
    }

    public static void OpenFolder(string? selectFile = null)
    {
        Directory.CreateDirectory(AppPaths.LogDir);
        var args = selectFile is null ? $"\"{AppPaths.LogDir}\"" : $"/select,\"{selectFile}\"";
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", args) { UseShellExecute = true });
    }

    private static string? RecentLog()
    {
        try
        {
            var file = new DirectoryInfo(AppPaths.LogDir).EnumerateFiles("habitgrid-*.log").OrderByDescending(f => f.LastWriteTimeUtc).FirstOrDefault();

            if (file is null)
            {
                return null;
            }

            using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            var lines = reader.ReadToEnd().Split('\n');
            return string.Join('\n', lines.TakeLast(40));
        }
        catch
        {
            return null;
        }
    }
}
