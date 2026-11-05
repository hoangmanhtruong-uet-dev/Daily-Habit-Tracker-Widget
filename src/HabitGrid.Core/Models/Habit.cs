using System.Text.Json.Serialization;

namespace HabitGrid.Core.Models;

/// <summary>
/// Một thói quen được theo dõi. Thuộc tầng Domain: không phụ thuộc WPF, SQL hay bất kỳ hạ tầng nào.
/// </summary>
public class Habit
{
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Tên thói quen, độ dài 1..40 ký tự.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Khóa icon Fluent, ví dụ "Checkmark24Regular".</summary>
    public string Icon { get; set; } = "Checkmark24Regular";

    public HabitKind Kind { get; set; } = HabitKind.Boolean;

    /// <summary>Mục tiêu mỗi ngày, chỉ áp dụng cho <see cref="HabitKind.Count"/>.</summary>
    public int Target { get; set; } = 1;

    /// <summary>Lịch lặp trong tuần. Mặc định cả 7 ngày.</summary>
    public DayOfWeekMask ScheduleMask { get; set; } = DayOfWeekMask.AllWeek;

    /// <summary>Định danh bảng màu preset ("default", "blue", "purple", ...).</summary>
    public string PaletteId { get; set; } = "default";

    /// <summary>Mã màu gốc #RRGGBB do người dùng tự chọn (chỉ dành cho Pro).</summary>
    public string? CustomColorHex { get; set; }

    /// <summary>Ngày bắt đầu theo dõi. Các ngày trước đây không được tính vào streak.</summary>
    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Now);

    /// <summary>Giờ nhắc nhở trong ngày (tùy chọn).</summary>
    public TimeOnly? ReminderTime { get; set; }

    /// <summary>Các thứ trong tuần được nhắc nhở.</summary>
    public DayOfWeekMask ReminderMask { get; set; } = DayOfWeekMask.AllWeek;

    public int SortOrder { get; set; }

    /// <summary>Thời điểm lưu trữ (archive). Null nghĩa là chưa lưu trữ.</summary>
    public DateTime? ArchivedAt { get; set; }

    /// <summary>Thời điểm xóa mềm (soft delete). Null nghĩa là chưa bị xóa.</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// Thói quen bị khóa chỉ đọc vì vượt hạn mức gói Free (2 thói quen).
    /// Dữ liệu vẫn được giữ nguyên, chỉ khoá thao tác chỉnh sửa.
    /// </summary>
    public bool IsReadOnly { get; set; }

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>Thói quen đang hoạt động (không bị lưu trữ và không bị xóa).</summary>
    [JsonIgnore]
    public bool IsActive => DeletedAt is null && ArchivedAt is null;

    /// <summary>Thói quen đã lưu trữ nhưng chưa bị xóa.</summary>
    [JsonIgnore]
    public bool IsArchived => ArchivedAt is not null && DeletedAt is null;
}
