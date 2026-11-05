using HabitGrid.Core.Models;
using HabitGrid.Core.Services;
using HabitGrid.Data.Backup;
using HabitGrid.Platform.Desktop;
using HabitGrid.UI.Controls;
using HabitGrid.UI.Widgets;

namespace HabitGrid.App;

/// <summary>
/// Quản lý vòng đời các widget trên desktop:
/// nạp thói quen, dựng cửa sổ, ghim, lưu vị trí, áp dụng cài đặt và phản hồi thao tác đánh dấu.
/// </summary>
public sealed class WidgetManager : IDisposable
{
    private readonly AppServices _services;
    private readonly Dictionary<Guid, WidgetWindow> _windows = new();
    private readonly Dictionary<Guid, WidgetViewModel> _viewModels = new();

    private DesktopPinningMode _pinningMode = DesktopPinningMode.DesktopLevel;
    private bool _allWidgetsHidden;
    private bool _disposed;

    /// <summary>Ẩn hoặc hiện nhanh toàn bộ các widget ngoài desktop.</summary>
    public void ToggleAllVisibility()
    {
        _allWidgetsHidden = !_allWidgetsHidden;
        foreach (var window in _windows.Values)
        {
            if (_allWidgetsHidden)
            {
                window.Hide();
            }
            else
            {
                window.Show();
            }
        }
    }

    public WidgetManager(AppServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;

        _pinningMode = (DesktopPinningMode)_services.AppSettings.PinningMode;
    }

    /// <summary>Chế độ ghim hiện tại.</summary>
    public DesktopPinningMode PinningMode => _pinningMode;

    /// <summary>Thông điệp trạng thái để hiển thị lên bảng điều khiển.</summary>
    public event EventHandler<string>? StatusChanged;

    /// <summary>Các widget đang mở.</summary>
    public IReadOnlyCollection<WidgetWindow> Windows => _windows.Values;

    /// <summary>Nạp thói quen và dựng widget theo trạng thái đã lưu.</summary>
    public async Task LoadAsync()
    {
        var habits = await _services.Habits.GetAllIncludingArchivedAsync();
        var states = await _services.WidgetStates.GetAllAsync();
        var today = GetToday();

        var stateByHabit = states.ToDictionary(s => s.HabitId);

        foreach (var habit in habits.Where(h => !h.IsArchived))
        {
            stateByHabit.TryGetValue(habit.Id, out var state);

            if (state is not null && !state.Visible)
            {
                continue;
            }

            await CreateWidgetAsync(habit, state);
        }

        await ApplySettingsAsync();

        Report($"Đã nạp {habits.Count} thói quen, dựng {_windows.Count} widget.");
    }

    /// <summary>Bật/tắt chế độ chỉnh sửa cho toàn bộ widget.</summary>
    public void SetEditMode(bool enabled)
    {
        foreach (var window in _windows.Values)
        {
            window.SetEditMode(enabled);
        }
    }

    /// <summary>Ngày logic hiện tại theo mốc chuyển ngày đã cấu hình.</summary>
    private DateOnly GetToday() => new DayProvider(TimeSpan.FromHours(_services.AppSettings.DayStartHour)).GetToday();

    /// <summary>Áp dụng toàn bộ cài đặt lên các widget đang mở.</summary>
    public async Task ApplySettingsAsync()
    {
        var settings = _services.AppSettings;
        var isDark = _services.ThemeService.IsDarkTheme;

        foreach (var window in _windows.Values)
        {
            window.ApplyThemeColors(isDark);
            window.ApplyWidgetStyle(settings.BackgroundStyle, settings.BackgroundOpacity, settings.CornerRadius);
            window.SetEditMode(settings.EditMode);
        }

        await ApplyPinningAsync((DesktopPinningMode)settings.PinningMode);
    }

    /// <summary>Đổi chế độ ghim cho toàn bộ widget, áp dụng ngay trong runtime.</summary>
    public async Task ApplyPinningAsync(DesktopPinningMode mode)
    {
        _pinningMode = mode;

        var fallbacks = new List<string>();

        foreach (var window in _windows.Values)
        {
            var result = window.ApplyPinning(mode);

            if (result.UsedFallback && result.Message is not null)
            {
                fallbacks.Add(result.Message);
            }
        }

        await Task.CompletedTask;

        Report(fallbacks.Count > 0
            ? $"Đã áp dụng chế độ {mode}. {fallbacks[0]}"
            : $"Đã áp dụng chế độ {mode} cho {_windows.Count} widget.");
    }

