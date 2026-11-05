using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HabitGrid.Core.Domain;
using IDayProvider = HabitGrid.Core.Services.IDayProvider;
using HabitGrid.Presentation.Services;

namespace HabitGrid.Presentation.ViewModels;

/// <summary>Dữ liệu người dùng nhập khi tạo thói quen (bản rút gọn của trình tạo đầy đủ).</summary>
public sealed record HabitDraft(
    string Name,
    HabitIcon Icon,
    HabitKind Kind = HabitKind.Boolean,
    int Target = 1,
    string? Unit = null,
    TimeOfDay TimeOfDay = TimeOfDay.Anytime,
    HabitFrequency? Frequency = null,
    string? ColorHue = null,
    DateOnly? StartDate = null,
    IReadOnlyList<Reminder>? Reminders = null);

public sealed record CreateHabitResult(Habit? Habit, IReadOnlyList<ValidationError> Errors)
{
    public bool Succeeded => Habit is not null;
}

/// <summary>Tạo thói quen: kiểm tra bằng <see cref="HabitValidator"/>, gán màu, ngày bắt đầu và khóa sắp xếp.</summary>
public sealed class HabitCreationService
{
    /// <summary>12 màu preset của design system (01-DESIGN-SYSTEM §2.2).</summary>
    public static readonly IReadOnlyList<string> PresetColors =
    [
        "#8B5CF6", "#3B82F6", "#10B981", "#84CC16", "#FACC15", "#FB923C",
        "#F43F5E", "#EC4899", "#06B6D4", "#A78BFA", "#A16207", "#64748B"
    ];

    private readonly IHabitRepository _habits;
    private readonly IDayProvider _dayProvider;

    private readonly IEntitlements _entitlements;

    public HabitCreationService(IHabitRepository habits, IDayProvider dayProvider, IEntitlements? entitlements = null)
    {
        _entitlements = entitlements ?? new UnlimitedEntitlements();
        _habits = habits ?? throw new ArgumentNullException(nameof(habits));
        _dayProvider = dayProvider ?? throw new ArgumentNullException(nameof(dayProvider));
    }

    public async Task<CreateHabitResult> CreateAsync(HabitDraft draft, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var existing = await _habits.GetAllAsync(ct);

        if (existing.Count(h => h.IsActive) >= _entitlements.MaxActiveHabits)
        {
            return new CreateHabitResult(null, [new ValidationError("limit.habits", nameof(Habit))]);
        }

        var last = existing.Select(h => h.SortKey).OrderBy(k => k, StringComparer.Ordinal).LastOrDefault();

        var habit = new Habit
        {
            Name = draft.Name.Trim(),
            Icon = draft.Icon,
            Kind = draft.Kind,
            Target = new HabitTarget(
                draft.Kind is HabitKind.Boolean or HabitKind.Quit ? 1 : draft.Target,
                string.IsNullOrWhiteSpace(draft.Unit) ? null : draft.Unit.Trim()),
            Frequency = draft.Frequency ?? new HabitFrequency.Daily(),
            TimeOfDay = draft.TimeOfDay,
            ColorHue = draft.ColorHue ?? PresetColors[existing.Count % PresetColors.Count],
            StartDate = draft.StartDate ?? _dayProvider.GetToday(),
            Reminders = draft.Reminders?.ToList() ?? [],
            SortKey = FractionalIndex.After(last)
        };

        var errors = HabitValidator.Validate(habit);
        if (errors.Count > 0)
        {
            return new CreateHabitResult(null, errors);
        }

        await _habits.InsertAsync(habit, ct);
        return new CreateHabitResult(habit, []);
    }
}

/// <summary>Dịch mã lỗi của Core thành câu cho người dùng.</summary>
public static class ValidationText
{
    public static string For(ILocalizer localizer, ValidationError error)
    {
        var key = $"validation.{error.Code}";
        var text = localizer[key];
        return text.StartsWith('[') ? localizer["validation.generic"] : text;
    }
}

public sealed partial class HabitListItem : ObservableObject
{
    public HabitListItem(Habit habit)
    {
        Habit = habit;
    }

    public Habit Habit { get; }

    public Guid Id => Habit.Id;

    public string Name => Habit.Name;

    public string Icon => Habit.Icon.Value;

    public string ColorHue => Habit.ColorHue;

    [ObservableProperty]
    private int _streak;

    [ObservableProperty]
    private StreakUnit _streakUnit;

    /// <summary>Tỷ lệ 30 ngày, 0–100; null khi chưa có dữ liệu.</summary>
    [ObservableProperty]
    private int? _rate30;
}

