using HabitGrid.Core.Models;

namespace HabitGrid.Core.Services;

/// <summary>
/// Cung cấp "ngày logic" của hệ thống — ngày tính theo giờ địa phương, có thể dời mốc chuyển ngày
/// (mặc định 00:00, cho phép cấu hình 00:00–05:00).
/// </summary>
public interface IDayProvider
{
    /// <summary>Khoảng thời gian dời mốc chuyển ngày so với nửa đêm địa phương.</summary>
    TimeSpan DayStartOffset { get; }

    /// <summary>Ngày logic hiện tại.</summary>
    DateOnly GetToday();

    /// <summary>Quy đổi một thời điểm giờ địa phương về ngày logic tương ứng.</summary>
    DateOnly ToLogicalDate(DateTime localDateTime);

    /// <summary>
    /// Thời điểm UTC mà ngày logic sẽ chuyển sang ngày kế tiếp.
    /// Dùng để đặt timer duy nhất cho ứng dụng.
    /// </summary>
    DateTime GetNextDayTransitionUtc();
}
