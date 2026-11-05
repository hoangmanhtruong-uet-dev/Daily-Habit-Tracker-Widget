# Gói Công Việc 07: Giấy Phép & Nâng Cấp Bản Pro (SPEC-07)

> **Mã công việc**: `SPEC-07-PRO`  
> **Thư viện mục tiêu**: `HabitGrid.Platform` & `HabitGrid.UI` (Windows.Services.Store, DPAPI, .NET 8)  
> **Mục tiêu**: Tích hợp hệ thống thanh toán Microsoft Store (In-App Purchase Durable Add-on), cơ chế mã hóa cache bản quyền ngoại tuyến (Offline Grace Period 30 ngày), xây dựng giao diện Paywall Modal chuyển đổi cao, và áp dụng quy tắc bảo vệ dữ liệu vĩnh viễn khi mất bản quyền.

---

## 1. MÔ HÌNH BẢN QUYỀN (PRICING & LICENSING MODEL)

| Tiêu chí | Bản Miễn Phí (Free) | Bản Nâng Cấp Pro (Mua đứt) |
|---|:---:|:---:|
| **Số lượng thói quen tối đa** | **2 thói quen** *(Tính cả thói quen đang theo dõi và đã lưu trữ)* | **Không giới hạn** |
| **Bảng màu Heatmap** | 1 bảng mặc định (GitHub Green) | Mọi Preset màu + Color Picker tùy biến mã Hex riêng |
| **Sao lưu & Xuất dữ liệu** | Thủ công (Nút "Mở thư mục dữ liệu" `%LocalAppData%`) | Tự động xuất/nhập file JSON & CSV đầy đủ |
| **Mọi tính năng còn lại** | Đầy đủ (Ghim desktop, nhắc nhở, dark theme, widget) | Đầy đủ |
| **Giá bán** | Miễn phí 100% | **4.99 USD** *(Khuyến mãi mở bán $3.99 trong 2 tuần đầu)* |
| **Hình thức giấy phép** | Không yêu cầu | Mua một lần sở hữu vĩnh viễn (Durable In-App Purchase) |

---

## 2. KỸ THUẬT TÍCH HỢP MICROSOFT STORE (`ILicenseService`)

```csharp
namespace HabitGrid.Platform.Licensing;

public interface ILicenseService
{
    Task<bool> IsProActiveAsync();
    Task<PurchaseResult> PurchaseProAsync(IntPtr windowHandle);
    Task<bool> RestorePurchasesAsync();
    event EventHandler<bool>? LicenseStatusChanged;
}
```

### 2.1 Cấu hình Add-on trên Partner Center
- Loại sản phẩm: **Durable (Bền vững)**.
- Mã định danh Add-on: `pro_unlock`.
- Không thiết lập thời hạn hết hạn (Lifetime).

### 2.2 Gọi API Mua hàng với HWND
Ứng dụng Win32/WPF đóng gói MSIX yêu cầu khởi tạo `StoreContext` gắn với cửa sổ hiện hành qua interface `IInitializeWithWindow`:

```csharp
var storeContext = StoreContext.GetDefault();
var initWindow = (IInitializeWithWindow)(object)storeContext;
initWindow.Initialize(windowHandle);

StorePurchaseResult result = await storeContext.RequestPurchaseAsync("pro_unlock");
```

### 2.3 Cơ chế Cache Ngoại Tuyến (Offline DPAPI Cache)
Để người dùng dùng được bản Pro ngay cả khi không có mạng Internet (máy bay, mất mạng, đi công tác):
- Khi xác thực Store thành công: Lưu trạng thái Pro kèm chữ ký mã hóa bằng **Windows DPAPI** (`ProtectedData.Protect` với phạm vi `DataProtectionScope.CurrentUser`) vào registry hoặc file cache bảo mật:
  `%LocalAppData%\HabitGrid\.license.dat`.
- **Thời hạn ngoại tuyến tối đa (Grace Period)**: **30 ngày**. Nếu sau 30 ngày app không kết nối được với Store để làm mới token bản quyền, tạm thời đưa về trạng thái Free cho đến khi có mạng trở lại.
- Khi có kết nối mạng: Tiến trình nền âm thầm gọi `GetAppLicenseAsync()` để gia hạn cache mà không làm phiền người dùng.

---

## 3. QUY TẮC BẢO VỆ DỮ LIỆU KHI HẠ CẤP (NEVER DELETE USER DATA)

