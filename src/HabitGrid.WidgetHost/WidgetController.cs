using System.Globalization;
using System.Windows.Threading;
using HabitGrid.Core.Domain;
using HabitGrid.Core.Services;
using HabitGrid.Data;
using HabitGrid.Data.V3;
using HabitGrid.Platform.Desktop;
using HabitGrid.Presentation.Services;
using HabitGrid.UI.Widgets;
using Microsoft.Data.Sqlite;
using V1 = HabitGrid.Core.Models;

namespace HabitGrid.WidgetHost;

/// <summary>
/// Quản lý các widget ghim desktop: đọc CSDL v3, dựng/làm mới cửa sổ, ghi check-in qua cùng repository với app chính.
/// Làm mới khi CSDL đổi (PRAGMA data_version, kể cả do tiến trình khác ghi) và khi qua ngày logic.
/// </summary>
public sealed class WidgetController : IDisposable
{
    public const int MaxWidgets = 3;

    private readonly HabitGridDatabase _database;
    private readonly HabitRepository _habits;
    private readonly DayLogRepository _logs;
    private readonly SettingsStore _settings;
    private readonly CheckInService _checkIn;
    private readonly IScreenProvider _screen = new WindowsScreenProvider();
    private readonly DesktopHostService _desktop;
    private readonly PaletteGenerator _palette = new();
    private readonly Dictionary<Guid, (WidgetWindow Window, WidgetViewModel Model)> _widgets = [];
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly SqliteConnection _watch;
    private long _dataVersion = -1;
    private DateOnly _today;
    private bool _refreshing;
    private bool _visible = true;

    public WidgetController(HabitGridDatabase database)
    {
        _database = database;
        var clock = new HlcClock(Environment.MachineName + ":widgets");
        var bus = new DataChangeBus();
        _habits = new HabitRepository(database, clock, bus);
        _logs = new DayLogRepository(database, clock, bus);
        _settings = new SettingsStore(database);
        _checkIn = new CheckInService(_habits, _logs);
        _desktop = new DesktopHostService(_screen);
        _watch = database.OpenConnection();
        _timer.Tick += async (_, _) => await PollAsync();
    }

    public IReadOnlyCollection<WidgetWindow> Windows => _widgets.Values.Select(w => w.Window).ToList();

    public IReadOnlyCollection<WidgetViewModel> Models => _widgets.Values.Select(w => w.Model).ToList();

    public DateOnly Today => _today;

    /// <summary>Nguồn trạng thái "chế độ sắp xếp" (menu khay); đọc mỗi nhịp làm mới.</summary>
    public Func<bool>? EditMode { get; init; }

    private bool _editApplied;

    public async Task StartAsync()
    {
        _today = CurrentDay();
        await RefreshAsync();
        _dataVersion = ReadDataVersion();
        _timer.Start();
    }

    public void ToggleVisibility()
    {
        _visible = !_visible;

        foreach (var (window, _) in _widgets.Values)
        {
            if (_visible)
            {
                window.Show();
            }
            else
            {
                window.Hide();
            }
        }
    }

    public void ReattachAll()
    {
        foreach (var (window, _) in _widgets.Values)
        {
            window.ApplyPinning(DesktopPinningMode.DesktopLevel);
        }
    }

    public void Dispose()
    {
        _timer.Stop();

        foreach (var (window, _) in _widgets.Values)
        {
            window.Close();
        }

        _widgets.Clear();
        _watch.Dispose();
    }

    /// <summary>Gọi khi cần làm mới ngay (sau check-in, sau khi thức dậy từ sleep).</summary>
    public async Task RefreshAsync()
    {
        if (_refreshing)
        {
            return;
        }

        _refreshing = true;

        try
        {
            var today = CurrentDay();
            var dayChanged = today != _today;
            _today = today;

            var habits = (await _habits.GetAllActiveAsync())
                .Where(h => h.Kind != HabitKind.Quit)
                .OrderBy(h => h.SortKey, StringComparer.Ordinal)
                .Take(MaxWidgets)
                .ToList();

            foreach (var stale in _widgets.Keys.Where(id => habits.All(h => h.Id != id)).ToList())
            {
                _widgets[stale].Window.Close();
                _widgets.Remove(stale);
            }

            var dark = IsSystemDark();

            for (var index = 0; index < habits.Count; index++)
            {
                var habit = habits[index];
                var logs = await _logs.GetAllForHabitAsync(habit.Id);
                var values = WidgetMapper.ToValues(habit, logs);

                if (_widgets.TryGetValue(habit.Id, out var existing))
                {
                    if (!dayChanged)
                    {
                        existing.Model.UpdateValues(values);
                        continue;
                    }

                    existing.Window.Close();
                }

                await CreateAsync(habit, values, dark, index);
            }
        }
        finally
        {
            _refreshing = false;
        }
    }

    /// <summary>Check-in một ngày rồi làm mới. Dùng cho widget bấm ô.</summary>
    public async Task CheckInAsync(Guid habitId, DateOnly day)
    {
        await _checkIn.ToggleAsync(habitId, day);
        await RefreshAsync();
    }

    private async Task CreateAsync(Habit habit, Dictionary<DateOnly, int> values, bool dark, int index)
    {
        var shades = _palette.GenerateFromHex(habit.ColorHue, dark);
        var model = new WidgetViewModel(WidgetMapper.ToWidgetHabit(habit), values, shades, _today, V1.WidgetSize.Large, dark);
        var window = new WidgetWindow(_screen);

        window.Bind(model);
        window.UseDesktopHost(_desktop);
        window.ApplyThemeColors(dark);
        window.Show();

        model.MarkRequested += async (_, day) => await CheckInAsync(habit.Id, day);
        model.CloseRequested += (_, _) => window.Hide();
        window.PositionChanged += async (_, args) =>
            await _settings.SetAsync(PositionKey(habit.Id), string.Create(CultureInfo.InvariantCulture, $"{args.Left},{args.Top}"));

        var saved = await _settings.GetAsync(PositionKey(habit.Id));

        if (saved?.Split(',') is [var left, var top] && int.TryParse(left, out var l) && int.TryParse(top, out var t))
        {
            window.SetScreenPosition(l, t);
        }
        else
        {
            window.SetScreenPosition(60 + index * 36, 80 + index * 160);
        }

        window.ApplyPinning(DesktopPinningMode.DesktopLevel);
        _widgets[habit.Id] = (window, model);
    }

    private static string PositionKey(Guid id) => "widget.pos." + id.ToString("N");

    private async Task PollAsync()
    {
        try
        {
            if (EditMode?.Invoke() is { } edit && edit != _editApplied)
            {
                _editApplied = edit;

                foreach (var (window, _) in _widgets.Values)
                {
                    window.SetEditMode(edit);
                }
            }

            var version = ReadDataVersion();

            if (version != _dataVersion || CurrentDay() != _today)
            {
                _dataVersion = version;
                await RefreshAsync();
            }
        }
        catch (Exception)
        {
            // Lần sau thử lại; widget giữ nguyên nội dung cũ.
        }
    }

    private long ReadDataVersion()
    {
        using var command = _watch.CreateCommand();
        command.CommandText = "PRAGMA data_version";
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static DateOnly CurrentDay() => new DayProvider(TimeSpan.Zero).GetToday();

    private static bool IsSystemDark()
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("AppsUseLightTheme") is int light && light == 0;
    }
}
