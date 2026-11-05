namespace HabitGrid.Platform.Desktop;

/// <summary>
/// Chế độ ghim cửa sổ widget lên desktop.
/// </summary>
public enum DesktopPinningMode
{
    /// <summary>Chế độ 1: nằm trên hình nền, dưới mọi cửa sổ (WorkerW).</summary>
    DesktopLevel = 0,

    /// <summary>Chế độ 2: cửa sổ trôi xuống đáy z-order (dự phòng ổn định).</summary>
    BottomMost = 1,

    /// <summary>Chế độ 3: luôn nằm trên cùng.</summary>
    AlwaysOnTop = 2
}
