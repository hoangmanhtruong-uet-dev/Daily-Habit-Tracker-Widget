using HabitGrid.Core.Domain;

namespace HabitGrid.Presentation.Services;

/// <summary>
/// Check-in nhanh một bấm (widget, tray, quick check-in): có/không bật-tắt; đếm +1 rồi về 0 khi đã đủ;
/// thời lượng bấm là đủ mục tiêu, bấm lại là xóa. Thói quen cần bỏ không check qua đây.
/// </summary>
public sealed class CheckInService
{
    private readonly IHabitRepository _habits;
    private readonly IDayLogRepository _logs;
    private readonly ISettingsStore? _settings;

    public CheckInService(IHabitRepository habits, IDayLogRepository logs, ISettingsStore? settings = null)
    {
        _settings = settings;
        _habits = habits ?? throw new ArgumentNullException(nameof(habits));
        _logs = logs ?? throw new ArgumentNullException(nameof(logs));
    }

    /// <summary>Trả về bản ghi mới của ngày, hoặc null nếu đã xóa/không áp dụng.</summary>
    public async Task<DayLog?> ToggleAsync(Guid habitId, DateOnly day, CancellationToken ct = default)
    {
        var habit = await _habits.GetByIdAsync(habitId, ct);

        if (habit is null || habit.DeletedAt is not null || habit.Kind == HabitKind.Quit || day < habit.StartDate)
        {
            return null;
        }

        // Thói quen chỉ đọc sau khi hạ cấp: không ghi gì (dữ liệu cũ giữ nguyên).
        if (_settings is not null && (await ReadOnlyHabits.LoadAsync(_settings, ct)).Contains(habitId))
        {
            return await _logs.GetAsync(habitId, day, ct);
        }

        var existing = await _logs.GetAsync(habitId, day, ct);
        var current = existing is { Status: DayStatus.Skipped or DayStatus.Frozen } ? 0 : existing?.Value ?? 0;
        var target = habit.EffectiveTarget;

        var next = habit.Kind switch
        {
            HabitKind.Boolean => current > 0 ? 0 : 1,
            HabitKind.Duration => current >= target ? 0 : target,
            _ => current >= target ? 0 : current + 1
        };

        if (next == 0)
        {
            await _logs.DeleteAsync(habitId, day, ct);
            return null;
        }

        var log = DayLogRules.Normalize(habit, new DayLog
        {
            HabitId = habitId,
            Day = day,
            Value = next,
            Note = existing?.Note,
            Mood = existing?.Mood,
            CheckedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await _logs.UpsertAsync(log, ct);
        return log;
    }
}
