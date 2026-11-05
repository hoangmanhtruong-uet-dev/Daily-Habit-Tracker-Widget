# Gói Công Việc 08: Đóng Gói MSIX & Phát Hành Microsoft Store (SPEC-08)

> **Mã công việc**: `SPEC-08-RELEASE`  
> **Dự án mục tiêu**: `HabitGrid.Package` (Windows Application Packaging Project - WAP, x64 + ARM64)  
> **Mục tiêu**: Thiết lập dự án đóng gói MSIX chuẩn hóa, cấu hình năng lực `runFullTrust`, chuẩn bị tài nguyên hình ảnh (Visual Assets), vượt qua kiểm thử tự động Windows App Certification Kit (WACK), soạn thảo hồ sơ Microsoft Store song ngữ (Việt - Anh) và chính sách quyền riêng tư (Privacy Policy).

---

## 1. CẤU HÌNH ĐÓNG GÓI MSIX (`Package.appxmanifest`)

### 1.1 Khai báo Năng lực (Capabilities)
Vì HabitGrid là ứng dụng WPF tương tác Win32 API (ghim WorkerW, đặt SetWindowPos, lắng nghe Mutex), gói MSIX bắt buộc khai báo:

```xml
<Capabilities>
  <rescap:Capability Name="runFullTrust" />
</Capabilities>
```

### 1.2 Khai báo Khởi động cùng Windows (StartupTask)
```xml
<Extensions>
  <desktop:Extension Category="windows.startupTask" Executable="HabitGrid.App\HabitGrid.App.exe" EntryPoint="Windows.FullTrustApplication">
    <desktop:StartupTask TaskId="HabitGridStartupTask" Enabled="false" DisplayName="HabitGrid Desktop Widget" />
  </desktop:Extension>
</Extensions>
```

---

## 2. BỘ TÀI NGUYÊN HÌNH ẢNH ỨNG DỤNG (VISUAL ASSETS)

Ý tưởng thiết kế biểu tượng: Một hình vuông bo góc hiện đại chứa lưới $3 \times 3$ ô vuông tối giản, trong đó 8 ô mang màu trung tính và 1 ô nổi bật bằng màu xanh lục bảo (Emerald Green) biểu trưng cho ngày thói quen được hoàn thành.

Cần cung cấp đầy đủ các kích thước ảnh PNG trong thư mục `HabitGrid.Package/Images/`:
- `StoreLogo.png` ($50 \times 50$, $100 \times 100$, $200 \times 200$)
- `Square44x44Logo.png` (TargetSize: 16, 24, 32, 48, 256)
- `Square150x150Logo.png` ($150 \times 150$, scale-100, 200, 400)
- `Wide310x150Logo.png` (scale-100, 200)
- `SplashScreen.png` ($620 \times 300$, scale-100, 200)

---

## 3. KIỂM THỬ WACK TRƯỚC KHI NỘP (WINDOWS APP CERTIFICATION KIT)

Chạy công cụ WACK cục bộ trước khi upload lên Partner Center:
```powershell
& "C:\Program Files (x86)\Windows Kits\10\App Certification Kit\appcert.exe" test -apptype desktop -packagepath ".\HabitGrid.Package\AppPackages\HabitGrid_1.0.0.0_x64_Test\HabitGrid_1.0.0.0_x64.msix" -reportoutputpath ".\build\wack-report.xml"
```

**Các lỗi thường gặp và cách khắc phục**:
1. *Lỗi Crash khi khởi động ngầm*: Ứng dụng phải xử lý tốt trường hợp khởi động không có màn hình chính khi WACK kiểm tra chế độ chạy nền.
2. *Lỗi Dependency*: Đảm bảo gói MSIX đã đóng gói đầy đủ `Microsoft.Data.Sqlite` và các thư viện C++ runtime cần thiết.
3. *Ghi chú giải trình kiểm duyệt (Notes for Certification)*:
   > "HabitGrid is a lightweight desktop widget application for habit tracking. It uses Win32 desktop interop to place transparent widget windows on the desktop background. The app is 100% offline, stores data locally in SQLite, and requires runFullTrust to position windows and run minimized in the system tray. No personal data is collected."

---

## 4. HỒ SƠ NIÊM YẾT MICROSOFT STORE (STORE LISTINGS)

### 4.1 Bản Tiếng Việt
- **Tên hiển thị**: `HabitGrid - Widget Theo Dõi Thói Quen Lưới`
- **Danh mục chính**: `Năng suất (Productivity)`
- **Mô tả ngắn ($\le 100$ ký tự)**:
  `Lưới thói quen kiểu GitHub ngay trên màn hình nền Windows. Đẹp, nhẹ, 100% ngoại tuyến.`
