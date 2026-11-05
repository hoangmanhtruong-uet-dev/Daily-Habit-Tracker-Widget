using HabitGrid.Core.Domain;
using HabitGrid.Data;
using HabitGrid.Data.V3;
using HabitGrid.Presentation.Services;
using IDayProvider = HabitGrid.Core.Services.IDayProvider;

namespace HabitGrid.Presentation.Tests;

/// <summary>Ngày hôm nay do test điều khiển.</summary>
public sealed class FixedDayProvider : IDayProvider
{
    public FixedDayProvider(DateOnly today) => Today = today;

    public DateOnly Today { get; set; }

    public TimeSpan DayStartOffset => TimeSpan.Zero;

    public DateOnly GetToday() => Today;

    public DateOnly ToLogicalDate(DateTime localDateTime) => DateOnly.FromDateTime(localDateTime);

    public DateTime GetNextDayTransitionUtc() => DateTime.UtcNow.AddHours(1);
}

/// <summary>CSDL v3 trong bộ nhớ cùng các repository thật (không giả lập) cho test ViewModel.</summary>
public sealed class AppFixture : IDisposable
{
    public static readonly DateOnly Today = new(2026, 10, 22); // thứ Năm

    private long _ticks = 1_700_000_000_000;

    public AppFixture()
    {
        Database = HabitGridV3.OpenInMemory();
        Clock = new HlcClock("test", () => ++_ticks);
        Bus = new DataChangeBus();
        Habits = new HabitRepository(Database, Clock, Bus);
        Logs = new DayLogRepository(Database, Clock, Bus);
        Days = new FixedDayProvider(Today);
        Creation = new ViewModels.HabitCreationService(Habits, Days);
        Settings = new SettingsStore(Database);
        Profile = new ProfileRepository(Database, Clock, Bus);
        Achievements = new AchievementRepository(Database, Clock, Bus);
        Focus = new FocusRepository(Database, Clock, Bus);
        Localizer = new Localizer();
    }

    public HabitGridDatabase Database { get; }

    public HlcClock Clock { get; }

    public DataChangeBus Bus { get; }

    public HabitRepository Habits { get; }

    public DayLogRepository Logs { get; }

    public FixedDayProvider Days { get; }

    public ViewModels.HabitCreationService Creation { get; }

    public SettingsStore Settings { get; }

    public ProfileRepository Profile { get; }

    public AchievementRepository Achievements { get; }

    public FocusRepository Focus { get; }

    public Services.FocusService NewFocus() => new(Habits, Logs, Focus, Days);

    public Services.GamificationService NewGamification(IEntitlements? entitlements = null) =>
        new(Habits, Logs, Profile, Achievements, Settings, Days, entitlements, Focus);

    public Localizer Localizer { get; }

    public ViewModels.HabitEditorViewModel NewEditor(IEntitlements? entitlements = null) =>
        new(Habits, Days, entitlements);

    public ViewModels.OnboardingViewModel NewOnboarding(IEntitlements? entitlements = null) =>
        new(new ViewModels.HabitCreationService(Habits, Days, entitlements), Profile, Settings, Days, Localizer);

    public ViewModels.TodayViewModel NewToday(IUiDispatcher? dispatcher = null, bool withBus = false) =>
        new(Habits, Logs, Days, bus: withBus ? Bus : null, dispatcher: dispatcher);

    public async Task<Habit> AddAsync(
        string name,
        HabitKind kind = HabitKind.Boolean,
        int target = 1,
        TimeOfDay time = TimeOfDay.Anytime,
        HabitFrequency? frequency = null,
        int startDaysAgo = 0)
    {
        var result = await Creation.CreateAsync(new ViewModels.HabitDraft(
            name, HabitIcon.Default, kind, target, null, time, frequency, null, Today.AddDays(-startDaysAgo)));
        return result.Habit ?? throw new InvalidOperationException(string.Join(",", result.Errors.Select(e => e.Code)));
    }

    public async Task LogAsync(Habit habit, DateOnly day, int value)
    {
        var log = DayLogRules.Normalize(habit, new DayLog { HabitId = habit.Id, Day = day, Value = value });
        await Logs.UpsertAsync(log);
    }

    public void Dispose() => Database.Dispose();
}
