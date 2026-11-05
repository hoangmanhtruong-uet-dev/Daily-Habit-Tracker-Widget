namespace HabitGrid.Platform.Licensing;

public record PurchaseResult(bool Success, string Message);

/// <summary>
/// Trạng thái và dịch vụ quản lý giấy phép bản quyền của ứng dụng.
/// </summary>
public interface ILicenseService
{
    /// <summary>True nếu người dùng đã mua bản Pro vĩnh viễn.</summary>
    bool IsPro { get; }

    /// <summary>Số thói quen tối đa được phép tạo (Free = 2, Pro = int.MaxValue).</summary>
    int HabitLimit { get; }

    /// <summary>Được gọi khi trạng thái giấy phép thay đổi (mua/khôi phục/hạ cấp).</summary>
    event EventHandler<bool>? LicenseChanged;

    /// <summary>Làm mới và xác thực lại giấy phép từ cache/Store.</summary>
    Task<bool> RefreshLicenseAsync();

    /// <summary>Yêu cầu mua gói Pro qua Microsoft Store gắn với HWND cửa sổ.</summary>
    Task<PurchaseResult> PurchaseProAsync(IntPtr windowHandle);

    /// <summary>Khôi phục lại giao dịch đã mua trước đó.</summary>
    Task<bool> RestorePurchasesAsync();
}
