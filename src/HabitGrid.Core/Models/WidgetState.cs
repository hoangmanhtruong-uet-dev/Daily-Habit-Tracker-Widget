namespace HabitGrid.Core.Models;

/// <summary>
/// Trạng thái hiển thị của widget trên desktop cho một thói quen.
/// Vị trí được lưu dạng tương đối (0..1) theo màn hình để không bị "bay" ra ngoài
/// khi người dùng đổi độ phân giải, DPI hoặc rút màn hình phụ.
/// </summary>
public class WidgetState
{
    public Guid HabitId { get; init; }

    /// <summary>Định danh màn hình (DisplayDevice path), ví dụ "MONITOR\DELL\...".</summary>
    public string Monitor { get; set; } = string.Empty;

    /// <summary>Toạ độ X tương đối trong vùng làm việc (0..1).</summary>
    public double X { get; set; }

    /// <summary>Toạ độ Y tương đối trong vùng làm việc (0..1).</summary>
    public double Y { get; set; }

    public WidgetSize Size { get; set; } = WidgetSize.Medium;

    /// <summary>True nếu widget đang hiển thị.</summary>
    public bool Visible { get; set; } = true;
}
