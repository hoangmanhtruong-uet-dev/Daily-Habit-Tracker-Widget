using Microsoft.Win32;
using HabitGrid.Core.Models;
using HabitGrid.Core.Services;
using HabitGrid.Platform.System;

namespace HabitGrid.App;

/// <summary>
/// Quản lý mốc chuyển ngày logic, sự kiện ngủ dậy của máy tính (Sleep/Resume)
/// và lịch kiểm tra thông báo nhắc nhở hằng ngày.
/// </summary>
public sealed class DayTransitionManager : IDisposable
{
    private readonly AppServices _services;
    private readonly IToastNotificationService _toastService;
    private Timer? _transitionTimer;
    private Timer? _reminderPollTimer;
    private DateOnly _currentLogicalDay;
    private bool _disposed;

    public DayTransitionManager(AppServices services, IToastNotificationService toastService)
    {
        _services = services;
        _toastService = toastService;

        _currentLogicalDay = GetCurrentDayProvider().GetToday();

        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.TimeChanged += OnTimeChanged;

        _toastService.ActionInvoked += OnToastActionInvoked;

        ScheduleNextDayTransition();
        StartReminderPolling();
    }

    private IDayProvider GetCurrentDayProvider() =>
        new DayProvider(TimeSpan.FromHours(_services.AppSettings.DayStartHour));

    private void ScheduleNextDayTransition()
    {
        _transitionTimer?.Dispose();

        var dayProvider = GetCurrentDayProvider();
        var nextUtc = dayProvider.GetNextDayTransitionUtc();
        var delay = nextUtc - DateTime.UtcNow;

        if (delay <= TimeSpan.Zero)
        {
            delay = TimeSpan.FromSeconds(5);
        }

        _transitionTimer = new Timer(async _ => await OnDayTransitionTickAsync(), null, delay, Timeout.InfiniteTimeSpan);
    }

    private async Task OnDayTransitionTickAsync()
    {
        var newLogicalDay = GetCurrentDayProvider().GetToday();

        if (newLogicalDay != _currentLogicalDay)
        {
            _currentLogicalDay = newLogicalDay;
            await _services.Widgets.ApplySettingsAsync();
        }

        ScheduleNextDayTransition();
    }

    private async void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
        {
            // Đợi 2 giây cho hệ thống mạng và đồng hồ ổn định sau khi thức dậy
            await Task.Delay(2000);

            var newDay = GetCurrentDayProvider().GetToday();
            if (newDay != _currentLogicalDay)
            {
                _currentLogicalDay = newDay;
                await _services.Widgets.ApplySettingsAsync();
            }

            ScheduleNextDayTransition();
        }
    }

    private void OnTimeChanged(object? sender, EventArgs e)
    {
        ScheduleNextDayTransition();
    }

    private void StartReminderPolling()
    {
        // Kiểm tra giờ nhắc nhở mỗi 60 giây
        _reminderPollTimer = new Timer(async _ => await CheckRemindersAsync(), null, TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(1));
    }

    private async Task CheckRemindersAsync()
    {
        var nowTime = TimeOnly.FromDateTime(DateTime.Now);
        var today = GetCurrentDayProvider().GetToday();
        var todayMask = (DayOfWeekMask)(1 << ((int)DateTime.Now.DayOfWeek == 0 ? 6 : (int)DateTime.Now.DayOfWeek - 1));

        var habits = await _services.Habits.GetAllActiveAsync();

        foreach (var habit in habits)
        {
            if (habit.ReminderTime == null)
            {
                continue;
            }

            // Kiểm tra xem hôm nay có nằm trong lịch nhắc không
            if ((habit.ReminderMask & todayMask) == 0)
            {
                continue;
            }

            // Kiểm tra xem hiện tại có đúng trong khung phút của ReminderTime không
            var reminder = habit.ReminderTime.Value;
            if (nowTime.Hour == reminder.Hour && nowTime.Minute == reminder.Minute)
            {
                // Kiểm tra xem hôm nay đã hoàn thành chưa
                var entry = await _services.Entries.GetAsync(habit.Id, today);
                var isCompleted = entry != null && (habit.Kind == HabitKind.Boolean ? entry.Value >= 1 : entry.Value >= habit.Target);

                if (!isCompleted)
                {
                    var entries = await _services.Entries.GetAllForHabitAsync(habit.Id);
                    var streakCalc = new StreakCalculator(new ScheduleCalculator());
                    var streak = streakCalc.Calculate(habit, entries, today).CurrentStreak;

                    _toastService.ShowReminderToast(habit.Id, habit.Name, streak);
                }
            }
        }
    }

    private async void OnToastActionInvoked(object? sender, ToastActionArgs e)
    {
        if (e.Action.Equals("checkin", StringComparison.OrdinalIgnoreCase))
        {
            var today = GetCurrentDayProvider().GetToday();
            var habit = await _services.Habits.GetByIdAsync(e.HabitId);
            if (habit == null) return;

            var existing = await _services.Entries.GetAsync(e.HabitId, today);
            var newValue = habit.Kind == HabitKind.Boolean ? 1 : Math.Max(habit.Target, (existing?.Value ?? 0) + 1);

            await _services.Entries.UpsertAsync(new Entry
            {
                HabitId = e.HabitId,
                Day = today,
                Value = newValue,
                UpdatedAt = DateTime.UtcNow
            });

            await _services.Widgets.ApplySettingsAsync();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.TimeChanged -= OnTimeChanged;
        _toastService.ActionInvoked -= OnToastActionInvoked;

        _transitionTimer?.Dispose();
        _reminderPollTimer?.Dispose();
    }
}
