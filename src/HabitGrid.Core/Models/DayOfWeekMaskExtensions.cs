namespace HabitGrid.Core.Models;

/// <summary>
/// Hỗ trợ chuyển đổi giữa <see cref="DayOfWeek"/> và <see cref="DayOfWeekMask"/>.
/// </summary>
public static class DayOfWeekMaskExtensions
{
    /// <summary>
    /// Quy đổi ngày trong tuần sang bit tương ứng trong <see cref="DayOfWeekMask"/>.
    /// Thứ Hai ánh xạ tới bit 0, Chủ Nhật ánh xạ tới bit 6.
    /// </summary>
    public static DayOfWeekMask ToMask(this DayOfWeek dayOfWeek) => dayOfWeek switch
    {
        DayOfWeek.Monday => DayOfWeekMask.Monday,
        DayOfWeek.Tuesday => DayOfWeekMask.Tuesday,
        DayOfWeek.Wednesday => DayOfWeekMask.Wednesday,
        DayOfWeek.Thursday => DayOfWeekMask.Thursday,
        DayOfWeek.Friday => DayOfWeekMask.Friday,
        DayOfWeek.Saturday => DayOfWeekMask.Saturday,
        DayOfWeek.Sunday => DayOfWeekMask.Sunday,
        _ => DayOfWeekMask.None
    };

    /// <summary>
    /// Kiểm tra ngày có thuộc lịch lặp hay không.
    /// </summary>
    public static bool IsInMask(this DayOfWeekMask mask, DayOfWeek dayOfWeek)
    {
        var flag = dayOfWeek.ToMask();
        return flag != DayOfWeekMask.None && (mask & flag) == flag;
    }
}
