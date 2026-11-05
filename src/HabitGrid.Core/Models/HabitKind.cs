namespace HabitGrid.Core.Models;

/// <summary>
/// Loại thói quen được theo dõi.
/// </summary>
public enum HabitKind
{
    /// <summary>Thói quen Đúng/Sai (Đã làm / Chưa làm).</summary>
    Boolean = 0,

    /// <summary>Thói quen Đếm số lượng (hoàn thành khi Value &gt;= Target).</summary>
    Count = 1
}
