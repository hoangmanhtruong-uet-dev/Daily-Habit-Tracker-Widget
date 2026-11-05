using HabitGrid.Core.Models;

namespace HabitGrid.Core.Domain.Reminders;

public enum ToastKind
{
    Reminder,
    FollowUp,
    StreakRescue,
    MorningSummary
}

/// <summary>Một toast đã lên lịch. <see cref="At"/> là giờ địa phương. Nhiều thói quen cùng giờ được gộp vào một toast.</summary>
public sealed record PlannedToast(string Id, DateTime At, ToastKind Kind, IReadOnlyList<Guid> HabitIds);

/// <summary>Cấu hình nhắc nhở (SPEC-V2-09 §1). Mặc định: giờ yên lặng 23:00–07:00, cứu streak 21:00, tối đa 6 toast/ngày, lên lịch trước 48 giờ.</summary>
public sealed record ReminderSettings
{
    public bool QuietHoursEnabled { get; init; } = true;

    public TimeOnly QuietStart { get; init; } = new(23, 0);

    public TimeOnly QuietEnd { get; init; } = new(7, 0);

    public bool StreakRescue { get; init; } = true;

    public TimeOnly StreakRescueAt { get; init; } = new(21, 0);

    public int StreakRescueMinStreak { get; init; } = 3;

    public bool MorningSummary { get; init; }

    public TimeOnly MorningSummaryAt { get; init; } = new(7, 30);

    public TimeOnly FollowUpLatest { get; init; } = new(22, 30);

    public TimeSpan FollowUpDelay { get; init; } = TimeSpan.FromHours(2);

    public int DailyCap { get; init; } = 6;

    public int HorizonHours { get; init; } = 48;

    public bool IsQuiet(TimeOnly time) =>
        QuietHoursEnabled && (QuietStart <= QuietEnd
            ? time >= QuietStart && time < QuietEnd
            : time >= QuietStart || time < QuietEnd);
}

/// <summary>Giờ nhắc thông minh: phân vị 40% giờ check-in 21 ngày gần nhất trừ 15 phút; cần ≥ 7 mẫu, không thì giờ mặc định theo khung giờ.</summary>
public static class SmartReminder
{
    public const int WindowDays = 21;
    public const int MinSamples = 7;
    public const double Percentile = 0.4;
    public static readonly TimeSpan Lead = TimeSpan.FromMinutes(15);

    public static TimeOnly DefaultFor(TimeOfDay timeOfDay) => timeOfDay switch
    {
        TimeOfDay.Morning => new TimeOnly(8, 0),
        TimeOfDay.Afternoon => new TimeOnly(14, 0),
        TimeOfDay.Evening => new TimeOnly(20, 0),
        _ => new TimeOnly(19, 0)
    };

    /// <summary>Giờ đề xuất cho <paramref name="habit"/> tính tới hết ngày hôm qua của <paramref name="today"/>.</summary>
    public static TimeOnly Suggest(Habit habit, IEnumerable<DayLog> logs, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(habit);
        ArgumentNullException.ThrowIfNull(logs);

        var from = today.AddDays(-WindowDays);
        var minutes = logs
            .Where(l => l.HabitId == habit.Id && l.Day >= from && l.Day < today
                        && l.Status is DayStatus.Done or DayStatus.Partial && l.CheckedAt is not null)
            .Select(l => ToLocal(l.CheckedAt!.Value).TimeOfDay.TotalMinutes)
            .OrderBy(m => m)
            .ToList();

        if (minutes.Count < MinSamples)
        {
            return DefaultFor(habit.TimeOfDay);
        }

        // Phân vị theo hạng gần nhất: phần tử thứ ceil(p·n) (đếm từ 1).
        var rank = (int)Math.Ceiling(Percentile * minutes.Count);
        var chosen = minutes[Math.Clamp(rank - 1, 0, minutes.Count - 1)] - Lead.TotalMinutes;
        chosen = Math.Max(0, chosen);
        return TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(Math.Floor(chosen)));
    }

    private static DateTime ToLocal(DateTime value) => value.Kind == DateTimeKind.Utc ? value.ToLocalTime() : value;
}

