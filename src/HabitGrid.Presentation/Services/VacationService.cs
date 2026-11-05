using System.Globalization;
using System.Text.Json;
using HabitGrid.Core.Domain;
using HabitGrid.Core.Domain.Vacation;
using IDayProvider = HabitGrid.Core.Services.IDayProvider;

namespace HabitGrid.Presentation.Services;

/// <summary>
/// Tạm nghỉ (F3): đánh dấu trước các ngày nghỉ là "đóng băng" cho mọi thói quen hoặc một số thói quen, để chuỗi không đứt,
/// không nhắc, không tính lỡ. Kỳ đang chạy lưu trong settings (WidgetHost/nhắc nhở không cần biết: chúng đọc nhật ký).
/// </summary>
public sealed class VacationService
{
    public const string Key = "vacation.current";

    private readonly IHabitRepository _habits;
    private readonly IDayLogRepository _logs;
    private readonly ISettingsStore _settings;
    private readonly IDayProvider _days;
    private readonly IEntitlements _entitlements;
    private readonly IScheduleCalculator _schedule = new ScheduleCalculator();

    public VacationService(IHabitRepository habits, IDayLogRepository logs, ISettingsStore settings, IDayProvider days, IEntitlements entitlements)
    {
        _habits = habits ?? throw new ArgumentNullException(nameof(habits));
        _logs = logs ?? throw new ArgumentNullException(nameof(logs));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _days = days ?? throw new ArgumentNullException(nameof(days));
        _entitlements = entitlements ?? throw new ArgumentNullException(nameof(entitlements));
    }

    public int MaxDays => _entitlements.MaxVacationDays;

    /// <summary>Kỳ tạm nghỉ chưa kết thúc (hôm nay ≤ ngày cuối), hoặc null. Kỳ đã qua được dọn khỏi settings.</summary>
    public async Task<VacationInfo?> CurrentAsync(CancellationToken ct = default)
    {
        var info = Parse(await _settings.GetAsync(Key, ct));

        if (info is null)
        {
            return null;
        }

        if (info.To < _days.GetToday())
        {
            await _settings.RemoveAsync(Key, ct);
            return null;
        }

        return info;
    }

    public async Task<(VacationCheck Check, int DaysMarked)> StartAsync(
        DateOnly from,
        DateOnly to,
        IReadOnlyCollection<Guid>? habitIds = null,
        CancellationToken ct = default)
    {
        var today = _days.GetToday();
        var check = VacationRules.Validate(from, to, today, _entitlements.IsPlus, await CurrentAsync(ct));

        if (!check.IsValid)
        {
            return (check, 0);
        }

        var info = new VacationInfo(from, to, habitIds?.ToList() ?? []);
        var habits = (await _habits.GetAllActiveAsync(ct)).ToList();
        var marked = 0;
        var any = false;

        foreach (var habit in habits)
        {
            var logs = await _logs.GetRangeAsync(habit.Id, from, to, ct);
            var days = VacationRules.DaysToMark(habit, info, logs.Select(l => l.Day).ToHashSet(), _schedule);
            any |= info.Covers(habit.Id) && habit.Kind != HabitKind.Quit;

            foreach (var day in days)
            {
                await _logs.UpsertAsync(new DayLog
                {
                    HabitId = habit.Id,
                    Day = day,
                    Status = DayStatus.Frozen,
                    UpdatedAt = DateTime.UtcNow
                }, ct);
                marked++;
            }
        }

        if (!any)
        {
            return (new VacationCheck(VacationError.NothingToPause, check.MaxDays), 0);
        }

        await _settings.SetAsync(Key, JsonSerializer.Serialize(new Stored(from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), info.HabitIds.ToList())), ct);
        return (check, marked);
    }

    /// <summary>Kết thúc sớm: gỡ các ngày nghỉ từ hôm nay trở đi (quá khứ giữ nguyên). Trả về số ngày đã gỡ.</summary>
    public async Task<int> EndAsync(CancellationToken ct = default)
    {
        var info = await CurrentAsync(ct);

        if (info is null)
        {
            return 0;
        }

        var today = _days.GetToday();
        var cleared = 0;

        foreach (var habit in await _habits.GetAllAsync(ct))
        {
            foreach (var log in await _logs.GetRangeAsync(habit.Id, today, info.To, ct))
            {
                if (VacationRules.ShouldClear(log, info, today))
                {
                    await _logs.DeleteAsync(habit.Id, log.Day, ct);
                    cleared++;
                }
            }
        }

        await _settings.RemoveAsync(Key, ct);
        return cleared;
    }

    private static VacationInfo? Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            var stored = JsonSerializer.Deserialize<Stored>(raw);

            if (stored is not null
                && DateOnly.TryParseExact(stored.From, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var from)
                && DateOnly.TryParseExact(stored.To, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var to))
            {
                return new VacationInfo(from, to, stored.Habits ?? []);
            }
        }
        catch (JsonException)
        {
            // Dữ liệu hỏng: coi như không có kỳ nghỉ.
        }

        return null;
    }

    private sealed record Stored(string From, string To, List<Guid>? Habits);
}
