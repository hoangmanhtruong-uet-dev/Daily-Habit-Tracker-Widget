using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace HabitGrid.Platform.System;

/// <summary>
/// Hiện thực hóa Windows Toast Notifications có các nút hành động tương tác nhanh.
/// </summary>
public sealed class ToastNotificationService : IToastNotificationService
{
    private const string AppId = "HabitGrid.App";

    public event EventHandler<ToastActionArgs>? ActionInvoked;

    public bool ShowReminderToast(Guid habitId, string habitName, int currentStreak)
    {
        try
        {
            var escapedName = global::System.Security.SecurityElement.Escape(habitName);
            var streakText = currentStreak > 0
                ? $"Chuỗi hiện tại: {currentStreak} ngày liên tiếp. Hãy giữ vững nhé!"
                : "Hôm nay bạn chưa hoàn thành. Hãy bắt đầu ngay!";

            var xml = $"""
<toast scenario="reminder">
  <visual>
    <binding template="ToastGeneric">
      <text>Đến giờ cho thói quen: {escapedName}! 🎯</text>
      <text>{streakText}</text>
    </binding>
  </visual>
  <actions>
    <action content="Đã xong" arguments="action=checkin&amp;habitId={habitId}" activationType="foreground"/>
    <action content="Để sau 1 giờ" arguments="action=snooze&amp;habitId={habitId}" activationType="foreground"/>
  </actions>
</toast>
""";

            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(xml);

            var toast = new ToastNotification(xmlDoc);
            toast.Activated += (_, args) =>
            {
                if (args is ToastActivatedEventArgs toastArgs)
                {
                    ParseAndRaise(toastArgs.Arguments);
                }
            };

            var notifier = ToastNotificationManager.CreateToastNotifier(AppId);
            notifier.Show(toast);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void ParseAndRaise(string arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
        {
            return;
        }

        string? action = null;
        var habitId = Guid.Empty;

        var pairs = arguments.Split('&');
        foreach (var pair in pairs)
        {
            var parts = pair.Split('=');
            if (parts.Length == 2)
            {
                if (parts[0].Equals("action", StringComparison.OrdinalIgnoreCase))
                {
                    action = parts[1];
                }
                else if (parts[0].Equals("habitId", StringComparison.OrdinalIgnoreCase))
                {
                    Guid.TryParse(parts[1], out habitId);
                }
            }
        }

        if (action != null && habitId != Guid.Empty)
        {
            ActionInvoked?.Invoke(this, new ToastActionArgs(action, habitId));
        }
    }
}
