namespace HabitGrid.Platform.System;

/// <summary>
/// Quản lý quyền và trạng thái tự khởi động cùng Windows.
/// </summary>
public interface IStartupService
{
    Task<bool> IsAutoStartEnabledAsync();
    Task<bool> SetAutoStartEnabledAsync(bool enable);
}
