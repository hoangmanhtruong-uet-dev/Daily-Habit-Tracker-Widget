using HabitGrid.Core.Models;

namespace HabitGrid.Data.Backup;

/// <summary>
/// Dữ liệu một bản sao lưu đầy đủ.
/// </summary>
/// <param name="App">Tên ứng dụng ("HabitGrid").</param>
/// <param name="SchemaVersion">Phiên bản lược đồ của file sao lưu.</param>
/// <param name="ExportedAt">Thời điểm xuất bản sao lưu.</param>
/// <param name="Habits">Danh sách thói quen.</param>
/// <param name="Entries">Danh sách nhật ký theo ngày.</param>
/// <param name="Settings">Cài đặt ứng dụng.</param>
public record BackupData(
    string App,
    int SchemaVersion,
    DateTime ExportedAt,
    IReadOnlyList<Habit> Habits,
    IReadOnlyList<Entry> Entries,
    IReadOnlyDictionary<string, string> Settings);

/// <summary>
/// Chế độ nạp dữ liệu từ bản sao lưu.
/// </summary>
public enum ImportMode
{
    /// <summary>Giữ nguyên dữ liệu hiện tại, gộp thêm dữ liệu mới. Trùng ngày thì lấy giá trị lớn hơn.</summary>
    Merge = 0,

    /// <summary>Xóa toàn bộ dữ liệu hiện tại và nạp dữ liệu từ file sao lưu.</summary>
    Replace = 1
}
