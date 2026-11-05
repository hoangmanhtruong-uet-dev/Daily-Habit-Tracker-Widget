namespace HabitGrid.Platform.Desktop;

/// <summary>
/// Kết quả của một lần thử ghim cửa sổ vào desktop.
/// </summary>
/// <param name="Success">True nếu cửa sổ đã được ghim thành công.</param>
/// <param name="AppliedMode">Chế độ ghim thực sự đã áp dụng (có thể khác chế độ yêu cầu do fallback).</param>
/// <param name="RequestedMode">Chế độ được yêu cầu.</param>
/// <param name="Message">Thông báo giải thích khi có fallback hoặc lỗi.</param>
public sealed record DesktopHostResult(
    bool Success,
    DesktopPinningMode AppliedMode,
    DesktopPinningMode RequestedMode,
    string? Message)
{
    /// <summary>True nếu ứng dụng phải chuyển sang chế độ dự phòng.</summary>
    public bool UsedFallback => AppliedMode != RequestedMode;

    public static DesktopHostResult Ok(DesktopPinningMode mode) => new(true, mode, mode, null);

    public static DesktopHostResult Fallback(DesktopPinningMode requested, DesktopPinningMode applied, string message) =>
        new(true, applied, requested, message);

    public static DesktopHostResult Failed(DesktopPinningMode requested, string message) =>
        new(false, requested, requested, message);
}
