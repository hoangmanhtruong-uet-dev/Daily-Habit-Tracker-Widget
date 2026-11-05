namespace HabitGrid.Platform.System;

public record ToastActionArgs(string Action, Guid HabitId);

/// <summary>
/// Dịch vụ thông báo tương tác nhanh Windows Toast Notifications.
/// </summary>
public interface IToastNotificationService
{
    event EventHandler<ToastActionArgs>? ActionInvoked;

    bool ShowReminderToast(Guid habitId, string habitName, int currentStreak);
}
