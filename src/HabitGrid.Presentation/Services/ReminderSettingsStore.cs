using System.Globalization;
using HabitGrid.Core.Domain;
using HabitGrid.Core.Domain.Reminders;

namespace HabitGrid.Presentation.Services;

/// <summary>Đọc/ghi cấu hình nhắc nhở và phím tắt toàn cục trong bảng settings (dùng chung cho app chính và WidgetHost).</summary>
public static class ReminderSettingsStore
{
    public const string QuietEnabledKey = "reminders.quiet.enabled";
    public const string QuietStartKey = "reminders.quiet.start";
    public const string QuietEndKey = "reminders.quiet.end";
    public const string RescueKey = "reminders.rescue";
    public const string MorningKey = "reminders.morning";
    public const string MorningAtKey = "reminders.morning.at";
    public const string RemindersEnabledKey = "reminders.enabled";
    public const string QuickHotKeyEnabledKey = "hotkey.quick.enabled";
    public const string FocusHotKeyEnabledKey = "hotkey.focus.enabled";

    /// <summary>Lệnh cho WidgetHost từ app chính (WidgetHost đọc rồi xóa): "focus.start|habitId|phút", "focus.toggle", "focus.stop".</summary>
    public const string HostCommandKey = "host.command";

    public static async Task<ReminderSettings> LoadAsync(ISettingsStore store, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        var defaults = new ReminderSettings();

        return defaults with
        {
            QuietHoursEnabled = await store.GetAsync(QuietEnabledKey, ct) != "0",
            QuietStart = ParseTime(await store.GetAsync(QuietStartKey, ct), defaults.QuietStart),
            QuietEnd = ParseTime(await store.GetAsync(QuietEndKey, ct), defaults.QuietEnd),
            StreakRescue = await store.GetAsync(RescueKey, ct) != "0",
            MorningSummary = await store.GetAsync(MorningKey, ct) == "1",
            MorningSummaryAt = ParseTime(await store.GetAsync(MorningAtKey, ct), defaults.MorningSummaryAt)
        };
    }

    public static async Task SaveAsync(ISettingsStore store, ReminderSettings settings, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(settings);

        await store.SetAsync(QuietEnabledKey, settings.QuietHoursEnabled ? "1" : "0", ct);
        await store.SetAsync(QuietStartKey, Format(settings.QuietStart), ct);
        await store.SetAsync(QuietEndKey, Format(settings.QuietEnd), ct);
        await store.SetAsync(RescueKey, settings.StreakRescue ? "1" : "0", ct);
        await store.SetAsync(MorningKey, settings.MorningSummary ? "1" : "0", ct);
        await store.SetAsync(MorningAtKey, Format(settings.MorningSummaryAt), ct);
    }

    /// <summary>Tắt toàn bộ nhắc nhở (mặc định bật).</summary>
    public static async Task<bool> RemindersEnabledAsync(ISettingsStore store, CancellationToken ct = default) =>
        await store.GetAsync(RemindersEnabledKey, ct) != "0";

    public static string Format(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);

    public static TimeOnly ParseTime(string? value, TimeOnly fallback) =>
        TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time) ? time : fallback;
}
