using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HabitGrid.Core.Domain;
using IDayProvider = HabitGrid.Core.Services.IDayProvider;
using HabitGrid.Presentation.Services;

namespace HabitGrid.Presentation.ViewModels;

/// <summary>Một dòng thói quen trên trang Hôm nay.</summary>
public sealed partial class HabitTodayItem : ObservableObject
{
    public HabitTodayItem(Habit habit)
    {
        Habit = habit;
    }

    public Habit Habit { get; }

    public Guid Id => Habit.Id;

    public string Name => Habit.Name;

    public string Icon => Habit.Icon.Value;

    public string ColorHue => Habit.ColorHue;

    public HabitKind Kind => Habit.Kind;

    public int Target => Habit.EffectiveTarget;

    public string? Unit => Habit.Target.Unit;

    [ObservableProperty]
    private int _value;

    [ObservableProperty]
    private DayStatus _status;

    [ObservableProperty]
    private int _streak;

    [ObservableProperty]
    private StreakUnit _streakUnit;

    [ObservableProperty]
    private bool _atRisk;

    /// <summary>Trạng thái 7 ngày gần nhất (cũ → mới, ngày cuối là hôm nay), dùng cho mini heatmap.</summary>
    [ObservableProperty]
    private IReadOnlyList<DayStatus> _lastSevenDays = [];

    public bool IsDone => Status == DayStatus.Done;

    /// <summary>0..1, dùng cho vòng tiến độ của loại đếm/thời lượng.</summary>
    public double Progress => Kind switch
    {
        HabitKind.Boolean => IsDone ? 1 : 0,
        HabitKind.Quit => Value == 0 ? 1 : 0,
        _ => Math.Clamp(Value / (double)Target, 0, 1)
    };

    partial void OnValueChanged(int value) => OnPropertyChanged(nameof(Progress));

    partial void OnStatusChanged(DayStatus value)
    {
        OnPropertyChanged(nameof(IsDone));
        OnPropertyChanged(nameof(Progress));
    }
}

public sealed record TodayGroup(TimeOfDay TimeOfDay, IReadOnlyList<HabitTodayItem> Items);

/// <summary>Thao tác gần nhất để hoàn tác.</summary>
public sealed record UndoEntry(Guid HabitId, string HabitName, DateOnly Day, DayLog? Previous);

/// <summary>
/// Trang "Hôm nay" (SPEC-V2-04 §5, 02-UX-SCREENS §3): danh sách thói quen cần làm hôm nay theo khung giờ,
/// check-in một chạm, hoàn tác, tiến độ trong ngày.
/// </summary>
public sealed partial class TodayViewModel : ObservableObject, IDisposable
{
    private readonly IHabitRepository _habits;
    private readonly IDayLogRepository _logs;
    private readonly IDayProvider _dayProvider;
    private readonly IStreakCalculator _streaks;
    private readonly IScheduleCalculator _schedule;
    private readonly IDataChangeBus? _bus;
    private readonly IUiDispatcher _dispatcher;
    private readonly IAppLogger _logger;
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private int _writes;
    private bool _disposed;

    public TodayViewModel(
        IHabitRepository habits,
        IDayLogRepository logs,
        IDayProvider dayProvider,
        IStreakCalculator? streaks = null,
        IScheduleCalculator? schedule = null,
        IDataChangeBus? bus = null,
        IUiDispatcher? dispatcher = null,
        IAppLogger? logger = null)
    {
        _habits = habits ?? throw new ArgumentNullException(nameof(habits));
        _logs = logs ?? throw new ArgumentNullException(nameof(logs));
        _dayProvider = dayProvider ?? throw new ArgumentNullException(nameof(dayProvider));
        _streaks = streaks ?? new StreakCalculator();
        _schedule = schedule ?? new ScheduleCalculator();
        _bus = bus;
        _dispatcher = dispatcher ?? new ImmediateDispatcher();
        _logger = logger ?? NullLogger.Instance;

        if (_bus is not null)
        {
            _bus.Changed += OnDataChanged;
        }
    }

    /// <summary>Thói quen chưa xong, nhóm theo khung giờ (Sáng, Chiều, Tối, Bất kỳ).</summary>
    public ObservableCollection<TodayGroup> Groups { get; } = [];

    /// <summary>Đã xong hôm nay.</summary>
    public ObservableCollection<HabitTodayItem> Completed { get; } = [];

