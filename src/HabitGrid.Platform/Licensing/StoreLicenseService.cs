using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Windows.Services.Store;

namespace HabitGrid.Platform.Licensing;

[ComImport]
[Guid("3E68D4BD-7135-4D10-8018-9FB6D9F33FA1")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IInitializeWithWindow
{
    void Initialize(IntPtr hwnd);
}

/// <summary>
/// Dữ liệu cache bản quyền mã hóa DPAPI.
/// </summary>
public record LicenseCacheEntry(bool IsPro, DateTime VerifiedAtUtc, DateTime ExpiryUtc);

/// <summary>
/// Hiện thực hoá quản lý giấy phép bản quyền qua Microsoft Store (WinRT StoreContext)
/// và bộ nhớ đệm ngoại tuyến mã hóa bảo mật DPAPI (30 ngày offline).
/// </summary>
public sealed class StoreLicenseService : ILicenseService
{
    public const string ProAddOnStoreId = "pro_unlock";
    public const int FreeHabitLimit = 2;
    private static readonly TimeSpan OfflineGracePeriod = TimeSpan.FromDays(30);
    private static readonly byte[] Entropy = "HabitGrid_Secure_Lic_Salt"u8.ToArray();

    private readonly string _cacheFilePath;
    private bool _isPro;
    private StoreContext? _storeContext;

    public StoreLicenseService(string? customCachePath = null)
    {
        _cacheFilePath = customCachePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HabitGrid",
            ".license.dat");

        // Đọc cache ngoại tuyến lúc khởi động
        _isPro = TryReadOfflineCache();
    }

    public bool IsPro => _isPro;

    public int HabitLimit => _isPro ? int.MaxValue : FreeHabitLimit;

    public event EventHandler<bool>? LicenseChanged;

    public async Task<bool> RefreshLicenseAsync()
    {
        if (!IsPackagedProcess())
        {
            return _isPro;
        }

        try
        {
            _storeContext ??= StoreContext.GetDefault();
            var appLicense = await _storeContext.GetAppLicenseAsync();

            var hasProAddon = appLicense.AddOnLicenses.TryGetValue(ProAddOnStoreId, out var addOn) && addOn.IsActive;

            UpdateLicenseState(hasProAddon);
            return _isPro;
        }
        catch
        {
            // Nếu không có mạng hoặc lỗi gọi Store: giữ nguyên trạng thái từ cache DPAPI
            return _isPro;
        }
    }

    public async Task<PurchaseResult> PurchaseProAsync(IntPtr windowHandle)
    {
        if (!IsPackagedProcess())
        {
            // Trong môi trường dev unpackaged, kích hoạt giả lập để test luồng
            UpdateLicenseState(true);
            return new PurchaseResult(true, "Đã mở khóa Pro thành công (Môi trường Dev).");
        }

        try
        {
            _storeContext ??= StoreContext.GetDefault();

            if (windowHandle != IntPtr.Zero)
            {
                var initWindow = (IInitializeWithWindow)(object)_storeContext;
                initWindow.Initialize(windowHandle);
            }

            var result = await _storeContext.RequestPurchaseAsync(ProAddOnStoreId);

            switch (result.Status)
            {
                case StorePurchaseStatus.Succeeded:
                case StorePurchaseStatus.AlreadyPurchased:
                    UpdateLicenseState(true);
                    return new PurchaseResult(true, "Giao dịch thành công! Đã mở khóa bản Pro vĩnh viễn.");

                case StorePurchaseStatus.NotPurchased:
                    return new PurchaseResult(false, "Giao dịch không được hoàn tất hoặc đã hủy.");

                case StorePurchaseStatus.NetworkError:
                    return new PurchaseResult(false, "Lỗi kết nối tới Microsoft Store. Vui lòng kiểm tra lại mạng.");

                default:
                    return new PurchaseResult(false, $"Không thể hoàn tất giao dịch: {result.ExtendedError?.Message ?? "Lỗi không xác định"}");
            }
        }
        catch (Exception ex)
        {
            return new PurchaseResult(false, $"Lỗi xử lý giao dịch: {ex.Message}");
        }
    }

    public async Task<bool> RestorePurchasesAsync()
    {
        return await RefreshLicenseAsync();
    }

    private void UpdateLicenseState(bool isPro)
    {
        var changed = _isPro != isPro;
        _isPro = isPro;

        SaveOfflineToCache(isPro);

        if (changed)
        {
            LicenseChanged?.Invoke(this, _isPro);
        }
    }

    private void SaveOfflineToCache(bool isPro)
    {
        try
        {
            var folder = Path.GetDirectoryName(_cacheFilePath);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var entry = new LicenseCacheEntry(
                IsPro: isPro,
                VerifiedAtUtc: DateTime.UtcNow,
                ExpiryUtc: DateTime.UtcNow.Add(OfflineGracePeriod)
            );

            var json = JsonSerializer.Serialize(entry);
            var plainBytes = Encoding.UTF8.GetBytes(json);
            var encrypted = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.CurrentUser);

            File.WriteAllBytes(_cacheFilePath, encrypted);
        }
        catch
        {
            // Ghi cache thất bại không làm crash ứng dụng
        }
    }

    private bool TryReadOfflineCache()
    {
        try
        {
            if (!File.Exists(_cacheFilePath))
            {
                return false;
            }

            var encrypted = File.ReadAllBytes(_cacheFilePath);
            var plainBytes = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
            var json = Encoding.UTF8.GetString(plainBytes);

            var entry = JsonSerializer.Deserialize<LicenseCacheEntry>(json);
            if (entry != null && entry.IsPro)
            {
                // Kiểm tra thời hạn 30 ngày ngoại tuyến
                if (DateTime.UtcNow <= entry.ExpiryUtc)
                {
                    return true;
                }
            }
        }
        catch
        {
            // Cache lỗi hoặc không giải mã được
        }

        return false;
    }

    private static bool IsPackagedProcess()
    {
        try
        {
            _ = Windows.ApplicationModel.Package.Current.Id;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