    /// <summary>Ghim lại widget sau khi Explorer khởi động lại.</summary>
    public void ReattachAll()
    {
        foreach (var window in _windows.Values)
        {
            window.ApplyPinning(_pinningMode);
        }

        Report("Explorer đã khởi động lại — đã ghim lại toàn bộ widget.");
    }

    /// <summary>Tính lại vị trí widget khi cấu hình màn hình thay đổi.</summary>
    public async Task RepositionAllAsync()
    {
        foreach (var (habitId, window) in _windows.ToArray())
        {
            var state = await _services.WidgetStates.GetAsync(habitId);

            if (state is null)
            {
                continue;
            }

            var screen = _services.ScreenProvider.GetScreenByDevicePath(state.Monitor)
                         ?? _services.ScreenProvider.GetPrimaryScreen();

            var (left, top) = WidgetPlacement.FromRelative(
                screen,
                new RelativePosition(state.X, state.Y),
                (int)window.Width,
                (int)window.Height);

            window.SetScreenPosition(left, top);
        }

        Report("Đã tính lại vị trí widget theo cấu hình màn hình mới.");
    }

    /// <summary>Đưa toàn bộ widget về giữa màn hình chính.</summary>
    public async Task ResetPositionsAsync()
    {
        var screen = _services.ScreenProvider.GetPrimaryScreen();

        foreach (var (habitId, window) in _windows.ToArray())
        {
            var left = screen.WorkArea.Left + ((screen.WorkArea.Width - (int)window.Width) / 2);
            var top = screen.WorkArea.Top + ((screen.WorkArea.Height - (int)window.Height) / 2);

            window.SetScreenPosition(left, top);

            var state = await _services.WidgetStates.GetAsync(habitId) ?? new WidgetState { HabitId = habitId };
            state.Monitor = screen.DevicePath;
            state.X = 0.5;
            state.Y = 0.5;

            await _services.WidgetStates.UpsertAsync(state);
        }

        Report("Đã đặt lại vị trí toàn bộ widget về giữa màn hình chính.");
    }

    /// <summary>Bật/tắt hiển thị widget của một thói quen.</summary>
    public async Task SetWidgetVisibleAsync(Guid habitId, bool visible)
    {
        var state = await _services.WidgetStates.GetAsync(habitId) ?? new WidgetState { HabitId = habitId };
        state.Visible = visible;

        await _services.WidgetStates.UpsertAsync(state);

        if (visible)
        {
            if (!_windows.ContainsKey(habitId))
            {
                var habit = await _services.Habits.GetByIdAsync(habitId);

                if (habit is not null)
                {
                    await CreateWidgetAsync(habit, state);
                    await ApplySettingsAsync();
                }
            }
        }
        else if (_windows.Remove(habitId, out var window))
        {
            window.Close();
        }

        Report(visible ? "Đã hiện widget." : "Đã ẩn widget.");
    }

    /// <summary>Xuất bản sao lưu JSON.</summary>
    public Task<string> ExportBackupJsonAsync() => GetBackupService().ExportJsonAsync();

    /// <summary>Xuất CSV của một thói quen.</summary>
    public Task<byte[]> ExportBackupCsvAsync(Guid habitId) => GetBackupService().ExportCsvForHabitAsync(habitId);

    /// <summary>Nhập bản sao lưu JSON.</summary>
    public Task ImportBackupJsonAsync(string json, ImportMode mode) =>
        GetBackupService().ImportJsonAsync(json, mode, _services.License.IsPro);

    /// <summary>Tạo vài thói quen mẫu kèm lịch sử để kiểm thử nhanh.</summary>
    public async Task<IReadOnlyList<Habit>> CreateDemoHabitsAsync()
    {
        var existing = await _services.Habits.GetAllIncludingArchivedAsync();

        if (existing.Count > 0)
        {
            return existing;
        }

        var today = GetToday();

        var definitions = new (string Name, string Icon, HabitKind Kind, int Target)[]
        {
            ("Đọc sách", "Book24Regular", HabitKind.Boolean, 1),
            ("Uống nước", "WaterDrop24Regular", HabitKind.Count, 8),
            ("Tập thể dục", "Bicycle24Regular", HabitKind.Boolean, 1)
        };

        var created = new List<Habit>();

        for (var index = 0; index < definitions.Length; index++)
        {
            var definition = definitions[index];
            var habit = new Habit
            {
                Name = definition.Name,
                Icon = definition.Icon,
                Kind = definition.Kind,
                Target = definition.Target,
                SortOrder = index,
                StartDate = today.AddDays(-120)
            };

            await _services.Habits.InsertAsync(habit);

            // Lịch sử mẫu: 80% ngày gần đây được đánh dấu.
            for (var day = 0; day < 120; day++)
            {
                if (day % 5 == 0)
                {
                    continue;
                }

                var value = definition.Kind == HabitKind.Count
                    ? definition.Target
                    : 1;

                await _services.Entries.UpsertAsync(new Entry
                {
                    HabitId = habit.Id,
                    Day = today.AddDays(-day),
                    Value = value
                });
            }

            created.Add(habit);
        }

        Report($"Đã tạo {created.Count} thói quen mẫu kèm lịch sử 120 ngày.");
        return created;
    }

