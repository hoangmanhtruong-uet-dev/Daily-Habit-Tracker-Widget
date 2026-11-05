using HabitGrid.Core.Models;

namespace HabitGrid.Core.Services;

/// <summary>
/// Hiện thực <see cref="IDayProvider"/> dựa trên giờ địa phương của máy.
/// </summary>
public sealed class DayProvider : IDayProvider
{
    /// <summary>Mốc chuyển ngày tối đa được phép cấu hình (05:00).</summary>
    public static readonly TimeSpan MaxDayStartOffset = TimeSpan.FromHours(5);

    private readonly Func<DateTime> _localNowProvider;

    /// <param name="dayStartOffset">Mốc chuyển ngày. Mặc định 00:00. Chỉ nhận giá trị trong khoảng 00:00–05:00.</param>
    /// <param name="localNowProvider">
    /// Nguồn cung cấp thời gian địa phương hiện tại. Mặc định là <see cref="DateTime.Now"/>.
    /// Tham số này tồn tại để kiểm thử được các mốc chuyển ngày mà không phụ thuộc đồng hồ thật.
    /// </param>
    public DayProvider(TimeSpan? dayStartOffset = null, Func<DateTime>? localNowProvider = null)
    {
        var offset = dayStartOffset ?? TimeSpan.Zero;

        if (offset < TimeSpan.Zero || offset > MaxDayStartOffset)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dayStartOffset),
                offset,
                "Mốc chuyển ngày phải nằm trong khoảng 00:00 đến 05:00.");
        }

        DayStartOffset = offset;
        _localNowProvider = localNowProvider ?? (() => DateTime.Now);
    }

    public TimeSpan DayStartOffset { get; }

    public DateOnly GetToday() => ToLogicalDate(_localNowProvider());

    public DateOnly ToLogicalDate(DateTime localDateTime)
    {
        var local = localDateTime.Kind == DateTimeKind.Utc
            ? localDateTime.ToLocalTime()
            : localDateTime;

        return local.TimeOfDay < DayStartOffset
            ? DateOnly.FromDateTime(local.AddDays(-1))
            : DateOnly.FromDateTime(local);
    }

    public DateTime GetNextDayTransitionUtc()
    {
        var nowLocal = _localNowProvider();
        var logicalToday = ToLogicalDate(nowLocal);

        // Ngày logic kế tiếp bắt đầu tại nửa đêm + mốc chuyển ngày của ngày hôm sau.
        var nextTransitionLocal = logicalToday
            .AddDays(1)
            .ToDateTime(TimeOnly.MinValue)
            .Add(DayStartOffset);

        // Chốt chặn an toàn: mốc chuyển ngày luôn nằm ở tương lai.
        if (nextTransitionLocal <= nowLocal)
        {
            nextTransitionLocal = nowLocal.AddHours(1);
        }

        var localKind = DateTime.SpecifyKind(nextTransitionLocal, DateTimeKind.Local);
        return TimeZoneInfo.ConvertTimeToUtc(localKind, TimeZoneInfo.Local);
    }
}
