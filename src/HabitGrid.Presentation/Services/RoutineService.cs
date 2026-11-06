using HabitGrid.Core.Domain;
using HabitGrid.Core.Domain.Routines;
using IDayProvider = HabitGrid.Core.Services.IDayProvider;

namespace HabitGrid.Presentation.Services;

/// <summary>Bản nháp routine do người dùng nhập. <see cref="HabitIds"/> theo thứ tự các bước.</summary>
public sealed record RoutineDraft(string Name, HabitIcon Icon, TimeOfDay TimeOfDay, IReadOnlyList<Guid> HabitIds);

public sealed record RoutineResult(Routine? Routine, RoutineError Error)
{
    public bool Succeeded => Error == RoutineError.None;
}

/// <summary>Một bước của routine trong một ngày.</summary>
public sealed record RoutineStep(Guid HabitId, string Name, string Icon, string ColorHue, HabitKind Kind, bool IsDone, int Value, int Target);

/// <summary>Tiến độ routine trong một ngày: chỉ tính các bước đến hạn hôm đó (bỏ thói quen không có lịch, chỉ đọc, đang nghỉ).</summary>
public sealed record RoutineProgress(Routine Routine, IReadOnlyList<RoutineStep> Steps)
{
    public int Total => Steps.Count;

    public int Done => Steps.Count(s => s.IsDone);

    public bool IsComplete => Total > 0 && Done == Total;

    public double Fraction => Total == 0 ? 0 : Done / (double)Total;

    public RoutineStep? Next => Steps.FirstOrDefault(s => !s.IsDone);
}

/// <summary>
/// Routine (F1): gom thói quen thành chuỗi bước ("Sáng", "Trước khi ngủ"). Thành viên lưu bằng <c>Habit.RoutineId</c>/<c>RoutineOrder</c>.
/// "Xong cả routine" chỉ đánh dấu những bước chưa xong và đang hoạt động, không đè lên ngày bỏ qua/đóng băng.
/// </summary>
public sealed class RoutineService
{
    private readonly IHabitRepository _habits;
    private readonly IDayLogRepository _logs;
    private readonly IRoutineRepository _routines;
    private readonly IEntitlements _entitlements;
    private readonly IDayProvider _days;
    private readonly ISettingsStore? _settings;
    private readonly IScheduleCalculator _schedule = new ScheduleCalculator();

    public RoutineService(
        IHabitRepository habits,
        IDayLogRepository logs,
        IRoutineRepository routines,
        IEntitlements entitlements,
        IDayProvider days,
        ISettingsStore? settings = null)
    {
        _habits = habits ?? throw new ArgumentNullException(nameof(habits));
        _logs = logs ?? throw new ArgumentNullException(nameof(logs));
        _routines = routines ?? throw new ArgumentNullException(nameof(routines));
        _entitlements = entitlements ?? throw new ArgumentNullException(nameof(entitlements));
        _days = days ?? throw new ArgumentNullException(nameof(days));
        _settings = settings;
    }

    public int MaxRoutines => RoutineRules.MaxRoutines(_entitlements.IsPlus);

    public Task<IReadOnlyList<Routine>> ListAsync(CancellationToken ct = default) => _routines.GetAllAsync(ct);

    public async Task<RoutineResult> CreateAsync(RoutineDraft draft, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var existing = await _routines.GetAllAsync(ct);
        var error = RoutineRules.Validate(draft.Name, draft.HabitIds.Count, existing.Count, _entitlements.IsPlus, isNew: true);

        if (error != RoutineError.None)
        {
            return new RoutineResult(null, error);
        }

        if (await CheckHabitsAsync(draft.HabitIds, null, ct) is { } habitError)
        {
            return new RoutineResult(null, habitError);
        }

        var routine = new Routine(
            Guid.CreateVersion7(),
            draft.Name.Trim(),
            draft.Icon,
            draft.TimeOfDay,
            FractionalIndex.Between(existing.Select(r => r.SortKey).DefaultIfEmpty().Max(StringComparer.Ordinal), null),
            draft.HabitIds.ToList());

        await _routines.UpsertAsync(routine, ct);
        await AssignAsync(routine.Id, draft.HabitIds, ct);
        return new RoutineResult(routine, RoutineError.None);
    }

    public async Task<RoutineResult> UpdateAsync(Guid id, RoutineDraft draft, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var existing = await _routines.GetAllAsync(ct);

        if (existing.FirstOrDefault(r => r.Id == id) is not { } current)
        {
            return new RoutineResult(null, RoutineError.NotFound);
        }

        var error = RoutineRules.Validate(draft.Name, draft.HabitIds.Count, existing.Count - 1, _entitlements.IsPlus, isNew: false);

        if (error != RoutineError.None)
        {
            return new RoutineResult(null, error);
        }

        if (await CheckHabitsAsync(draft.HabitIds, id, ct) is { } habitError)
        {
            return new RoutineResult(null, habitError);
        }

        var updated = current with { Name = draft.Name.Trim(), Icon = draft.Icon, TimeOfDay = draft.TimeOfDay, HabitIds = draft.HabitIds.ToList() };
        await _routines.UpsertAsync(updated, ct);

        // Gỡ những thói quen không còn trong routine rồi gán lại theo thứ tự mới.
        foreach (var removed in current.HabitIds.Where(h => !draft.HabitIds.Contains(h)))
        {
            if (await _habits.GetByIdAsync(removed, ct) is { } habit && habit.RoutineId == id)
            {
                habit.RoutineId = null;
                habit.RoutineOrder = 0;
                await _habits.UpdateAsync(habit, ct);
            }
        }

        await AssignAsync(id, draft.HabitIds, ct);
        return new RoutineResult(updated, RoutineError.None);
    }