    private async Task CreateWidgetAsync(Habit habit, WidgetState? state)
    {
        var entries = await _services.Entries.GetAllForHabitAsync(habit.Id);
        var values = entries.ToDictionary(e => e.Day, e => e.Value);

        var today = GetToday();
        var isDark = _services.ThemeService.IsDarkTheme;
        var shades = new PaletteGenerator().GetPreset(habit.PaletteId, isDark);
        var size = state?.Size ?? (WidgetSize)_services.AppSettings.DefaultWidgetSize;

        var viewModel = new WidgetViewModel(
            habit,
            values,
            shades,
            today,
            size,
            isDark,
            weekStart: _services.AppSettings.WeekStart);

        var window = new WidgetWindow(_services.ScreenProvider);

        window.Bind(viewModel);
        window.UseDesktopHost(_services.DesktopHost);
        window.ApplyThemeColors(isDark);
        window.ApplyWidgetStyle(
            _services.AppSettings.BackgroundStyle,
            _services.AppSettings.BackgroundOpacity,
            _services.AppSettings.CornerRadius);

        viewModel.MarkRequested += async (_, day) => await ToggleEntryAsync(habit.Id, day, viewModel);
        viewModel.CloseRequested += async (_, _) => await SetWidgetVisibleAsync(habit.Id, false);

        window.PositionChanged += async (_, args) => await SavePositionAsync(habit.Id, args.Left, args.Top, args.Width, args.Height);

        window.Show();

        _windows[habit.Id] = window;
        _viewModels[habit.Id] = viewModel;

        await RestorePositionAsync(habit.Id, window, state);
    }

    private async Task RestorePositionAsync(Guid habitId, WidgetWindow window, WidgetState? state)
    {
        if (state is null)
        {
            return;
        }

        var screen = _services.ScreenProvider.GetScreenByDevicePath(state.Monitor)
                     ?? _services.ScreenProvider.GetPrimaryScreen();

        var (left, top) = WidgetPlacement.FromRelative(
            screen,
            new RelativePosition(state.X, state.Y),
            (int)window.Width,
            (int)window.Height);

        window.SetScreenPosition(left, top);

        await Task.CompletedTask;
    }

    private async Task SavePositionAsync(Guid habitId, int left, int top, int width, int height)
    {
        var screen = _services.ScreenProvider.GetScreenAt(left + (width / 2), top + (height / 2))
                     ?? _services.ScreenProvider.GetPrimaryScreen();

        var relative = WidgetPlacement.ToRelative(screen, left, top, width, height);

        var state = await _services.WidgetStates.GetAsync(habitId)
                    ?? new WidgetState { HabitId = habitId };

        state.Monitor = screen.DevicePath;
        state.X = relative.X;
        state.Y = relative.Y;

        await _services.WidgetStates.UpsertAsync(state);
    }

    private async Task ToggleEntryAsync(Guid habitId, DateOnly day, WidgetViewModel viewModel)
    {
        var habit = await _services.Habits.GetByIdAsync(habitId);

        if (habit is null)
        {
            return;
        }

        var existing = await _services.Entries.GetAsync(habitId, day);
        var currentValue = existing?.Value ?? 0;

        var newValue = habit.Kind == HabitKind.Boolean
            ? (currentValue >= 1 ? 0 : 1)
            : currentValue + 1;

        await _services.Entries.UpsertAsync(new Entry
        {
            HabitId = habitId,
            Day = day,
            Value = newValue,
            UpdatedAt = DateTime.UtcNow
        });

        var values = new Dictionary<DateOnly, int>(viewModel.Values);
        values[day] = newValue;

        viewModel.UpdateValues(values);
    }

    private BackupService GetBackupService() => new(
        _services.Database,
        _services.Habits,
        _services.Entries);

    private void Report(string message) => StatusChanged?.Invoke(this, message);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var window in _windows.Values)
        {
            window.Close();
        }

        _windows.Clear();
        _viewModels.Clear();
    }
}
