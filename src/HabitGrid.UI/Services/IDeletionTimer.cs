namespace HabitGrid.UI.Services;

/// <summary>
/// Hẹn giờ đơn giản, cho phép thay thế trong kiểm thử.
/// </summary>
public interface IDeletionTimer
{
    /// <summary>Hẹn gọi hành động sau <paramref name="delay"/>. Trả về mã hủy.</summary>
    IDisposable Schedule(TimeSpan delay, Action callback);
}