    /// <summary>Xóa routine: thói quen bên trong được tách ra, không bị xóa.</summary>
    public Task DeleteAsync(Guid id, CancellationToken ct = default) => _routines.DeleteAsync(id, ct);

    /// <summary>Thói quen có thể chọn khi tạo/sửa routine: đang hoạt động, hợp lệ, chưa thuộc routine khác (trừ routine đang sửa).</summary>
    public async Task<IReadOnlyList<Habit>> AvailableHabitsAsync(Guid? editing = null, CancellationToken ct = default) =>
        (await _habits.GetAllActiveAsync(ct))
            .Where(h => RoutineRules.CanJoin(h) && (h.RoutineId is null || h.RoutineId == editing))
            .OrderBy(h => h.SortKey, StringComparer.Ordinal)
            .ToList();

    /// <summary>Tiến độ mọi routine trong ngày <paramref name="day"/> (mặc định hôm nay). Routine không còn bước nào đến hạn thì bỏ qua.</summary>
    public async Task<IReadOnlyList<RoutineProgress>> ProgressAsync(DateOnly? day = null, CancellationToken ct = default)
    {
        var date = day ?? _days.GetToday();
        var routines = await _routines.GetAllAsync(ct);

        if (routines.Count == 0)
        {
            return [];
        }

        var habits = (await _habits.GetAllActiveAsync(ct)).ToDictionary(h => h.Id);
        var logs = (await _logs.GetForDayAsync(date, ct)).GroupBy(l => l.HabitId).ToDictionary(g => g.Key, g => g.OrderByDescending(l => l.UpdatedAt).First());
        var readOnly = _settings is null ? new HashSet<Guid>() : await ReadOnlyHabits.LoadAsync(_settings, ct);
        var result = new List<RoutineProgress>();

        foreach (var routine in routines)
        {
            var steps = new List<RoutineStep>();

            foreach (var id in routine.HabitIds)
            {
                if (!habits.TryGetValue(id, out var habit) || !RoutineRules.CanJoin(habit) || readOnly.Contains(id)
                    || date < habit.StartDate || !_schedule.IsScheduled(habit, date))
                {
                    continue;
                }

                logs.TryGetValue(id, out var log);

                // Ngày đóng băng/bỏ qua (tạm nghỉ, freeze): không phải bước cần làm.
                if (log is { Status: DayStatus.Skipped or DayStatus.Frozen })
                {
                    continue;
                }

                steps.Add(new RoutineStep(id, habit.Name, habit.Icon.Value, habit.ColorHue, habit.Kind, log?.Status == DayStatus.Done, log?.Value ?? 0, habit.EffectiveTarget));
            }

            if (steps.Count > 0)
            {
                result.Add(new RoutineProgress(routine, steps));
            }
        }

        return result;
    }

    /// <summary>"Xong cả routine": đánh dấu mọi bước chưa xong của routine. Trả về số bước vừa hoàn thành.</summary>
    public async Task<int> CompleteAllAsync(Guid routineId, DateOnly? day = null, CancellationToken ct = default)
    {
        var date = day ?? _days.GetToday();
        var progress = (await ProgressAsync(date, ct)).FirstOrDefault(p => p.Routine.Id == routineId);

        if (progress is null)
        {
            return 0;
        }

        var done = 0;

        foreach (var step in progress.Steps.Where(s => !s.IsDone))
        {
            if (await _habits.GetByIdAsync(step.HabitId, ct) is not { } habit)
            {
                continue;
            }

            var existing = await _logs.GetAsync(habit.Id, date, ct);
            await _logs.UpsertAsync(DayLogRules.Normalize(habit, new DayLog
            {
                HabitId = habit.Id,
                Day = date,
                Value = Math.Max(habit.EffectiveTarget, existing?.Value ?? 0),
                Note = existing?.Note,
                Mood = existing?.Mood,
                CheckedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }), ct);
            done++;
        }

        return done;
    }

    private async Task<RoutineError?> CheckHabitsAsync(IReadOnlyList<Guid> ids, Guid? routineId, CancellationToken ct)
    {
        if (ids.Distinct().Count() != ids.Count)
        {
            return RoutineError.UnknownHabit;
        }

        foreach (var id in ids)
        {
            if (await _habits.GetByIdAsync(id, ct) is not { } habit
                || !RoutineRules.CanJoin(habit)
                || (habit.RoutineId is { } other && other != routineId))
            {
                return RoutineError.UnknownHabit;
            }
        }

        return null;
    }

    private async Task AssignAsync(Guid routineId, IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        for (var i = 0; i < ids.Count; i++)
        {
            if (await _habits.GetByIdAsync(ids[i], ct) is { } habit && (habit.RoutineId != routineId || habit.RoutineOrder != i))
            {
                habit.RoutineId = routineId;
                habit.RoutineOrder = i;
                await _habits.UpdateAsync(habit, ct);
            }
        }
    }
}
