namespace HabitGrid.Data.Backup;

/// <summary>
/// Dịch vụ xuất/nhập bản sao lưu.
/// </summary>
public interface IBackupService
{
    /// <summary>Xuất toàn bộ dữ liệu ra chuỗi JSON.</summary>
    Task<string> ExportJsonAsync();

    /// <summary>Xuất dữ liệu của một thói quen ra CSV. Định dạng: Date,Value,Status.</summary>
    Task<byte[]> ExportCsvForHabitAsync(Guid habitId);

    /// <summary>Nạp dữ liệu từ chuỗi JSON theo chế độ đã chọn.</summary>
    Task ImportJsonAsync(string jsonContent, ImportMode mode, bool isProUser);
}
