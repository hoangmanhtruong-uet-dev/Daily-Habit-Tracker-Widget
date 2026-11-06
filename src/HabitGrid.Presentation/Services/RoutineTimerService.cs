using System.Text.Json;
using HabitGrid.Core.Domain;
using HabitGrid.Core.Domain.Routines;

namespace HabitGrid.Presentation.Services;

/// <summary>Một bước của routine kèm thời lượng đã đặt và trạng thái hôm nay (để màn hình hẹn giờ liệt kê).</summary>
public sealed record TimedStepPlan(Guid HabitId, string Name, string Icon, int Minutes, bool IsDone);

/// <summary>
/// Hẹn giờ từng bước của routine (P4, Plus): lưu thời lượng mỗi bước (settings, JSON) và dựng bộ chạy cho những bước hôm nay chưa xong.
/// Free không dùng được; thời lượng đã đặt được giữ khi hết Plus.
/// </summary>
public sealed class RoutineTimerService
{
    public const string Key = "routine.durations";

    private readonly RoutineService _routines;
    private readonly ISettingsStore _settings;
    private readonly IEntitlements _entitlements;

    public RoutineTimerService(RoutineService routines, ISettingsStore settings, IEntitlements entitlements)
    {
        _routines = routines ?? throw new ArgumentNullException(nameof(routines));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _entitlements = entitlements ?? throw new ArgumentNullException(nameof(entitlements));
    }

    public bool IsAvailable => _entitlements.IsPlus;

    public static int Clamp(int minutes) => Math.Clamp(minutes, RoutineRunner.MinMinutes, RoutineRunner.MaxMinutes);

    /// <summary>Các bước của routine hôm nay kèm thời lượng (mặc định 5 phút). Rỗng nếu routine không còn bước nào đến hạn.</summary>
    public async Task<IReadOnlyList<TimedStepPlan>> PlanAsync(Guid routineId, DateOnly? day = null, CancellationToken ct = default)
    {
        var progress = (await _routines.ProgressAsync(day, ct)).FirstOrDefault(p => p.Routine.Id == routineId);

        if (progress is null)
        {
            return [];
        }

        var saved = await LoadAsync(ct);
        return progress.Steps
            .Select(s => new TimedStepPlan(s.HabitId, s.Name, s.Icon, saved.GetValueOrDefault(s.HabitId, RoutineRunner.DefaultMinutes), s.IsDone))
            .ToList();
    }

    public async Task SetDurationAsync(Guid habitId, int minutes, CancellationToken ct = default)
    {
        var saved = await LoadAsync(ct);
        saved[habitId] = Clamp(minutes);
        await _settings.SetAsync(Key, JsonSerializer.Serialize(saved), ct);
    }

    /// <summary>Bộ chạy cho các bước chưa xong, theo đúng thứ tự routine. Null nếu không phải Plus hoặc không còn bước nào cần làm.</summary>
    public async Task<RoutineRunner?> BuildRunnerAsync(Guid routineId, DateOnly? day = null, CancellationToken ct = default)
    {
        if (!IsAvailable)
        {
            return null;
        }

        var steps = (await PlanAsync(routineId, day, ct))
            .Where(s => !s.IsDone)
            .Select(s => new TimedStep(s.HabitId, s.Name, Clamp(s.Minutes)))
            .ToList();
        return steps.Count == 0 ? null : new RoutineRunner(steps);
    }

    private async Task<Dictionary<Guid, int>> LoadAsync(CancellationToken ct)
    {
        var text = await _settings.GetAsync(Key, ct);

        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<Guid, int>>(text) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
