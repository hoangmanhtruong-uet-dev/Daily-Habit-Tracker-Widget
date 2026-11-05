using Microsoft.Win32;

namespace HabitGrid.Platform.Desktop;

/// <summary>
/// Chạy cùng Windows qua khóa Run của người dùng hiện tại (ứng dụng không đóng gói MSIX nên không dùng StartupTask).
/// Không cần quyền quản trị; người dùng vẫn tắt được trong Task Manager → Startup.
/// </summary>
public sealed class AutoStart
{
    public const string DefaultKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private readonly string _valueName;
    private readonly string _keyPath;

    public AutoStart(string valueName, string keyPath = DefaultKeyPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(valueName);
        ArgumentException.ThrowIfNullOrWhiteSpace(keyPath);
        _valueName = valueName;
        _keyPath = keyPath;
    }

    /// <summary>Lệnh đang được đăng ký, hoặc null nếu chưa bật.</summary>
    public string? CurrentCommand
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(_keyPath);
            return key?.GetValue(_valueName) as string;
        }
    }

    public bool IsEnabled => CurrentCommand is not null;

    /// <summary>Bật (ghi đường dẫn exe, có ngoặc kép) hoặc tắt. Gọi lại nhiều lần an toàn; cập nhật nếu exe đã dời chỗ.</summary>
    public void Apply(bool enabled, string exePath)
    {
        if (enabled)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(exePath);
            var command = Quote(exePath);

            if (CurrentCommand == command)
            {
                return;
            }

            using var key = Registry.CurrentUser.CreateSubKey(_keyPath);
            key.SetValue(_valueName, command, RegistryValueKind.String);
        }
        else if (IsEnabled)
        {
            using var key = Registry.CurrentUser.OpenSubKey(_keyPath, writable: true);
            key?.DeleteValue(_valueName, throwOnMissingValue: false);
        }
    }

    public static string Quote(string path) => "\"" + path.Trim('"') + "\"";
}