/// <summary>Trang "Thói quen": danh sách, lưu trữ, xóa có hoàn tác, đổi vị trí.</summary>
public sealed partial class HabitsViewModel : ObservableObject
{
    private readonly IHabitRepository _habits;
    private readonly IDayLogRepository _logs;
    private readonly IDayProvider _dayProvider;
    private readonly IStreakCalculator _streaks;
    private readonly IStatsCalculator _stats;
    private readonly IUiDispatcher _dispatcher;
    private readonly IAppLogger _logger;

    public HabitsViewModel(
        IHabitRepository habits,
        IDayLogRepository logs,
        IDayProvider dayProvider,
        IStreakCalculator? streaks = null,
        IStatsCalculator? stats = null,
        IUiDispatcher? dispatcher = null,
        IAppLogger? logger = null)
    {
        _habits = habits ?? throw new ArgumentNullException(nameof(habits));
        _logs = logs ?? throw new ArgumentNullException(nameof(logs));
        _dayProvider = dayProvider ?? throw new ArgumentNullException(nameof(dayProvider));
        _streaks = streaks ?? new StreakCalculator();
        _stats = stats ?? new StatsCalculator();
        _dispatcher = dispatcher ?? new ImmediateDispatcher();
        _logger = logger ?? NullLogger.Instance;
    }

    public ObservableCollection<HabitListItem> Items { get; } = [];

    [ObservableProperty]
    private bool _isEmpty = true;

    /// <summary>Thói quen vừa xóa, cho phép hoàn tác trong thời gian ngắn.</summary>
    [ObservableProperty]
    private Guid? _lastDeletedId;

    [ObservableProperty]
    private string? _errorMessage;

    [RelayCommand]
    public async Task LoadAsync()
    {
        try
        {
            ErrorMessage = null;
            var today = _dayProvider.GetToday();
            var habits = await _habits.GetAllActiveAsync();
            var items = new List<HabitListItem>();

            foreach (var habit in habits)
            {
                var logs = await _logs.GetAllForHabitAsync(habit.Id);
                var streak = _streaks.Calculate(habit, logs, today);
                var rate = _stats.CompletionRate(habit, logs, today.AddDays(-29), today);

                items.Add(new HabitListItem(habit)
                {
                    Streak = streak.Current,
                    StreakUnit = streak.Unit,
                    Rate30 = rate is { } r ? (int)Math.Round(r * 100) : null
                });
            }

            _dispatcher.Post(() =>
            {
                Items.Clear();
                foreach (var item in items)
                {
                    Items.Add(item);
                }

                IsEmpty = Items.Count == 0;
            });
        }
        catch (Exception ex)
        {
            _logger.Error("Tải danh sách thói quen thất bại", ex);
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    public async Task ArchiveAsync(HabitListItem? item)
    {
        if (item is null)
        {
            return;
        }

        await _habits.ArchiveAsync(item.Id);
        await LoadAsync();
    }

    [RelayCommand]
    public async Task DeleteAsync(HabitListItem? item)
    {
        if (item is null)
        {
            return;
        }

        await _habits.SoftDeleteAsync(item.Id);
        LastDeletedId = item.Id;
        await LoadAsync();
    }

    [RelayCommand]
    public async Task UndoDeleteAsync()
    {
        if (LastDeletedId is not { } id)
        {
            return;
        }

        await _habits.RestoreAsync(id);
        LastDeletedId = null;
        await LoadAsync();
    }

    /// <summary>Đặt <paramref name="item"/> vào vị trí <paramref name="newIndex"/>. Chỉ ghi một dòng nhờ fractional index.</summary>
    public async Task MoveAsync(HabitListItem item, int newIndex)
    {
        ArgumentNullException.ThrowIfNull(item);

        var ordered = Items.Where(i => i.Id != item.Id).ToList();
        newIndex = Math.Clamp(newIndex, 0, ordered.Count);

        // Dữ liệu có khóa trùng/đảo thứ tự ở bất kỳ đâu: đánh lại toàn bộ khóa một lần cho chắc.
        var healthy = ordered
            .Select(i => i.Habit.SortKey)
            .Zip(ordered.Select(i => i.Habit.SortKey).Skip(1), (a, b) => string.CompareOrdinal(a, b) < 0)
            .All(ok => ok);

        if (!healthy)
        {
            ordered.Insert(newIndex, item);
            var keys = FractionalIndex.Generate(null, null, ordered.Count);

            for (var i = 0; i < ordered.Count; i++)
            {
                await _habits.SetSortKeyAsync(ordered[i].Id, keys[i]);
            }
        }
        else
        {
            var before = newIndex > 0 ? ordered[newIndex - 1].Habit.SortKey : null;
            var after = newIndex < ordered.Count ? ordered[newIndex].Habit.SortKey : null;
            await _habits.SetSortKeyAsync(item.Id, FractionalIndex.Between(before, after));
        }

        await LoadAsync();
    }
}
