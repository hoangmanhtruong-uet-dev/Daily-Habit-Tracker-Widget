namespace HabitGrid.Core.Models;

/// <summary>
/// Nhật ký thực hiện của một thói quen trong một ngày logic.
/// </summary>
public class Entry
{
    public Guid HabitId { get; init; }

    /// <summary>Ngày logic (yyyy-MM-dd), đã tính theo mốc chuyển ngày của người dùng.</summary>
    public DateOnly Day { get; init; }

    /// <summary>Giá trị ghi nhận. Boolean: 0 hoặc 1. Count: số nguyên &gt;= 0.</summary>
    public int Value { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Kiểm tra ngày này đã hoàn thành chưa.
    /// Boolean hoàn thành khi Value &gt;= 1; Count hoàn thành khi Value &gt;= Target.
    /// </summary>
    public bool IsCompleted(Habit habit)
    {
        ArgumentNullException.ThrowIfNull(habit);

        return habit.Kind == HabitKind.Boolean ? Value >= 1 : Value >= habit.Target;
    }
}