- **Mô tả chi tiết**:
  > Bạn muốn xây dựng thói quen tốt nhưng thường quên mở ứng dụng?  
  > **HabitGrid** mang các ô lưới thói quen tối giản ghim thẳng lên màn hình máy tính của bạn. Chỉ cần nhìn lướt qua desktop là biết hôm nay mình đã làm gì.  
  > 
  > ✨ **ĐIỂM NỔI BẬT**:
  > • **Widget trực tiếp trên Desktop**: Nằm gọn gàng trên hình nền, luôn hiện diện mỗi khi bạn dùng máy.  
  > • **Đánh dấu 1-chạm**: Nhấp vào ô hôm nay để hoàn thành trong 1 giây, hoặc đánh dấu trực tiếp từ thông báo Toast.  
  > • **Lưới thói quen Heatmap**: Mỗi ngày một ô vuông đậm nhạt tạo động lực giữ chuỗi streak liên tục.  
  > • **Nhẹ & Riêng tư**: 100% dữ liệu lưu trên máy tính của bạn. Không cần tài khoản, không quảng cáo, không theo dõi dữ liệu.  
  > • **Giao diện Fluent Windows 11**: Chuẩn phong cách thiết kế hiện đại, hỗ trợ Dark Mode và nền Acrylic mờ sang trọng.  
  > 
  > 📦 **GÓI MIỄN PHÍ & NÂNG CẤP PRO**:
  > - Bản Free: Theo dõi 2 thói quen hoàn toàn miễn phí mãi mãi.
  > - Bản Pro (Mua đứt một lần): Không giới hạn thói quen, tùy biến bảng màu sắc heatmap và công cụ xuất/nhập sao lưu JSON & CSV.
- **7 Cụm từ khóa tìm kiếm (Search Keywords)**:
  `habit tracker`, `thói quen`, `streak`, `heatmap widget`, `desktop widget`, `productivity`, `ghim màn hình`
- **Bộ 6 ảnh chụp màn hình (1920 $\times$ 1080 - 16:9)**:
  1. Widget hiển thị sắc nét trên hình nền Desktop Windows 11.
  2. Thao tác 1-click đánh dấu hôm nay kèm hiệu ứng pop trực quan.
  3. Bảng điều khiển quản lý thói quen với giao diện Fluent Design.
  4. Lưới lịch sử 52 tuần và biểu đồ thống kê theo tháng.
  5. Các bảng màu Heatmap rực rỡ của gói Pro.
  6. Giao diện Chế độ Tối (Dark Mode) và chế độ nền Acrylic trong suốt.

### 4.2 Bản Tiếng Anh (English - United States)
- **Title**: `HabitGrid - Desktop Habit Tracker Widget`
- **Category**: `Productivity`
- **Short Description**:
  `Minimalist GitHub-style habit heatmap pinned right on your Windows desktop. Clean, fast & offline.`
- **Keywords**:
  `habit tracker`, `streak counter`, `desktop widget`, `heatmap tracker`, `daily habits`, `productivity`, `fluent design`

---

## 5. CHÍNH SÁCH QUYỀN RIÊNG TƯ (PRIVACY POLICY)

Tạo file công khai `docs/PRIVACY-POLICY.md` (và host tại GitHub Pages):

> **Chính sách Quyền riêng tư của HabitGrid**:
> 1. **Thu thập dữ liệu**: HabitGrid KHÔNG thu thập, lưu trữ hay truyền tải bất kỳ thông tin cá nhân, lịch sử thói quen hay dữ liệu sử dụng nào của bạn ra máy chủ bên ngoài.
> 2. **Lưu trữ cục bộ**: 100% dữ liệu thói quen và cấu hình được lưu cục bộ trên thiết bị của bạn tại `%LocalAppData%\HabitGrid\habitgrid.db`.
> 3. **Kết nối mạng**: Ứng dụng chỉ sử dụng kết nối mạng Internet nhằm mục đích duy nhất là giao tiếp với Microsoft Store để xác thực và xử lý thanh toán giấy phép bản quyền theo tiêu chuẩn bảo mật của Microsoft.
> 4. **Liên hệ**: Mọi thắc mắc xin vui lòng gửi email về bộ phận hỗ trợ nhà phát triển.

---

## 6. TIÊU CHUẨN NGHIỆM THU PHÁT HÀNH (RELEASE GATE)
- [ ] Gói MSIX vượt qua 100% bài kiểm tra tự động của WACK (Windows App Certification Kit) không có lỗi Pass with warning hoặc Fail.
- [ ] Asset hình ảnh đầy đủ kích cỡ, hiển thị sắc nét trên màn hình High-DPI (200%, 300%).
- [ ] Bản quyền In-App Purchase `pro_unlock` được định cấu hình chính xác trên Microsoft Partner Center.
- [ ] Hoàn thành bảng câu hỏi phân loại độ tuổi IARC (kết quả All Ages).
- [ ] Ứng dụng sẵn sàng nộp và phát hành chính thức trên Microsoft Store.
