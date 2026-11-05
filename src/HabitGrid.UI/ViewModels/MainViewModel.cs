using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HabitGrid.Core.Models;
using HabitGrid.Core.Services;
using HabitGrid.Core.Repositories;
using HabitGrid.Platform.Licensing;
using HabitGrid.UI.Services;

namespace HabitGrid.UI.ViewModels;

/// <summary>
/// ViewModel chính của bảng điều khiển quản lý.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IHabitRepository _habits;
    private readonly IEntryRepository _entries;
    private readonly IWidgetStateRepository _widgetStates;
    private readonly ILicenseService _license;
    private readonly IStreakCalculator _streakCalculator;
    private readonly IDayProvider _dayProvider;
    private readonly IDialogService _dialogs;
    private readonly SafeDeletionManager _deletion;
    private readonly Stack<UndoEntry> _undoStack = new();

    /// <summary>Thông tin để hoàn tác một lần đánh dấu.</summary>
    private sealed record UndoEntry(Guid HabitId, DateOnly Day, int PreviousValue);

    [ObservableProperty]
    private HabitItemViewModel? _selectedItem;

    [ObservableProperty]
    private HabitDetailViewModel? _detail;

    [ObservableProperty]
    private bool _isEditMode;

    public MainViewModel(
        IHabitRepository habits,
        IEntryRepository entries,
        IWidgetStateRepository widgetStates,
        ILicenseService license,
        IStreakCalculator streakCalculator,
        IDayProvider dayProvider,
        IDialogService dialogs,
        SafeDeletionManager deletion)
    {
        ArgumentNullException.ThrowIfNull(habits);
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(widgetStates);
        ArgumentNullException.ThrowIfNull(license);
        ArgumentNullException.ThrowIfNull(streakCalculator);
        ArgumentNullException.ThrowIfNull(dayProvider);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(deletion);

        _habits = habits;
        _entries = entries;
        _widgetStates = widgetStates;
        _license = license;
        _streakCalculator = streakCalculator;
        _dayProvider = dayProvider;
        _dialogs = dialogs;
        _deletion = deletion;

        AddHabitCommand = new AsyncRelayCommand(AddHabitAsync);
        EditHabitCommand = new AsyncRelayCommand<HabitItemViewModel>(EditHabitAsync);
        DeleteHabitCommand = new AsyncRelayCommand<HabitItemViewModel>(DeleteHabitAsync);
        ArchiveHabitCommand = new AsyncRelayCommand<HabitItemViewModel>(ArchiveHabitAsync);
        ToggleWidgetCommand = new AsyncRelayCommand<HabitItemViewModel>(ToggleWidgetAsync);
        UndoCommand = new AsyncRelayCommand(UndoLastAsync);
        ArchiveSelectedCommand = new AsyncRelayCommand(ArchiveSelectedAsync);

        _deletion.DeletionCommitted += OnDeletionCommitted;
        _deletion.DeletionUndone += OnDeletionUndone;
    }

    /// <summary>Danh sách thói quen đang hiển thị (chưa lưu trữ).</summary>
    public ObservableCollection<HabitItemViewModel> Items { get; } = new();

    /// <summary>Nhóm thói quen đã lưu trữ.</summary>
    public ObservableCollection<HabitItemViewModel> ArchivedItems { get; } = new();

    /// <summary>True nếu chưa có thói quen nào (dùng để hiện empty state / onboarding).</summary>
    public bool IsEmpty => Items.Count == 0;

    /// <summary>True nếu người dùng đã có Pro.</summary>
    public bool IsPro => _license.IsPro;

    /// <summary>Hạn mức thói quen của gói hiện tại.</summary>
    public int HabitLimit => _license.HabitLimit;

    /// <summary>Số thói quen đã dùng (kể cả đã lưu trữ).</summary>
    public int UsedCount { get; private set; }

    public IAsyncRelayCommand AddHabitCommand { get; }

    public IAsyncRelayCommand<HabitItemViewModel> EditHabitCommand { get; }

    public IAsyncRelayCommand<HabitItemViewModel> DeleteHabitCommand { get; }

    public IAsyncRelayCommand<HabitItemViewModel> ArchiveHabitCommand { get; }

    public IAsyncRelayCommand<HabitItemViewModel> ToggleWidgetCommand { get; }

    /// <summary>Hoàn tác thao tác đánh dấu gần nhất (Ctrl+Z).</summary>
    public IAsyncRelayCommand UndoCommand { get; }

    /// <summary>Lưu trữ thói quen đang chọn (phím Delete).</summary>
    public IAsyncRelayCommand ArchiveSelectedCommand { get; }

    /// <summary>Được gọi khi người dùng đổi chế độ chỉnh sửa.</summary>
    public event EventHandler<bool>? EditModeChanged;

    /// <summary>Được gọi khi cần hiển thị/ẩn widget của một thói quen.</summary>
    public event EventHandler<(Guid HabitId, bool Visible)>? WidgetVisibilityRequested;

    /// <summary>Nạp danh sách thói quen từ CSDL.</summary>
    public async Task LoadAsync()
    {
        var habits = await _habits.GetAllIncludingArchivedAsync();
        var states = await _widgetStates.GetAllAsync();
        var today = _dayProvider.GetToday();

        var visibleByHabit = states.ToDictionary(s => s.HabitId, s => s.Visible);

        Items.Clear();
        ArchivedItems.Clear();

        foreach (var habit in habits.Where(h => !h.IsArchived))
        {
            var item = await CreateItemAsync(habit, visibleByHabit, today);
            Items.Add(item);
        }

        foreach (var habit in habits.Where(h => h.IsArchived))
        {
            var item = await CreateItemAsync(habit, visibleByHabit, today);
            ArchivedItems.Add(item);
        }

        UsedCount = await _habits.CountNonDeletedAsync();

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(UsedCount));

        if (SelectedItem is null && Items.Count > 0)
        {
            SelectedItem = Items[0];
        }
        else
        {
            await RefreshDetailAsync();
        }
    }

    /// <summary>
    /// Di chuyển một thói quen trong danh sách (kéo-thả sắp xếp) và lưu thứ tự mới.
    /// </summary>
    public async Task MoveAsync(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= Items.Count)
        {
            return;
        }

        toIndex = Math.Clamp(toIndex, 0, Items.Count - 1);

        if (fromIndex == toIndex)
        {
            return;
        }

        var item = Items[fromIndex];
        Items.Move(fromIndex, toIndex);

        // Ghi lại sort_order theo thứ tự mới rồi lưu xuống CSDL.
        var orders = new List<(Guid HabitId, int SortOrder)>();

        for (var index = 0; index < Items.Count; index++)
        {
            Items[index].Model.SortOrder = index;
            orders.Add((Items[index].Id, index));
        }

        await _habits.UpdateSortOrdersAsync(orders);
    }

    /// <summary>Đặt widget hiển thị/ẩn và lưu vào CSDL.</summary>
    public async Task SetWidgetVisibleAsync(Guid habitId, bool visible)
    {
        var state = await _widgetStates.GetAsync(habitId) ?? new WidgetState { HabitId = habitId };
        state.Visible = visible;

        await _widgetStates.UpsertAsync(state);

        var item = Items.FirstOrDefault(i => i.Id == habitId);
        if (item is not null)
        {
            item.IsWidgetVisible = visible;
        }

        WidgetVisibilityRequested?.Invoke(this, (habitId, visible));
    }

    private async Task AddHabitAsync()
    {
        // Rào chắn giới hạn gói Free: mở màn hình nâng cấp thay vì báo lỗi thô thiển.
        if (!IsPro && UsedCount >= HabitLimit)
        {
            await _dialogs.ShowPaywallAsync(
                $"Gói Miễn phí cho phép tối đa {HabitLimit} thói quen. Bạn đang dùng {UsedCount}. " +
                "Nâng cấp Pro để không giới hạn thói quen.");

            return;
        }

        var result = await _dialogs.ShowEditHabitAsync(null);

        if (result is null)
        {
            return;
        }

        var habit = new Habit
        {
            Name = result.Name,
            Icon = result.IconKey,
            Kind = result.Kind,
            Target = result.Target,
            ScheduleMask = result.ScheduleMask,
            PaletteId = result.PaletteId,
            CustomColorHex = result.CustomColorHex,
            StartDate = _dayProvider.GetToday(),
            ReminderTime = result.ReminderEnabled ? result.ReminderTime : null,
            SortOrder = Items.Count
        };

        await _habits.InsertAsync(habit);
        await LoadAsync();

        SelectedItem = Items.FirstOrDefault(i => i.Id == habit.Id);
    }

    private async Task EditHabitAsync(HabitItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        var result = await _dialogs.ShowEditHabitAsync(item.Model);

        if (result is null)
        {
            return;
        }

        var habit = item.Model;

        habit.Name = result.Name;
        habit.Icon = result.IconKey;
        habit.Kind = result.Kind;
        habit.Target = result.Target;
        habit.ScheduleMask = result.ScheduleMask;
        habit.PaletteId = result.PaletteId;
        habit.CustomColorHex = result.CustomColorHex;
        habit.ReminderTime = result.ReminderEnabled ? result.ReminderTime : null;

        await _habits.UpdateAsync(habit);

        item.RefreshDisplay();

        await RefreshDetailAsync();
    }

    private async Task DeleteHabitAsync(HabitItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        // Xóa mềm ngay, kèm cửa sổ hoàn tác 10 giây.
        await _deletion.RequestDeleteAsync(item.Model);

        Items.Remove(item);
        ArchivedItems.Remove(item);

        OnPropertyChanged(nameof(IsEmpty));

        if (SelectedItem == item)
        {
            SelectedItem = Items.FirstOrDefault();
        }

        await LoadAsync();
    }

    private async Task ArchiveHabitAsync(HabitItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        await _habits.ArchiveAsync(item.Model.Id);
        await LoadAsync();
    }

    private async Task ToggleWidgetAsync(HabitItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        await SetWidgetVisibleAsync(item.Id, !item.IsWidgetVisible);
    }

    /// <summary>
    /// Đánh dấu / bỏ đánh dấu một ngày từ lưới chi tiết rồi làm mới số liệu.
    /// </summary>
    public async Task ToggleEntryAsync(Guid habitId, DateOnly day)
    {
        var habit = await _habits.GetByIdAsync(habitId);

        if (habit is null || habit.IsReadOnly)
        {
            return;
        }

        var existing = await _entries.GetAsync(habitId, day);
        var currentValue = existing?.Value ?? 0;

        var newValue = habit.Kind == HabitKind.Boolean
            ? (currentValue >= 1 ? 0 : 1)
            : currentValue + 1;

        await _entries.UpsertAsync(new Entry
        {
            HabitId = habitId,
            Day = day,
            Value = newValue,
            UpdatedAt = DateTime.UtcNow
        });

        // Ghi nhớ để có thể hoàn tác (Ctrl+Z).
        _undoStack.Push(new UndoEntry(habitId, day, currentValue));

        await RefreshDetailAsync();
        await UpdateItemStreakAsync(habitId);
    }

    /// <summary>Hoàn tác thao tác đánh dấu gần nhất.</summary>
    public async Task UndoLastAsync()
    {
        if (_undoStack.Count == 0)
        {
            return;
        }

        var entry = _undoStack.Pop();

        await _entries.UpsertAsync(new Entry
        {
            HabitId = entry.HabitId,
            Day = entry.Day,
            Value = entry.PreviousValue,
            UpdatedAt = DateTime.UtcNow
        });

        await RefreshDetailAsync();
        await UpdateItemStreakAsync(entry.HabitId);
    }

    private async Task ArchiveSelectedAsync()
    {
        if (SelectedItem is not null)
        {
            await ArchiveHabitAsync(SelectedItem);
        }
    }

    private async Task UpdateItemStreakAsync(Guid habitId)
    {
        var item = Items.FirstOrDefault(i => i.Id == habitId);

        if (item is null)
        {
            return;
        }

        var entries = await _entries.GetAllForHabitAsync(habitId);
        item.CurrentStreak = _streakCalculator.Calculate(item.Model, entries, _dayProvider.GetToday()).CurrentStreak;
    }

    private async Task<HabitItemViewModel> CreateItemAsync(Habit habit, Dictionary<Guid, bool> visibleByHabit, DateOnly today)
    {
        var entries = await _entries.GetAllForHabitAsync(habit.Id);
        var streak = _streakCalculator.Calculate(habit, entries, today);

        var item = new HabitItemViewModel(habit)
        {
            CurrentStreak = streak.CurrentStreak,
            IsWidgetVisible = !visibleByHabit.TryGetValue(habit.Id, out var visible) || visible
        };

        return item;
    }

    private async Task RefreshDetailAsync()
    {
        if (SelectedItem is null)
        {
            Detail = null;
            return;
        }

        Detail = new HabitDetailViewModel(
            SelectedItem.Model,
            _entries,
            _streakCalculator,
            _dayProvider,
            _dayProvider.GetToday());

        await Detail.LoadAsync();
    }

    partial void OnSelectedItemChanged(HabitItemViewModel? value) => _ = RefreshDetailAsync();

    partial void OnIsEditModeChanged(bool value) => EditModeChanged?.Invoke(this, value);

    private void OnDeletionCommitted(object? sender, SafeDeletionInfo info) =>
        _dialogs.ShowToast($"Đã xóa vĩnh viễn thói quen \"{info.Name}\".");

    private void OnDeletionUndone(object? sender, SafeDeletionInfo info) =>
        _dialogs.ShowToast($"Đã khôi phục thói quen \"{info.Name}\".");
}
