using System.Windows;
using System.Windows.Interop;
using HabitGrid.Platform.Licensing;

namespace HabitGrid.UI.Views;

/// <summary>
/// Màn hình nâng cấp Pro (paywall).
/// Tích hợp trực tiếp với Microsoft Store In-App Purchase qua ILicenseService.
/// </summary>
public partial class PaywallDialog : Window
{
    private readonly ILicenseService? _licenseService;

    public PaywallDialog(string? reason = null) : this(null, reason)
    {
    }

    public PaywallDialog(ILicenseService? licenseService, string? reason = null)
    {
        InitializeComponent();
        _licenseService = licenseService;

        if (!string.IsNullOrWhiteSpace(reason))
        {
            ReasonText.Text = reason;
        }
    }

    /// <summary>True nếu người dùng đã nâng cấp thành công.</summary>
    public bool Purchased { get; private set; }

    private async void OnBuyClick(object sender, RoutedEventArgs e)
    {
        if (_licenseService != null)
        {
            StatusText.Text = "Đang kết nối tới Microsoft Store...";
            var handle = new WindowInteropHelper(this).Handle;
            var result = await _licenseService.PurchaseProAsync(handle);

            if (result.Success)
            {
                Purchased = true;
                StatusText.Text = result.Message;
                await Task.Delay(800);
                DialogResult = true;
                Close();
            }
            else
            {
                StatusText.Text = result.Message;
            }
        }
        else
        {
            Purchased = true;
            DialogResult = true;
            Close();
        }
    }

    private async void OnRestoreClick(object sender, RoutedEventArgs e)
    {
        if (_licenseService != null)
        {
            StatusText.Text = "Đang kiểm tra lại giao dịch trên Microsoft Store...";
            var restored = await _licenseService.RestorePurchasesAsync();
            if (restored)
            {
                Purchased = true;
                StatusText.Text = "Khôi phục thành công! Đã kích hoạt bản Pro vĩnh viễn.";
                await Task.Delay(800);
                DialogResult = true;
                Close();
            }
            else
            {
                StatusText.Text = "Không tìm thấy giao dịch mua Pro hợp lệ trên tài khoản này.";
            }
        }
        else
        {
            StatusText.Text = "Chức năng khôi phục chỉ khả dụng khi kết nối Microsoft Store.";
        }
    }
}
