using HabitGrid.Core.Models;
using HabitGrid.Core.Repositories;

namespace HabitGrid.UI.Services;

/// <summary>
/// Thông tin về một lần xóa đang chờ hoàn tác.
/// </summary>
/// <param name="HabitId">Định danh thói quen.</param>
/// <param name="Name">Tên thói quen.</param>
public sealed record SafeDeletionInfo(Guid HabitId, string Name);

/// <summary>
/// Quy trình xóa an toàn (soft delete + hoàn tác trong 10 giây):
/// 1. Xóa mềm ngay lập tức → người dùng thấy thói quen biến mất khỏi danh sách.
/// 2. Thanh thông báo hiện nút "Hoàn tác" trong 10 giây.
/// 3. Nếu không hoàn tác → xóa hẳn khỏi CSDL.
/// Tuyệt đối không bao giờ mất dữ liệu khi người dùng chưa xác nhận.
/// </summary>
public sealed class SafeDeletionManager
{
    /// <summary>Thời gian cho phép hoàn tác.</summary>
    public static readonly TimeSpan UndoWindow = TimeSpan.FromSeconds(10);

    private readonly IHabitRepository _habits;
    private readonly IDeletionTimer _timer;

    private Habit? _pending;
    private IDisposable? _registration;

    public SafeDeletionManager(IHabitRepository habits, IDeletionTimer timer)
    {
        ArgumentNullException.ThrowIfNull(habits);
        ArgumentNullException.ThrowIfNull(timer);

        _habits = habits;
        _timer = timer;
    }

    /// <summary>Được gọi khi có một lần xóa đang chờ hoàn tác (để hiện toast).</summary>
    public event EventHandler<SafeDeletionInfo>? DeletionPending;

    /// <summary>Được gọi khi lần xóa bị hoàn tác.</summary>
    public event EventHandler<SafeDeletionInfo>? DeletionUndone;

    /// <summary>Được gọi khi lần xóa hoàn tất (xóa hẳn).</summary>
    public event EventHandler<SafeDeletionInfo>? DeletionCommitted;

    /// <summary>True nếu đang có một lần xóa chờ hoàn tác.</summary>
    public bool HasPendingDeletion => _pending is not null;

    /// <summary>Xóa mềm một thói quen và mở cửa sổ hoàn tác.</summary>
    public async Task RequestDeleteAsync(Habit habit)
    {
        ArgumentNullException.ThrowIfNull(habit);

        await _habits.SoftDeleteAsync(habit.Id);

        _pending = habit;
        _registration?.Dispose();
        _registration = _timer.Schedule(UndoWindow, () => _ = CommitAsync());

        DeletionPending?.Invoke(this, new SafeDeletionInfo(habit.Id, habit.Name));
    }

    /// <summary>Hoàn tác lần xóa đang chờ: khôi phục thói quen.</summary>
    public async Task UndoAsync()
    {
        if (_pending is null)
        {
            return;
        }

        var habit = _pending;

        ClearPending();

        habit.DeletedAt = null;
        await _habits.UpdateAsync(habit);

        DeletionUndone?.Invoke(this, new SafeDeletionInfo(habit.Id, habit.Name));
    }

    /// <summary>Xóa hẳn sau khi hết thời gian hoàn tác.</summary>
    public async Task CommitAsync()
    {
        if (_pending is null)
        {
            return;
        }

        var habit = _pending;

        ClearPending();

        await _habits.HardDeleteAsync(habit.Id);

        DeletionCommitted?.Invoke(this, new SafeDeletionInfo(habit.Id, habit.Name));
    }

    private void ClearPending()
    {
        _pending = null;
        _registration?.Dispose();
        _registration = null;
    }
}
