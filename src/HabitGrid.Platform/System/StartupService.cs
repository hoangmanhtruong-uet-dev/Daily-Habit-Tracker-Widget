using Microsoft.Win32;

namespace HabitGrid.Platform.System;

/// <summary>
/// Hiện thực hóa tự khởi động cùng Windows:
/// Tự động thích ứng giữa MSIX StartupTask và Registry HKCU Run.
/// </summary>
public sealed class StartupService : IStartupService
{
    private const string TaskId = "HabitGridStartupTask";
    private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppRegistryName = "HabitGrid";

    public async Task<bool> IsAutoStartEnabledAsync()
    {
        if (IsPackagedProcess())
        {
            try
            {
                var task = await Windows.ApplicationModel.StartupTask.GetAsync(TaskId);
                return task.State is Windows.ApplicationModel.StartupTaskState.Enabled
                    or Windows.ApplicationModel.StartupTaskState.EnabledByPolicy;
            }
            catch
            {
                return false;
            }
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, writable: false);
            return key?.GetValue(AppRegistryName) != null;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> SetAutoStartEnabledAsync(bool enable)
    {
        if (IsPackagedProcess())
        {
            try
            {
                var task = await Windows.ApplicationModel.StartupTask.GetAsync(TaskId);
                if (enable)
                {
                    var state = await task.RequestEnableAsync();
                    return state is Windows.ApplicationModel.StartupTaskState.Enabled
                        or Windows.ApplicationModel.StartupTaskState.EnabledByPolicy;
                }
                else
                {
                    task.Disable();
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, writable: true);
            if (key == null) return false;

            if (enable)
            {
                var exePath = Environment.ProcessPath ?? global::System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                {
                    key.SetValue(AppRegistryName, $"\"{exePath}\" /startup");
                    return true;
                }
                return false;
            }
            else
            {
                key.DeleteValue(AppRegistryName, false);
                return true;
            }
        }
        catch
        {
            return false;
        }
    }

    private static bool IsPackagedProcess()
    {
        try
        {
            _ = Windows.ApplicationModel.Package.Current.Id;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
