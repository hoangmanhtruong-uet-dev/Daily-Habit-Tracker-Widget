using System.Text.Json;
using HabitGrid.Core.Domain;
using HabitGrid.Core.Domain.Challenges;
using IDayProvider = HabitGrid.Core.Services.IDayProvider;

namespace HabitGrid.Presentation.Services;

public enum ChallengeError
{
    None,
    UnknownTemplate,
    InvalidDays,
    ActiveLimit,
    HabitLimit,
    NotFound
}

public sealed record ChallengeStartResult(Challenge? Challenge, ChallengeError Error)
{
    public bool Succeeded => Error == ChallengeError.None;
}

/// <param name="Items">Thử thách chưa đóng (đang chạy, vừa xong, hoặc vừa lỡ) để hiện trên trang Hôm nay.</param>
/// <param name="NewlyCompleted">Những thử thách vừa được ghi nhận hoàn thành ở lần nạp này (để chúc mừng một lần).</param>
public sealed record ChallengeBoard(IReadOnlyList<ChallengeProgress> Items, IReadOnlyList<ChallengeProgress> NewlyCompleted);

/// <summary>
/// Thử thách 30 ngày (F2): tạo thói quen kèm mốc ngày kết thúc, theo dõi tiến độ liền mạch, ghi nhận hoàn thành thành "huy hiệu".
/// Danh sách thử thách lưu trong settings (JSON) nên cục bộ; thói quen bên dưới vẫn là thói quen thường (đồng bộ như bình thường).
/// Free: 1 thử thách đang chạy; Plus: 5.
/// </summary>
public sealed class ChallengeService
{
    public const string Key = "challenges";
    public const int FreeMaxActive = 1;
    public const int PlusMaxActive = 5;

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    private readonly ISettingsStore _settings;
    private readonly IHabitRepository _habits;
    private readonly IDayLogRepository _logs;
    private readonly IEntitlements _entitlements;
    private readonly IDayProvider _days;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public ChallengeService(ISettingsStore settings, IHabitRepository habits, IDayLogRepository logs, IEntitlements entitlements, IDayProvider days)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _habits = habits ?? throw new ArgumentNullException(nameof(habits));
        _logs = logs ?? throw new ArgumentNullException(nameof(logs));
        _entitlements = entitlements ?? throw new ArgumentNullException(nameof(entitlements));
        _days = days ?? throw new ArgumentNullException(nameof(days));
    }

    public int MaxActive => _entitlements.IsPlus ? PlusMaxActive : FreeMaxActive;

    /// <summary>Tạo thói quen theo thử thách mẫu rồi bắt đầu thử thách từ hôm nay.</summary>
    public async Task<ChallengeStartResult> StartAsync(string templateId, string title, int days, CancellationToken ct = default)
    {
        if (ChallengeCatalog.Find(templateId) is not { } template)
        {
            return new ChallengeStartResult(null, ChallengeError.UnknownTemplate);
        }

        if (days < ChallengeCatalog.MinDays || days > ChallengeCatalog.MaxDays || string.IsNullOrWhiteSpace(title))
        {
            return new ChallengeStartResult(null, ChallengeError.InvalidDays);
        }

        await _gate.WaitAsync(ct);

        try
        {
            var today = _days.GetToday();
            var board = await EvaluateAsync(await LoadAsync(ct), today, ct);

            if (board.Count(p => p.State == ChallengeState.Active) >= MaxActive)
            {
                return new ChallengeStartResult(null, ChallengeError.ActiveLimit);
            }

            var all = await _habits.GetAllAsync(ct);

            if (all.Count(h => h.IsActive) >= _entitlements.MaxActiveHabits)
            {
                return new ChallengeStartResult(null, ChallengeError.HabitLimit);
            }

            var habit = new Habit
            {
                Name = title.Trim(),
                Icon = new HabitIcon(template.Emoji),
                Kind = template.Kind,
                Target = new HabitTarget(template.Target),
                TimeOfDay = template.TimeOfDay,
                StartDate = today,
                SortKey = FractionalIndex.After(all.Select(h => h.SortKey).OrderBy(k => k, StringComparer.Ordinal).LastOrDefault())
            };

            if (HabitValidator.Validate(habit).Count > 0)
            {
                return new ChallengeStartResult(null, ChallengeError.InvalidDays);
            }

            await _habits.InsertAsync(habit, ct);
            var challenge = new Challenge(Guid.CreateVersion7(), habit.Id, habit.Name, template.Id, today, days);
            var list = (await LoadAsync(ct)).Append(challenge).ToList();
            await SaveAsync(list, ct);
            return new ChallengeStartResult(challenge, ChallengeError.None);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Nạp tiến độ; thử thách vừa hoàn thành được ghi ngày hoàn thành đúng một lần.</summary>
    public async Task<ChallengeBoard> RefreshAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);

        try
        {
            var today = _days.GetToday();
            var list = await LoadAsync(ct);
            var progress = await EvaluateAsync(list, today, ct);
            var newly = new List<ChallengeProgress>();
            var changed = false;

            for (var i = 0; i < list.Count; i++)
            {
                var p = progress.FirstOrDefault(x => x.Challenge.Id == list[i].Id);

                if (p is { State: ChallengeState.Completed } && list[i].CompletedOn is null)
                {
                    list[i] = list[i] with { CompletedOn = today };
                    newly.Add(p with { Challenge = list[i] });
                    changed = true;
                }
            }

            if (changed)
            {
                await SaveAsync(list, ct);
            }

            var open = progress
                .Where(p => p.Challenge.DismissedOn is null)
                .Select(p => list.First(c => c.Id == p.Challenge.Id) is { } fresh ? p with { Challenge = fresh } : p)
                .ToList();

            return new ChallengeBoard(open, newly);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Thử thách đã hoàn thành (huy hiệu), mới nhất trước.</summary>
    public async Task<IReadOnlyList<Challenge>> CompletedAsync(CancellationToken ct = default) =>
        (await LoadAsync(ct)).Where(c => c.CompletedOn is not null).OrderByDescending(c => c.CompletedOn).ToList();

    /// <summary>Làm lại một thử thách đã lỡ: cùng thói quen, đếm lại từ hôm nay.</summary>
    public async Task<ChallengeError> RestartAsync(Guid id, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);

        try
        {
            var list = await LoadAsync(ct);
            var index = list.FindIndex(c => c.Id == id);

            if (index < 0)
            {
                return ChallengeError.NotFound;
            }

            var today = _days.GetToday();
            var others = list.Where(c => c.Id != id).ToList();

            if ((await EvaluateAsync(others, today, ct)).Count(p => p.State == ChallengeState.Active && p.Challenge.DismissedOn is null) >= MaxActive)
            {
                return ChallengeError.ActiveLimit;
            }

            list[index] = list[index] with { Start = today, CompletedOn = null, DismissedOn = null };
            await SaveAsync(list, ct);
            return ChallengeError.None;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Ẩn khỏi trang Hôm nay (đã lỡ hoặc đã xong). Huy hiệu hoàn thành vẫn được giữ.</summary>
    public async Task DismissAsync(Guid id, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);

        try
        {
            var list = await LoadAsync(ct);
            var index = list.FindIndex(c => c.Id == id);

            if (index >= 0)
            {
                list[index] = list[index] with { DismissedOn = _days.GetToday() };
                await SaveAsync(list, ct);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<List<ChallengeProgress>> EvaluateAsync(List<Challenge> list, DateOnly today, CancellationToken ct)
    {
        var result = new List<ChallengeProgress>();

        foreach (var challenge in list)
        {
            if (await _habits.GetByIdAsync(challenge.HabitId, ct) is not { IsActive: true } habit)
            {
                // Thói quen đã xóa/lưu trữ: thử thách không còn theo dõi được, coi như đóng (không tính là đang chạy).
                if (challenge.CompletedOn is null)
                {
                    result.Add(new ChallengeProgress(challenge with { DismissedOn = challenge.DismissedOn ?? today }, ChallengeState.Failed, 0, 0, null));
                }

                continue;
            }

            var logs = await _logs.GetRangeAsync(habit.Id, challenge.Start, today < challenge.End ? today : challenge.End, ct);
            var progress = ChallengeEngine.Evaluate(challenge, habit, logs, today);

            // Đã ghi nhận hoàn thành thì giữ nguyên dù sau đó dữ liệu đổi.
            result.Add(challenge.CompletedOn is not null ? progress with { State = ChallengeState.Completed } : progress);
        }

        return result;
    }

    private async Task<List<Challenge>> LoadAsync(CancellationToken ct)
    {
        var text = await _settings.GetAsync(Key, ct);

        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<Challenge>>(text, Json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private Task SaveAsync(List<Challenge> list, CancellationToken ct) =>
        _settings.SetAsync(Key, JsonSerializer.Serialize(list, Json), ct);
}
