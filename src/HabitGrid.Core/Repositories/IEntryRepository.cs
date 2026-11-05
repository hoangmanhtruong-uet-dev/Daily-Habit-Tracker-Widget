using HabitGrid.Core.Models;

namespace HabitGrid.Core.Repositories;

/// <summary>
/// Truy cập dữ liệu nhật ký hoàn thành theo ngày.
/// </summary>
public interface IEntryRepository
{
    /// <summary>Lấy bản ghi của một ngày cụ thể. Trả về null nếu chưa có dữ liệu.</summary>
    Task<Entry?> GetAsync(Guid habitId, DateOnly day);

    /// <summary>Lấy các bản ghi trong khoảng [fromDay, toDay], theo thứ tự ngày tăng dần.</summary>
    Task<IReadOnlyList<Entry>> GetRangeAsync(Guid habitId, DateOnly fromDay, DateOnly toDay);

    /// <summary>Lấy toàn bộ bản ghi của một thói quen, theo thứ tự ngày tăng dần.</summary>
    Task<IReadOnlyList<Entry>> GetAllForHabitAsync(Guid habitId);

    /// <summary>Ghi đè nếu đã tồn tại, nếu không thì tạo mới.</summary>
    Task UpsertAsync(Entry entry);

    /// <summary>Xóa bản ghi của một ngày.</summary>
    Task DeleteAsync(Guid habitId, DateOnly day);
}
