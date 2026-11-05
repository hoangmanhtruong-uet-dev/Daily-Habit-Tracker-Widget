namespace HabitGrid.Platform.System;

/// <summary>
/// Quản lý ứng dụng đơn thực thể (Single-instance) bằng Mutex và Named Pipe.
/// </summary>
public interface ISingleInstanceService : IDisposable
{
    /// <summary>Được gọi khi một instance khác gửi tham số tới tiến trình chính.</summary>
    event EventHandler<string[]>? ArgumentsReceived;

    /// <summary>
    /// Thử lấy Mutex ứng dụng.
    /// Trả về <c>true</c> nếu đây là instance đầu tiên (Server).
    /// Trả về <c>false</c> nếu đã có instance khác đang chạy (Client).
    /// </summary>
    bool TryAcquire();

    /// <summary>
    /// Gửi tham số tới instance đang chạy và đợi phản hồi ngắn.
    /// </summary>
    Task<bool> NotifyExistingInstanceAsync(string[] args, CancellationToken cancellationToken = default);
}