    /// <summary>Không có lịch hôm nay.</summary>
    public ObservableCollection<HabitTodayItem> NotToday { get; } = [];

    public DateOnly Today { get; private set; }

    [ObservableProperty]
    private int _doneCount;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private bool _isEmpty = true;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private UndoEntry? _lastAction;

    [ObservableProperty]
    private string? _errorMessage;

    public double ProgressFraction => TotalCount == 0 ? 0 : DoneCount / (double)TotalCount;

    public bool AllDone => TotalCount > 0 && DoneCount == TotalCount;

    public bool CanUndo => LastAction is not null;

    partial void OnDoneCountChanged(int value) => RaiseProgress();

    partial void OnTotalCountChanged(int value) => RaiseProgress();

    partial void OnLastActionChanged(UndoEntry? value) => OnPropertyChanged(nameof(CanUndo));

    private void RaiseProgress()
    {
        OnPropertyChanged(nameof(ProgressFraction));
        OnPropertyChanged(nameof(AllDone));
    }

    // ------------------------------------------------------------------ nạp dữ liệu

    [RelayCommand]
    public async Task LoadAsync()
    {
        await _loadGate.WaitAsync();

        try
        {
            IsLoading = true;
            ErrorMessage = null;

            var today = _dayProvider.GetToday();
            var habits = await _habits.GetAllActiveAsync();
            var items = new List<HabitTodayItem>();

            foreach (var habit in habits)
            {
                var logs = await _logs.GetAllForHabitAsync(habit.Id);
                items.Add(BuildItem(habit, logs, today));
            }

            _dispatcher.Post(() => Apply(today, items));
        }
        catch (Exception ex)
        {
            _logger.Error("Tải trang Hôm nay thất bại", ex);
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
            _loadGate.Release();
        }
    }

    private HabitTodayItem BuildItem(Habit habit, IReadOnlyList<DayLog> logs, DateOnly today)
    {
        var item = new HabitTodayItem(habit);
        FillItem(item, logs, today);
        return item;
    }

    private void FillItem(HabitTodayItem item, IReadOnlyList<DayLog> logs, DateOnly today)
    {
        var habit = item.Habit;
        var todayLog = logs.FirstOrDefault(l => l.Day == today);
        var streak = _streaks.Calculate(habit, logs, today);

        item.Value = todayLog?.Value ?? 0;
        item.Status = todayLog?.Status ?? DayStatus.None;
        item.Streak = streak.Current;
        item.StreakUnit = streak.Unit;
        item.AtRisk = streak.AtRiskToday;

        var byDay = logs.ToDictionary(l => l.Day, l => l.Status);
        item.LastSevenDays = Enumerable.Range(-6, 7)
            .Select(offset => byDay.GetValueOrDefault(today.AddDays(offset), DayStatus.None))
            .ToList();
    }

    private void Apply(DateOnly today, List<HabitTodayItem> items)
    {
        Today = today;
        Groups.Clear();
        Completed.Clear();
        NotToday.Clear();

        var scheduled = new List<HabitTodayItem>();

        foreach (var item in items)
        {
            if (!_schedule.IsScheduled(item.Habit, today))
            {
                NotToday.Add(item);
            }
            else
            {
                scheduled.Add(item);
            }
        }

        // Thói quen "cần bỏ" luôn nằm trong danh sách (để ghi nhận lần lỡ) và không tính vào tiến độ ngày.
        var trackable = scheduled.Where(i => i.Kind != HabitKind.Quit).ToList();

        foreach (var item in trackable.Where(i => i.IsDone))
        {
            Completed.Add(item);
        }

        var pending = scheduled.Where(i => i.Kind == HabitKind.Quit || !i.IsDone).ToList();

        foreach (var time in new[] { TimeOfDay.Morning, TimeOfDay.Afternoon, TimeOfDay.Evening, TimeOfDay.Anytime })
        {
            var inGroup = pending.Where(i => i.Habit.TimeOfDay == time).ToList();
            if (inGroup.Count > 0)
            {
                Groups.Add(new TodayGroup(time, inGroup));
            }
        }

        TotalCount = trackable.Count;
        DoneCount = Completed.Count;
        IsEmpty = items.Count == 0;
    }

    // ------------------------------------------------------------------ check-in

