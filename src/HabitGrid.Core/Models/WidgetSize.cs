namespace HabitGrid.Core.Models;

/// <summary>
/// Cỡ hiển thị của widget trên desktop.
/// Thuộc tầng Domain vì được lưu trong CSDL (widget_state.size) và dùng chung cho nhiều tầng.
/// </summary>
public enum WidgetSize
{
    /// <summary>Nhỏ: 13 tuần (~3 tháng).</summary>
    Small = 0,

    /// <summary>Vừa: 26 tuần (~6 tháng).</summary>
    Medium = 1,

    /// <summary>Lớn: 52 tuần (~1 năm).</summary>
    Large = 2
}
