namespace HabitGrid.Platform.Licensing;

/// <summary>
/// Dịch vụ giấy phép mặc định / giả lập cho môi trường kiểm thử.
/// </summary>
public class FreeLicenseService : ILicenseService
{
    public const int FreeHabitLimit = 2;
    private bool _isPro;

    public FreeLicenseService(bool initialPro = false)
    {
        _isPro = initialPro;
    }

    public bool IsPro => _isPro;

    public int HabitLimit => _isPro ? int.MaxValue : FreeHabitLimit;

    public event EventHandler<bool>? LicenseChanged;

    public Task<bool> RefreshLicenseAsync() => Task.FromResult(_isPro);

    public Task<PurchaseResult> PurchaseProAsync(IntPtr windowHandle)
    {
        _isPro = true;
        LicenseChanged?.Invoke(this, true);
        return Task.FromResult(new PurchaseResult(true, "Mở khóa Pro thành công (Môi trường thử nghiệm)."));
    }

    public Task<bool> RestorePurchasesAsync() => Task.FromResult(_isPro);
}
