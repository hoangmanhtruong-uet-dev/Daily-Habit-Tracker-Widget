namespace HabitGrid.Data;

/// <summary>
/// Không tìm thấy thói quen theo định danh.
/// </summary>
public class HabitNotFoundException : Exception
{
    public HabitNotFoundException(Guid habitId)
        : base($"Không tìm thấy thói quen với định danh {habitId}.")
    {
        HabitId = habitId;
    }

    /// <summary>Định danh thói quen không tồn tại.</summary>
    public Guid HabitId { get; }
}
