using HabitGrid.Core.Domain;
using HabitGrid.Core.Domain.Focus;
using IDayProvider = HabitGrid.Core.Services.IDayProvider;

namespace HabitGrid.Presentation.Services;

/// <summary>
/// Ghi kết quả một phiên Focus (SPEC-V2-09 §2): lưu nhật ký phiên (cho XP +5 và huy hiệu 10 giờ tập trung) và,
/// nếu gắn với thói quen loại Thời lượng, cộng số phút vào nhật ký ngày kết thúc phiên.
/// </summary>
public sealed class FocusService
{
    private readonly IHabitRepository _habits;
    private readonly IDayLogRepository _logs;
    private readonly IFocusRepository _focus;
    private readonly IDayProvider _days;
    private readonly ISettingsStore? _settings;

    public FocusService(IHabitRepository habits, IDayLogRepository logs, IFocusRepository focus, IDayProvider days, ISettingsStore? settings = null)
    {
        _settings = settings;
        _habits = habits ?? throw new ArgumentNullException(nameof(habits));
        _logs = logs ?? throw new ArgumentNullException(nameof(logs));
        _focus = focus ?? throw new ArgumentNullException(nameof(focus));
        _days = days ?? throw new ArgumentNullException(nameof(days));
    }

    /// <summary>Thói quen có thể gắn với Focus: đang theo dõi, không phải loại cần bỏ; Thời lượng xếp trước.</summary>
    public async Task<IReadOnlyList<Habit>> CandidatesAsync(CancellationToken ct = default) =>
        (await _habits.GetAllActiveAsync(ct))
            .Where(h => h.Kind != HabitKind.Quit)
            .OrderBy(h => h.Kind == HabitKind.Duration ? 0 : 1)
            .ThenBy(h => h.SortKey, StringComparer.Ordinal)
            .ToList();

    public async Task<FocusRecord?> CompleteAsync(FocusResult result, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.Minutes < 1)
        {
            return null;
        }

        var record = new FocusRecord(Guid.CreateVersion7(), result.HabitId, result.StartedAtUtc, result.EndedAtUtc, result.Minutes, result.Mode);
        await _focus.AddAsync(record, ct);

        if (result.HabitId is { } habitId
            && await _habits.GetByIdAsync(habitId, ct) is { Kind: HabitKind.Duration, DeletedAt: null } habit
            && (_settings is null || !(await ReadOnlyHabits.LoadAsync(_settings, ct)).Contains(habitId)))
        {
            var day = _days.ToLogicalDate(result.EndedAtUtc.ToLocalTime());
            var existing = await _logs.GetAsync(habitId, day, ct);
            var current = existing is { Status: DayStatus.Skipped or DayStatus.Frozen } ? 0 : existing?.Value ?? 0;

            await _logs.UpsertAsync(DayLogRules.Normalize(habit, new DayLog
            {
                HabitId = habitId,
                Day = day,
                Value = current + result.Minutes,
                Note = existing?.Note,
                Mood = existing?.Mood,
                CheckedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }), ct);
        }

        return record;
    }

    public Task<IReadOnlyList<FocusRecord>> HistoryAsync(CancellationToken ct = default) => _focus.GetAllAsync(ct);
}
