using HabitGrid.Core.Services;
using HabitGrid.Core.Settings;

namespace HabitGrid.App;

/// <summary>
/// IDayProvider luôn đọc mốc chuyển ngày mới nhất từ cài đặt,
/// để màn quản lý, widget và nhắc nhở cùng tính "hôm nay" giống nhau.
/// </summary>
public sealed class SettingsDayProvider : IDayProvider
{
    private readonly AppSettings _settings;

    public SettingsDayProvider(AppSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public TimeSpan DayStartOffset => Current.DayStartOffset;

    private DayProvider Current => new(TimeSpan.FromHours(_settings.DayStartHour));

    public DateOnly GetToday() => Current.GetToday();

    public DateOnly ToLogicalDate(DateTime localDateTime) => Current.ToLogicalDate(localDateTime);

    public DateTime GetNextDayTransitionUtc() => Current.GetNextDayTransitionUtc();
}
