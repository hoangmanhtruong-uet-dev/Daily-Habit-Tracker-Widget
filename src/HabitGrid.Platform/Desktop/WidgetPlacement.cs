namespace HabitGrid.Platform.Desktop;

/// <summary>
/// Vị trí tương đối của widget trên màn hình (0.0 – 1.0 theo từng trục).
/// Lưu theo dạng này giúp widget không bị "bay" ra ngoài màn hình khi đổi độ phân giải hoặc DPI.
/// </summary>
public readonly record struct RelativePosition(double X, double Y)
{
    public static readonly RelativePosition Origin = new(0, 0);

    public static RelativePosition From(double x, double y) => new(Clamp01(x), Clamp01(y));

    private static double Clamp01(double value) => Math.Clamp(value, 0.0, 1.0);
}

/// <summary>
/// Toán học vị trí widget: chuyển đổi toạ độ tuyệt đối &lt;-&gt; tương đối, snap lưới 8px, giữ widget trong vùng làm việc.
/// Thuộc lớp thuần toán nên kiểm thử được không cần màn hình thật.
/// </summary>
public static class WidgetPlacement
{
    /// <summary>Bước lưới snap (px).</summary>
    public const int SnapGridSize = 8;

    /// <summary>Khoảng cách tối thiểu giữ mép widget và mép vùng làm việc (px).</summary>
    public const int EdgeMargin = 16;

    /// <summary>
    /// Chuyển vị trí tuyệt đối của widget trên màn hình sang toạ độ tương đối.
    /// Công thức: X_rel = (Left - Screen.Left) / (Screen.Width - Widget.Width).
    /// </summary>
    public static RelativePosition ToRelative(ScreenDescriptor screen, int left, int top, int widgetWidth, int widgetHeight)
    {
        ArgumentNullException.ThrowIfNull(screen);

        var spanX = Math.Max(1, screen.WorkArea.Width - widgetWidth);
        var spanY = Math.Max(1, screen.WorkArea.Height - widgetHeight);

        var x = (left - screen.WorkArea.Left) / (double)spanX;
        var y = (top - screen.WorkArea.Top) / (double)spanY;

        return RelativePosition.From(x, y);
    }

    /// <summary>
    /// Khôi phục vị trí tuyệt đối của widget từ toạ độ tương đối trên màn hình hiện tại.
    /// </summary>
    public static (int Left, int Top) FromRelative(ScreenDescriptor screen, RelativePosition position, int widgetWidth, int widgetHeight)
    {
        ArgumentNullException.ThrowIfNull(screen);

        var spanX = Math.Max(1, screen.WorkArea.Width - widgetWidth);
        var spanY = Math.Max(1, screen.WorkArea.Height - widgetHeight);

        var left = (int)Math.Round(screen.WorkArea.Left + (position.X * spanX), MidpointRounding.AwayFromZero);
        var top = (int)Math.Round(screen.WorkArea.Top + (position.Y * spanY), MidpointRounding.AwayFromZero);

        return (left, top);
    }

    /// <summary>
    /// Snap vị trí widget vào lưới 8px và giữ cho widget luôn nằm trong vùng làm việc của màn hình.
    /// </summary>
    public static (int Left, int Top) Snap(int left, int top, ScreenDescriptor screen, int widgetWidth, int widgetHeight)
    {
        ArgumentNullException.ThrowIfNull(screen);

        var snappedLeft = SnapToGrid(left, screen.WorkArea.Left);
        var snappedTop = SnapToGrid(top, screen.WorkArea.Top);

        return (ClampToWorkArea(snappedLeft, screen, widgetWidth, horizontal: true),
                ClampToWorkArea(snappedTop, screen, widgetHeight, horizontal: false));
    }

    /// <summary>
    /// Bám vào lưới 8px tính từ gốc của vùng làm việc.
    /// Khi đúng giữa hai mốc, làm tròn ra xa số 0 (đối xứng về hai phía).
    /// </summary>
    public static int SnapToGrid(int value, int origin)
    {
        var offset = value - origin;
        var snapped = (int)Math.Round(offset / (double)SnapGridSize, MidpointRounding.AwayFromZero) * SnapGridSize;

        return origin + snapped;
    }

    /// <summary>
    /// Giữ widget nằm trong vùng làm việc, chừa tối thiểu <see cref="EdgeMargin"/> px ở mép.
    /// Nếu vùng làm việc nhỏ hơn widget thì canh vào mép trên/trái.
    /// </summary>
    private static int ClampToWorkArea(int value, ScreenDescriptor screen, int size, bool horizontal)
    {
        var workArea = screen.WorkArea;
        var areaStart = horizontal ? workArea.Left : workArea.Top;
        var areaSize = horizontal ? workArea.Width : workArea.Height;

        // Không đủ chỗ để chừa cả hai mép -> canh sát mép đầu.
        if (areaSize <= size + (EdgeMargin * 2))
        {
            return areaStart;
        }

        var min = areaStart + EdgeMargin;
        var max = areaStart + areaSize - size - EdgeMargin;

        return Math.Clamp(value, min, max);
    }
}