Nếu vì bất kỳ lý do gì người dùng bị mất Pro (hoàn tiền trên Store, khiếu nại thẻ, hoặc hết hạn cache ngoại tuyến 30 ngày) trong khi trong CSDL đang có 5 thói quen:
1. **Tuyệt đối KHÔNG xóa bất kỳ thói quen hay lịch sử nào** trong CSDL.
2. Hai thói quen đầu tiên (xác định theo `sort_order` nhỏ nhất) tiếp tục hoạt động bình thường, người dùng vẫn đánh dấu và sửa đổi được.
3. Ba thói quen còn lại chuyển sang trạng thái **Chỉ đọc (Read-Only)**:
   - Trên bảng điều khiển: Thẻ thói quen hiển thị huy hiệu ổ khóa mờ kèm nhãn `"Chỉ đọc (Cần Pro)"`.
   - Trên desktop: Widget phủ một lớp mờ nhẹ (`Opacity = 0.5`) kèm biểu tượng ổ khóa nhỏ ở góc.
   - Khi click vào widget hoặc thói quen bị khóa: Mở Paywall modal giải thích lý do.
4. Ngay khi người dùng mua lại hoặc bấm "Khôi phục giao dịch", toàn bộ các thói quen bị khóa lập tức kích hoạt lại bình thường với dữ liệu nguyên vẹn 100%.

---

## 4. GIAO DIỆN MÀN HÌNH NÂNG CẤP PRO (`ProUpgradeDialog.xaml`)

Giao diện được thiết kế hiện đại, tinh tế, mang lại cảm giác cao cấp và tin cậy:

```
┌────────────────────────────────────────────────────────┐
│                      HabitGrid Pro                     │
│          Mở khóa toàn bộ sức mạnh cho desktop          │
├────────────────────────────────────────────────────────┤
│  [★] Không giới hạn thói quen                          │
│      Theo dõi 5, 10 hay 20 mục tiêu cùng lúc trên nền. │
│                                                        │
│  [🎨] Tùy biến màu sắc Heatmap                         │
│      Preset màu sắc đa dạng hoặc tự do chọn mã Hex.   │
│                                                        │
│  [💾] Sao lưu & Xuất dữ liệu tức thì                   │
│      Xuất JSON/CSV tự động, bảo vệ lịch sử của bạn.    │
├────────────────────────────────────────────────────────┤
│           [ Minh họa Heatmap đa sắc màu ]              │
├────────────────────────────────────────────────────────┤
│          4.99 USD • Mua một lần, dùng mãi mãi          │
│                                                        │
│             [   Mở khóa Pro ngay   ]                   │
│               [Khôi phục giao dịch]                    │
│                                                        │
│     🔒 Thanh toán bảo mật qua Microsoft Store         │
└────────────────────────────────────────────────────────┘
```

**Các trạng thái tương tác của Modal**:
- **Sẵn sàng (Ready)**: Hiển thị giá lấy trực tiếp từ Microsoft Store theo đơn vị tiền tệ địa phương (ví dụ: `119.000 ₫` hoặc `$4.99`).
- **Đang xử lý (Purchasing)**: Nút mua chuyển thành ProgressRing quay tròn, vô hiệu hóa các nút khác.
- **Thành công (Success)**: Phát hiệu ứng chúc mừng nhẹ nhàng (Confetti), thông báo *"Cảm ơn bạn đã ủng hộ nhà phát triển!"* và tự động đóng modal sau 1.5 giây, mở khóa ngay các tính năng Pro.
- **Hủy bỏ (Cancelled)**: Người dùng đóng cửa sổ thanh toán của Store -> trở lại màn hình bình thường, không báo lỗi.
- **Lỗi kết nối (Network Error)**: Hiển thị thông báo nhỏ: *"Không thể kết nối với Microsoft Store. Vui lòng kiểm tra lại mạng Internet."*.

---

## 5. TIÊU CHUẨN NGHIỆM THU (ACCEPTANCE CRITERIA)
- [ ] Khi ở bản Free, tạo thói quen thứ 3 (hoặc đã có 2 thói quen bao gồm cả thói quen lưu trữ) thì nút thêm mới kích hoạt mở Paywall modal 100%.
- [ ] Mua thử nghiệm qua Store Simulator / Fake License Service chuyển đổi trạng thái Free sang Pro ngay lập tức mà không cần khởi động lại ứng dụng.
- [ ] Bật chế độ Airplane Mode (ngắt mạng), khởi động lại máy tính, trạng thái Pro vẫn duy trì nhờ cache mã hóa DPAPI.
- [ ] Thử nghiệm kịch bản hoàn tiền (Revoke License): CSDL vẫn nguyên vẹn 100%, các thói quen dư thừa chuyển sang Read-only chính xác theo quy định.
