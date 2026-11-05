# Gói Công Việc 06: Tích Hợp Hệ Sinh Thái Windows (SPEC-06)

> **Mã công việc**: `SPEC-06-SYSTEM`  
> **Thư viện mục tiêu**: `HabitGrid.Platform` & `HabitGrid.App` (.NET 8, Win32 Interop, Windows Runtime APIs)  
> **Mục tiêu**: Xây dựng kiến trúc ứng dụng chạy nền đơn thực thể (Single-instance Mutex & IPC), biểu tượng khay hệ thống (System Tray), thông báo tương tác nhanh Windows Toast Notifications, tự động khởi động cùng Windows (StartupTask), và xử lý mốc chuyển ngày khi máy tính thức dậy từ chế độ ngủ (Sleep/Resume).

---

## 1. KIẾN TRÚC ĐƠN THỰC THỂ (SINGLE INSTANCE & IPC)

Ứng dụng widget desktop yêu cầu chỉ có duy nhất một tiến trình chạy nền:
1. **Mutex toàn cục**: Khởi tạo với tên `Global\HabitGrid_SingleInstanceMutex_{Hash}`.
   - Nếu Mutex đã tồn tại: Tiến trình thứ 2 đóng vai trò là Client, gửi thông điệp mở cửa sổ qua **Named Pipe** (`HabitGrid_IPC_Pipe`) tới tiến trình chính rồi tự thoát ngay lập tức.
2. **Tiến trình chính (Server)**:
   - Khi nhận được thông điệp từ Named Pipe: Tự động kích hoạt (BringToFront) cửa sổ Quản lý trung tâm và đưa tiêu điểm (Focus) lên trên màn hình.
   - Khi người dùng bấm nút đóng `[X]` của cửa sổ Quản lý: **Không thoát app**, chỉ ẩn cửa sổ (`Visibility = Hidden`) và thu về khay hệ thống để giữ các widget ngoài desktop luôn hoạt động.

---

## 2. BIỂU TƯỢNG KHAY HỆ THỐNG (SYSTEM TRAY NOTIFYICON)

Tích hợp NotifyIcon trong thanh Taskbar với icon Fluent chất lượng cao:
- **Click chuột trái**: Mở / Kích hoạt Bảng điều khiển quản lý.
- **Menu chuột phải (Context Menu)**:
  - 📌 **Mở bảng quản lý** *(Chữ đậm - Hành động mặc định)*
  - ✏️ **Chế độ chỉnh sửa Widget** *(Checkbox kiểm tra trạng thái)*
  - 👁️ **Hiện / Ẩn tất cả Widget**
  - ───────────────
  - ⚙️ **Cài đặt**
  - ───────────────
  - 🚪 **Thoát hoàn toàn** *(Giải phóng Mutex, lưu lại toàn bộ tọa độ widget, đóng kết nối DB sạch sẽ và thoát tiến trình)*

---

## 3. THÔNG BÁO TƯƠNG TÁC WINDOWS (ACTIONABLE TOAST NOTIFICATIONS)

Sử dụng thư viện `Microsoft.Toolkit.Uwp.Notifications` để tạo các thông báo chuẩn Windows 10/11 có nút hành động nhanh:

```xml
<toast scenario="reminder">
  <visual>
    <binding template="ToastGeneric">
      <text>Đến giờ đọc sách rồi! 📖</text>
      <text>Hôm nay bạn chưa ghi nhận thói quen Đọc sách. Hãy giữ vững chuỗi 14 ngày nhé!</text>
    </binding>
  </visual>
  <actions>
    <action content="Đã xong" arguments="action=checkin&amp;habitId={guid}" activationType="background"/>
    <action content="Để sau 1 giờ" arguments="action=snooze&amp;habitId={guid}" activationType="background"/>
  </actions>
</toast>
```

