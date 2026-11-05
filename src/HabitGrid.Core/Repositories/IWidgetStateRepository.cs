using HabitGrid.Core.Models;

namespace HabitGrid.Core.Repositories;

/// <summary>
/// Truy cập trạng thái hiển thị của widget trên desktop (vị trí, cỡ, ẩn/hiện).
/// </summary>
public interface IWidgetStateRepository
{
    /// <summary>Lấy toàn bộ trạng thái widget.</summary>
    Task<IReadOnlyList<WidgetState>> GetAllAsync();

    /// <summary>Lấy trạng thái widget của một thói quen. Trả về null nếu chưa có.</summary>
    Task<WidgetState?> GetAsync(Guid habitId);

    /// <summary>Ghi đè nếu đã tồn tại, nếu không thì tạo mới.</summary>
    Task UpsertAsync(WidgetState state);

    /// <summary>Đặt trạng thái ẩn/hiện của widget.</summary>
    Task SetVisibleAsync(Guid habitId, bool visible);

    /// <summary>Xóa trạng thái widget (dùng khi xóa thói quen).</summary>
    Task DeleteAsync(Guid habitId);
}
