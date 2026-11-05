namespace HabitGrid.Core.Settings;

/// <summary>
/// Kiểu nền của widget trên desktop.
/// </summary>
public enum WidgetBackgroundStyle
{
    /// <summary>Acrylic — nền mờ trong suốt (mica/acrylic của Windows 11).</summary>
    Acrylic = 0,

    /// <summary>Đặc — nền đơn sắc.</summary>
    Solid = 1,

    /// <summary>Không nền — chỉ hiện các ô vuông.</summary>
    None = 2
}