**Quy tắc nghiệp vụ Toast**:
1. **Không làm phiền nếu đã xong**: Trước khi phát thông báo vào giờ hẹn, hệ thống kiểm tra `IEntryRepository`. Nếu thói quen hôm nay đã đạt `IsCompleted == true`, **bỏ qua không phát thông báo**.
2. **Đánh dấu 1-Click từ thông báo**: Khi người dùng nhấn nút `"Đã xong"` ngay trên Toast:
   - Tiến trình nền ghi nhận `Entry` vào CSDL ngay lập tức.
   - Widget trên desktop tự động cập nhật đổi màu ô và tăng chuỗi streak mà không cần bật bất kỳ cửa sổ nào.
3. **Tôn trọng hệ thống**: Tự động hoãn thông báo nếu Windows đang ở chế độ Focus Assist (Không làm phiền) hoặc đang chơi game toàn màn hình.

---

## 4. KHỞI ĐỘNG CÙNG HỆ ĐIỀU HÀNH (`IStartupService`)

Sử dụng API hiện đại của UWP/MSIX Package:
```csharp
namespace HabitGrid.Platform.System;

public interface IStartupService
{
    Task<bool> IsAutoStartEnabledAsync();
    Task<bool> SetAutoStartEnabledAsync(bool enable);
}
```

- Sử dụng `StartupTask.GetAsync("HabitGridStartupTask")`.
- **Hành vi khi khởi động cùng Windows**:
  - Nhận tham số dòng lệnh `/startup` hoặc `/background`.
  - Khởi động âm thầm: Dựng các widget ngoài desktop, hiển thị icon khay hệ thống, **không mở cửa sổ quản lý chính**, không chiếm tiêu điểm của người dùng khi mới mở máy.

---

## 5. XỬ LÝ MỐC CHUYỂN NGÀY & THỨC DẬY TỪ CHẾ ĐỘ NGỦ (SLEEP / WAKEUP)

Lỗi phổ biến nhất của các ứng dụng theo dõi thói quen là máy tính gập màn hình (Sleep) qua đêm, sáng hôm sau mở máy thì ngày trong app vẫn là ngày hôm qua.

**Giải pháp kỹ thuật đồng bộ**:
1. **Timer mốc chuyển ngày**: Duy trì duy nhất 1 `System.Threading.Timer` tính toán chính xác số mili-giây tới thời điểm chuyển ngày tiếp theo (`DayProvider.GetNextDayTransitionUtc()`). Khi timer kích hoạt:
   - Phát thông điệp `DayTransitionEvent`.
   - Các Widget tự động dịch chuyển cột ngày hôm nay sang ô mới và cập nhật trạng thái hiển thị.
2. **Bắt sự kiện hệ thống `SystemEvents`**:
   - `SystemEvents.PowerModeChanged`: Khi nhận sự kiện `PowerModes.Resume` (máy tính bật lại từ Sleep/Hibernate):
     - Kiểm tra lại ngày logic hiện tại (`DayProvider.GetToday()`).
     - Nếu đã sang ngày mới so với ngày đang nạp trong bộ nhớ: Làm mới (Refresh) toàn bộ Widget và tính lại streak ngay tức thì.
   - `SystemEvents.TimeChanged`: Khi người dùng đổi múi giờ hoặc đồng hồ hệ thống được cập nhật NTP: Tự động tính toán lại lịch chuyển ngày.

---

## 6. TIÊU CHUẨN NGHIỆM THU (ACCEPTANCE CRITERIA)
- [ ] Chạy file thực thi lần 2 kích hoạt cửa sổ của lần chạy 1 thành công mà không mở thêm tiến trình thứ hai.
- [ ] Bấm nút "Đã xong" trên Toast thông báo ghi nhận thành công vào DB và widget đổi màu trong vòng < 100ms.
- [ ] Đổi ngày hệ thống bằng tay qua 00:00 hoặc gập máy qua đêm mở ra, widget tự động chuyển ô hôm nay chính xác mà không cần bấm F5 hay mở lại app.
- [ ] Bật/tắt khởi động cùng Windows hoạt động chính xác trong Task Manager > Startup Apps.
