namespace HabitGrid.Core.Models;

/// <summary>
/// Bitmask biểu diễn lịch lặp trong tuần.
/// Bit 0 = Thứ Hai ... Bit 6 = Chủ Nhật.
/// </summary>
[Flags]
public enum DayOfWeekMask
{
    None = 0,
    Monday = 1 << 0,      // 1
    Tuesday = 1 << 1,     // 2
    Wednesday = 1 << 2,   // 4
    Thursday = 1 << 3,    // 8
    Friday = 1 << 4,      // 16
    Saturday = 1 << 5,    // 32
    Sunday = 1 << 6,      // 64,
    AllWeek = Monday | Tuesday | Wednesday | Thursday | Friday | Saturday | Sunday // 127
}
