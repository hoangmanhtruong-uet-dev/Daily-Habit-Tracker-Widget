using HabitGrid.Core.Models;

namespace HabitGrid.Core.Repositories;

/// <summary>
/// Truy cập dữ liệu thói quen.
/// </summary>
public interface IHabitRepository
{
    /// <summary>Lấy các thói quen đang hoạt động (chưa lưu trữ, chưa xóa), theo thứ tự hiển thị.</summary>
    Task<IReadOnlyList<Habit>> GetAllActiveAsync();

    /// <summary>Lấy cả thói quen đã lưu trữ (chưa xóa), theo thứ tự hiển thị.</summary>
    Task<IReadOnlyList<Habit>> GetAllIncludingArchivedAsync();

    /// <summary>Lấy một thói quen theo định danh. Trả về null nếu không tồn tại.</summary>
    Task<Habit?> GetByIdAsync(Guid id);

    /// <summary>Thêm mới một thói quen.</summary>
    Task InsertAsync(Habit habit);

    /// <summary>Cập nhật thông tin của một thói quen.</summary>
    Task UpdateAsync(Habit habit);

    /// <summary>Xóa mềm: giữ dữ liệu, chỉ đánh dấu đã xóa (hỗ trợ hoàn tác).</summary>
    Task SoftDeleteAsync(Guid id);

    /// <summary>Xóa hẳn, kèm xóa các bản ghi và trạng thái widget liên quan.</summary>
    Task HardDeleteAsync(Guid id);

    /// <summary>Lưu trữ thói quen (ẩn khỏi danh sách chính nhưng giữ dữ liệu).</summary>
    Task ArchiveAsync(Guid id);

    /// <summary>Bỏ lưu trữ thói quen.</summary>
    Task UnarchiveAsync(Guid id);

    /// <summary>Cập nhật thứ tự hiển thị của nhiều thói quen trong một lần ghi.</summary>
    Task UpdateSortOrdersAsync(IReadOnlyList<(Guid HabitId, int SortOrder)> orders);

    /// <summary>
    /// Đếm số thói quen chưa bị xóa (Active + Archived).
    /// Dùng để kiểm tra hạn mức gói Free.
    /// </summary>
    Task<int> CountNonDeletedAsync();
}
