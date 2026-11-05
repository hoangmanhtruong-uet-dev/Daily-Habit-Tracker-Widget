namespace HabitGrid.Core.Repositories;

/// <summary>
/// Truy cập bảng cài đặt ứng dụng (key/value).
/// </summary>
public interface ISettingsRepository
{
    /// <summary>Lấy giá trị thô của một khóa. Trả về null nếu khóa không tồn tại.</summary>
    Task<string?> GetAsync(string key);

    /// <summary>Lấy giá trị của một khóa đã chuyển đổi kiểu. Trả về <paramref name="defaultValue"/> nếu không có hoặc dữ liệu lỗi.</summary>
    Task<T> GetAsync<T>(string key, T defaultValue);

    /// <summary>Ghi đè giá trị của một khóa.</summary>
    Task SetAsync<T>(string key, T value);
}