/// <summary>
/// Lập lịch toast nhắc thuần (SPEC-V2-09 §1): nhắc cố định/thông minh, nhắc lần 2 (+2 giờ, không quá 22:30), cứu streak 21:00,
/// tổng kết sáng; bỏ thói quen đã xong/bỏ qua/đóng băng, ngày không có lịch; chặn giờ yên lặng; gộp cùng giờ; tối đa N toast/ngày
/// (vượt thì gộp phần dư vào toast cuối). Gọi lại mỗi khi dữ liệu đổi, qua ngày, hay khởi động.
/// </summary>
public static class ReminderPlanner
{
    public static IReadOnlyList<PlannedToast> Plan(
        IReadOnlyList<Habit> habits,
        IReadOnlyList<DayLog> logs,
        DateTime nowLocal,
        ReminderSettings? settings = null,
        IScheduleCalculator? schedule = null,
        IStreakCalculator? streaks = null)
    {
        ArgumentNullException.ThrowIfNull(habits);
        ArgumentNullException.ThrowIfNull(logs);

        settings ??= new ReminderSettings();
        schedule ??= new ScheduleCalculator();
        streaks ??= new StreakCalculator(DayOfWeek.Monday, schedule);

        var today = DateOnly.FromDateTime(nowLocal);
        var horizon = nowLocal.AddHours(settings.HorizonHours);
        var active = habits.Where(h => h.IsActive && h.Kind != HabitKind.Quit).ToList();
        var logIndex = logs.GroupBy(l => (l.HabitId, l.Day)).ToDictionary(g => g.Key, g => g.OrderByDescending(l => l.UpdatedAt).First());

        bool Settled(Habit h, DateOnly day) =>
            logIndex.TryGetValue((h.Id, day), out var log) && log.Status is DayStatus.Done or DayStatus.Skipped or DayStatus.Frozen;

        bool Due(Habit h, DateOnly day) => day >= h.StartDate && schedule.IsScheduled(h, day) && !Settled(h, day);

        var raw = new List<(DateTime At, ToastKind Kind, Guid HabitId)>();

        for (var day = today; day.ToDateTime(TimeOnly.MinValue) <= horizon; day = day.AddDays(1))
        {
            foreach (var habit in active.Where(h => h.Reminders.Count > 0 && Due(h, day)))
            {
                var smartTime = (TimeOnly?)null;

                foreach (var reminder in habit.Reminders.Where(r => r.Days.HasFlag(day.DayOfWeek.ToMask())))
                {
                    var time = reminder.Smart ? smartTime ??= SmartReminder.Suggest(habit, logs, today) : reminder.Time;
                    var at = day.ToDateTime(time);
                    raw.Add((at, ToastKind.Reminder, habit.Id));

                    if (reminder.FollowUp)
                    {
                        var latest = day.ToDateTime(settings.FollowUpLatest);
                        var follow = at + settings.FollowUpDelay;

                        if (follow > latest && at < latest)
                        {
                            follow = latest;
                        }

                        if (follow <= latest)
                        {
                            raw.Add((follow, ToastKind.FollowUp, habit.Id));
                        }
                    }
                }
            }

            if (settings.MorningSummary)
            {
                foreach (var habit in active.Where(h => Due(h, day)))
                {
                    raw.Add((day.ToDateTime(settings.MorningSummaryAt), ToastKind.MorningSummary, habit.Id));
                }
            }
        }

        // Cứu streak chỉ lập cho hôm nay: ngày mai chưa biết chuỗi còn hay không (lập lại khi qua ngày).
        if (settings.StreakRescue)
        {
            foreach (var habit in active.Where(h => Due(h, today)))
            {
                var mine = logs.Where(l => l.HabitId == habit.Id).ToList();

                if (streaks.Calculate(habit, mine, today).Current >= settings.StreakRescueMinStreak)
                {
                    raw.Add((today.ToDateTime(settings.StreakRescueAt), ToastKind.StreakRescue, habit.Id));
                }
            }
        }

        var grouped = raw
            .Where(r => r.At > nowLocal && r.At <= horizon && !settings.IsQuiet(TimeOnly.FromDateTime(r.At)))
            .GroupBy(r => (At: Truncate(r.At), r.Kind))
            .Select(g => (g.Key.At, g.Key.Kind, Ids: g.Select(x => x.HabitId).Distinct().ToList()))
            .OrderBy(t => t.At)
            .ThenBy(t => t.Kind)
            .ToList();

        var result = new List<PlannedToast>();

        foreach (var day in grouped.GroupBy(t => DateOnly.FromDateTime(t.At)))
        {
            var items = day.ToList();

            if (items.Count > settings.DailyCap)
            {
                // Vượt trần: giữ (cap − 1) toast đầu, gộp phần còn lại vào toast thứ cap.
                var keep = items.Take(settings.DailyCap - 1).ToList();
                var rest = items.Skip(settings.DailyCap - 1).ToList();
                keep.Add((rest[0].At, ToastKind.Reminder, rest.SelectMany(r => r.Ids).Distinct().ToList()));
                items = keep;
            }

            result.AddRange(items.Select(t => new PlannedToast(MakeId(t.Kind, t.At), t.At, t.Kind, t.Ids)));
        }

        return result;
    }

    private static DateTime Truncate(DateTime value) => new(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0, value.Kind);

    /// <summary>Id ngắn (≤ 16 ký tự, giới hạn Tag của toast trên Windows cũ), xác định theo loại + thời điểm.</summary>
    private static string MakeId(ToastKind kind, DateTime at) =>
        (kind switch { ToastKind.FollowUp => "f", ToastKind.StreakRescue => "s", ToastKind.MorningSummary => "m", _ => "r" })
        + at.ToString("yyMMddHHmm", System.Globalization.CultureInfo.InvariantCulture);
}