    /// <summary>Chạm vào một dòng: có/không → đảo; đếm → +1; thời lượng → đảo giữa 0 và mục tiêu; cần bỏ → ghi một lần lỡ.</summary>
    [RelayCommand]
    public async Task CheckInAsync(HabitTodayItem? item)
    {
        if (item is null)
        {
            return;
        }

        var newValue = item.Kind switch
        {
            HabitKind.Boolean => item.IsDone ? 0 : 1,
            HabitKind.Count => item.Value + 1,
            HabitKind.Duration => item.IsDone ? 0 : item.Target,
            HabitKind.Quit => item.Value + 1,
            _ => item.Value
        };

        await SetValueAsync(item, newValue);
    }

    /// <summary>Giảm một đơn vị (menu chuột phải của loại đếm, hoặc xóa một lần lỡ của loại cần bỏ).</summary>
    [RelayCommand]
    public async Task DecrementAsync(HabitTodayItem? item)
    {
        if (item is null || item.Value <= 0)
        {
            return;
        }

        await SetValueAsync(item, item.Value - 1);
    }

    /// <summary>Đặt giá trị cụ thể (nhập số từ menu).</summary>
    public async Task SetValueAsync(HabitTodayItem item, int newValue)
    {
        ArgumentNullException.ThrowIfNull(item);

        var day = Today;
        Interlocked.Increment(ref _writes);

        try
        {
            var previous = await _logs.GetAsync(item.Id, day);

            // Ngày đã đánh dấu bỏ qua/đóng băng giữ nguyên trạng thái đó khi chưa có giá trị.
            var keepManualStatus = previous is { Status: DayStatus.Skipped or DayStatus.Frozen } && newValue <= 0;

            var log = DayLogRules.Normalize(item.Habit, new DayLog
            {
                HabitId = item.Id,
                Day = day,
                Value = Math.Max(0, newValue),
                Note = previous?.Note,
                Mood = previous?.Mood,
                Status = keepManualStatus ? previous!.Status : DayStatus.None,
                CheckedAt = newValue > 0 ? DateTime.UtcNow : null
            });

            // Về 0 và không có ghi chú/mood thì xóa dòng thay vì lưu một dòng trống.
            var isBlank = log.Value == 0 && log.Note is null && log.Mood is null &&
                          log.Status is DayStatus.None or DayStatus.Done;

            if (isBlank)
            {
                if (previous is not null)
                {
                    await _logs.DeleteAsync(item.Id, day);
                }
            }
            else
            {
                await _logs.UpsertAsync(log);
            }

            LastAction = new UndoEntry(item.Id, item.Name, day, previous);
        }
        catch (Exception ex)
        {
            _logger.Error($"Check-in thất bại cho {item.Name}", ex);
            ErrorMessage = ex.Message;
            return;
        }
        finally
        {
            Interlocked.Decrement(ref _writes);
        }

        await LoadAsync();
    }

    /// <summary>Hoàn tác thao tác gần nhất.</summary>
    [RelayCommand]
    public async Task UndoAsync()
    {
        var action = LastAction;
        if (action is null)
        {
            return;
        }

        Interlocked.Increment(ref _writes);

        try
        {
            if (action.Previous is null)
            {
                await _logs.DeleteAsync(action.HabitId, action.Day);
            }
            else
            {
                await _logs.UpsertAsync(action.Previous);
            }

            LastAction = null;
        }
        catch (Exception ex)
        {
            _logger.Error("Hoàn tác thất bại", ex);
            ErrorMessage = ex.Message;
            return;
        }
        finally
        {
            Interlocked.Decrement(ref _writes);
        }

        await LoadAsync();
    }

    // ------------------------------------------------------------------ làm mới theo dữ liệu

    private void OnDataChanged(object? sender, DataChange change)
    {
        // Thay đổi do chính mình gây ra đã được nạp lại ở cuối thao tác.
        if (_disposed || Volatile.Read(ref _writes) > 0)
        {
            return;
        }

        if (change.Entity is DataEntity.Habit or DataEntity.DayLog or DataEntity.Everything)
        {
            _ = LoadAsync();
        }
    }

    public void Dispose()
    {
        _disposed = true;

        if (_bus is not null)
        {
            _bus.Changed -= OnDataChanged;
        }

        _loadGate.Dispose();
    }
}
