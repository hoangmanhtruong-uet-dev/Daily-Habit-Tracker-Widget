using HabitGrid.Core.Models;

namespace HabitGrid.UI.Services;

/// <summary>
/// Kết quả chỉnh sửa thói quen trả về từ hộp thoại.
/// </summary>
public sealed class HabitEditResult
{
    public string Name { get; init; } = string.Empty;

    public string IconKey { get; init; } = Theme.IconCatalog.DefaultKey;

    public HabitKind Kind { get; init; } = HabitKind.Boolean;

    public int Target { get; init; } = 1;

    public DayOfWeekMask ScheduleMask { get; init; } = DayOfWeekMask.AllWeek;

    public string PaletteId { get; init; } = "default";

    public string? CustomColorHex { get; init; }

    public bool ReminderEnabled { get; init; }

    public TimeOnly? ReminderTime { get; init; }
}

/// <summary>
/// Lựa chọn trong luồng làm quen ban đầu.
/// </summary>
/// <param name="Name">Tên thói quen.</param>
/// <param name="IconKey">Khóa icon.</param>
/// <param name="Kind">Loại thói quen.</param>
/// <param name="Target">Mục tiêu mỗi ngày (với thói quen đếm số lượng).</param>
public sealed record OnboardingChoice(string Name, string IconKey, HabitKind Kind, int Target);

/// <summary>
/// Các hộp thoại mà ViewModel cần mở.
/// ViewModel không gọi trực tiếp cửa sổ WPF mà thông qua interface này để dễ kiểm thử.
/// </summary>
public interface IDialogService
{
    /// <summary>Mở hộp thoại tạo/sửa thói quen. Trả về null nếu người dùng hủy.</summary>
    Task<HabitEditResult?> ShowEditHabitAsync(Habit? existing);

    /// <summary>Mở màn hình nâng cấp Pro. Trả về true nếu người dùng đã nâng cấp.</summary>
    Task<bool> ShowPaywallAsync(string? reason);

    /// <summary>Mở luồng làm quen ban đầu. Trả về null nếu người dùng bỏ qua.</summary>
    Task<OnboardingChoice?> ShowOnboardingAsync();

    /// <summary>Hiện thanh thông báo nhỏ ở đáy màn hình.</summary>
    void ShowToast(string message, string? actionText = null, Action? action = null);
}
