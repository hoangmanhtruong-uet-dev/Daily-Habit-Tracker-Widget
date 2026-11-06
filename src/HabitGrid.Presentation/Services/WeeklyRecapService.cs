using HabitGrid.Core.Domain;
using HabitGrid.Core.Domain.Insights;
using IDayProvider = HabitGrid.Core.Services.IDayProvider;

namespace HabitGrid.Presentation.Services;

/// <summary>Tiến độ "ý định tuần này": thói quen ưu tiên và số lần đã làm so với số ngày đến hạn trong cả tuần.</summary>
public sealed record IntentProgress(Guid HabitId, string Name, string Icon, string ColorHue, int Done, int Due)
{
    public double Fraction => Due == 0 ? 0 : Math.Min(1.0, Done / (double)Due);
}

/// <param name="Recap">Tổng kết tuần trước; null khi chưa tới lúc hiện, đã ẩn, hoặc chưa có dữ liệu.</param>
/// <param name="Intent">Ý định tuần này nếu đã chọn.</param>
/// <param name="Choices">Thói quen có thể chọn làm ý định (hiện khi chưa chọn và đang hiện tổng kết).</param>
public sealed record WeeklyCard(WeeklyRecap? Recap, IntentProgress? Intent, IReadOnlyList<Habit> Choices)
{
    public bool IsEmpty => Recap is null && Intent is null;
}

/// <summary>
/// Tổng kết tuần + ý định tuần (F6). Thẻ tổng kết hiện từ thứ Hai đến thứ Tư cho tuần trước, đến khi người dùng bấm "Đã xem".
/// Ý định tuần chỉ có hiệu lực trong tuần được chọn. Lưu trong settings (cục bộ).
/// </summary>
public sealed class WeeklyRecapService
{
    public const string DismissedKey = "weekly.dismissed";
    public const string IntentKey = "weekly.intent";
    public const int ShowDays = 3;
    public const int MaxChoices = 6;

    private readonly IHabitRepository _habits;
    private readonly IDayLogRepository _logs;
    private readonly ISettingsStore _settings;
    private readonly IDayProvider _days;
    private readonly IScheduleCalculator _schedule = new ScheduleCalculator();

    public WeeklyRecapService(IHabitRepository habits, IDayLogRepository logs, ISettingsStore settings, IDayProvider days)
    {
        _habits = habits ?? throw new ArgumentNullException(nameof(habits));
        _logs = logs ?? throw new ArgumentNullException(nameof(logs));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _days = days ?? throw new ArgumentNullException(nameof(days));
    }

    public async Task<WeeklyCard> GetAsync(CancellationToken ct = default)
    {
        var today = _days.GetToday();
        var week = PeriodCalendar.StartOfWeek(today, DayOfWeek.Monday);
        var habits = (await _habits.GetAllActiveAsync(ct)).Where(h => h.Kind != HabitKind.Quit).ToList();
        var intent = await IntentAsync(habits, week, today, ct);
        WeeklyRecap? recap = null;
        var showWindow = today.DayNumber - week.DayNumber < ShowDays;

        if (showWindow && habits.Count > 0 && await _settings.GetAsync(DismissedKey, ct) != week.ToString("yyyy-MM-dd"))
        {
            var logs = new List<DayLog>();

            foreach (var habit in habits)
            {
                logs.AddRange(await _logs.GetRangeAsync(habit.Id, week.AddDays(-14), today, ct));
            }

            var built = WeeklyRecapBuilder.Build(habits, logs, today, _schedule);
            recap = built.Tip == RecapTipKind.NoData ? null : built;
        }

        var choices = recap is not null && intent is null ? habits.Take(MaxChoices).ToList() : [];
        return new WeeklyCard(recap, intent, choices);
    }

    /// <summary>Chọn (hoặc bỏ với null) thói quen ưu tiên của tuần này. Trả false nếu thói quen không hợp lệ.</summary>
    public async Task<bool> SetIntentAsync(Guid? habitId, CancellationToken ct = default)
    {
        var week = PeriodCalendar.StartOfWeek(_days.GetToday(), DayOfWeek.Monday);

        if (habitId is null)
        {
            await _settings.RemoveAsync(IntentKey, ct);
            return true;
        }

        if (await _habits.GetByIdAsync(habitId.Value, ct) is not { IsActive: true, Kind: not HabitKind.Quit })
        {
            return false;
        }

        await _settings.SetAsync(IntentKey, $"{week:yyyy-MM-dd}|{habitId.Value}", ct);
        return true;
    }

    /// <summary>Ẩn thẻ tổng kết của tuần này (ý định vẫn giữ).</summary>
    public Task DismissAsync(CancellationToken ct = default) =>
        _settings.SetAsync(DismissedKey, PeriodCalendar.StartOfWeek(_days.GetToday(), DayOfWeek.Monday).ToString("yyyy-MM-dd"), ct);

    private async Task<IntentProgress?> IntentAsync(IReadOnlyList<Habit> habits, DateOnly week, DateOnly today, CancellationToken ct)
    {
        var text = await _settings.GetAsync(IntentKey, ct);
        var parts = text?.Split('|');

        if (parts is not { Length: 2 } || parts[0] != week.ToString("yyyy-MM-dd") || !Guid.TryParse(parts[1], out var id))
        {
            return null;
        }

        if (habits.FirstOrDefault(h => h.Id == id) is not { } habit)
        {
            return null;
        }

        var logs = await _logs.GetRangeAsync(habit.Id, week, today, ct);
        var done = InsightsCalculator.Latest(logs).Values.Count(l => l.Status == DayStatus.Done);
        var due = 0;

        for (var day = week; day <= week.AddDays(6); day = day.AddDays(1))
        {
            if (day >= habit.StartDate && _schedule.IsScheduled(habit, day))
            {
                due++;
            }
        }

        // Thói quen "n lần mỗi tuần": mục tiêu là số lần, không phải số ngày có lịch.
        if (habit.Frequency is HabitFrequency.TimesPerWeek weekly)
        {
            due = weekly.Times;
        }

        return new IntentProgress(habit.Id, habit.Name, habit.Icon.Value, habit.ColorHue, done, Math.Max(due, done));
    }
}
