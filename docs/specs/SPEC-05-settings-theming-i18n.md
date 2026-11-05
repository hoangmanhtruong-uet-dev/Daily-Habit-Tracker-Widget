# Gói Công Việc 05: Cài Đặt, Theme & Đa Ngôn Ngữ (SPEC-05)

> **Mã công việc**: `SPEC-05-SETTINGS`  
> **Thư viện mục tiêu**: `HabitGrid.UI` (WPF-UI, Resource Dictionaries, .NET 8)  
> **Mục tiêu**: Xây dựng trung tâm Cài đặt với cơ chế lưu tức thì (Auto-save), hệ thống Theme thích ứng (Sáng/Tối/Hệ thống, Nền Acrylic/Đặc/Không nền), hỗ trợ đa ngôn ngữ Việt - Anh qua `.resx`, và tuân thủ tiêu chuẩn trợ năng Windows Accessibility (WCAG AA).

---

## 1. NGUYÊN TẮC THIẾT KẾ TRANG CÀI ĐẶT (`SettingsView.xaml`)

- **Không dùng nút "Lưu"**: Mọi thay đổi trên giao diện (Toggle, Slider, ComboBox) đều được ViewModel ghi ngay lập tức xuống CSDL thông qua `ISettingsRepository` và phát tín hiệu `Messenger` để cập nhật trực tiếp lên các widget đang mở.
- **Tổ chức phân nhóm rõ ràng theo tiêu chuẩn Windows 11**:

### 1.1 Nhóm Chung (General)
1. **Ngôn ngữ (Language)**: ComboBox lựa chọn `Tiếng Việt` hoặc `English`. Thay đổi áp dụng ngay lập tức mà không cần khởi động lại app.
2. **Tuần bắt đầu từ (Week Starts On)**: ComboBox `Thứ Hai` (Mặc định chuẩn ISO) hoặc `Chủ Nhật`. Lưới heatmap và nhãn thứ sẽ tự động sắp xếp lại tương ứng.
3. **Mốc chuyển ngày mới (Logical Day Cutoff)**: Slider từ `00:00` đến `05:00` (Bước nhảy 1 giờ). Mặc định `00:00`. Dành cho người làm việc khuya muốn qua nửa đêm vẫn tính là ngày hôm trước.
4. **Khởi động cùng Windows**: Toggle công tắc kết nối tới `IStartupService` (chi tiết ở `SPEC-06`).

### 1.2 Nhóm Giao diện (Appearance & Theming)
1. **Chế độ màu**: `Theo hệ thống` (Mặc định) / `Sáng (Light)` / `Tối (Dark)`.
2. **Kiểu nền Widget (Background Style)**:
   - `Acrylic (Mờ trong suốt)`: Dùng hiệu ứng Acrylic / Mica mờ ảo của Windows 11.
   - `Đặc (Solid)`: Nền đơn sắc có đổ bóng nhẹ.
   - `Không nền (Transparent / None)`: Ẩn hoàn toàn khung nền, chỉ các ô vuông thói quen hiển thị lơ lửng trên desktop.
3. **Độ mờ nền (Background Opacity)**: Slider $0\% \rightarrow 100\%$ (mặc định $85\%$).
4. **Bo góc Widget (Corner Radius)**: Slider từ $4\text{px}$ đến $16\text{px}$ (mặc định $12\text{px}$).

### 1.3 Nhóm Widget Desktop
1. **Chế độ ghim Desktop**:
   - `Desktop-Level`: Ghim chìm vào hình nền, không bị ẩn khi nhấn `Win + D`.
   - `Dưới cùng (Bottom-most)`: Nằm đáy cửa sổ ứng dụng (chế độ an toàn).
   - `Luôn trên cùng (Always-on-top)`: Thích hợp khi muốn tập trung theo dõi liên tục.
2. **Cỡ hiển thị mặc định**: `Nhỏ (S)` / `Vừa (M)` / `Lớn (L)`.
3. **Chế độ chỉnh sửa (Edit Mode)**: Toggle bật tắt viền kéo và tay nắm căn chỉnh trên màn hình.
4. **Đặt lại vị trí Widget (Reset Positions)**: Nút bấm đưa toàn bộ widget đang mở về giữa màn hình chính (tránh trường hợp người dùng kéo lệch ra ngoài màn hình).

### 1.4 Nhóm Dữ liệu & Sao lưu (Data & Backup)
- **Người dùng Free**:
  - Dòng chữ giải thích minh bạch: *"Dữ liệu của bạn được lưu trữ an toàn 100% ngoại tuyến trên máy tính này tại: `%LocalAppData%\HabitGrid`"*.
  - Nút **"Mở thư mục dữ liệu"**: Mở trực tiếp thư mục chứa file `habitgrid.db` trong Windows Explorer để người dùng tự copy sao lưu thủ công.
- **Người dùng Pro**:
  - Nút **"Xuất file sao lưu (JSON)"**: Lưu trữ toàn bộ dữ liệu cấu hình và lịch sử vào file `.json`.
  - Nút **"Xuất bảng tính (CSV)"**: Cho phép xuất dữ liệu thói quen để mở bằng Excel/Google Sheets.
  - Nút **"Nhập file sao lưu (JSON)"**: Hỗ trợ 2 chế độ `Gộp` hoặc `Thay thế`.

### 1.5 Nhóm Giấy phép (License & Store)
- Huy hiệu trạng thái: `Bản Free (2 thói quen)` hoặc `Bản Pro Vĩnh Viễn`.
- Nút **"Mở khóa Pro"**: Mở Paywall modal nếu đang dùng Free.
- Nút **"Khôi phục giao dịch"**: Kiểm tra lại giấy phép từ Microsoft Store khi cài app trên máy tính mới.

### 1.6 Nhóm Giới thiệu (About)
- Tên app, logo, phiên bản (ví dụ: `v1.0.0 (x64)`).
- Nút **"Sao chép thông tin phiên bản"** (phục vụ hỗ trợ kỹ thuật khi người dùng báo lỗi).
- Đường dẫn mở trang **Chính sách riêng tư (Privacy Policy)** và **Hỗ trợ qua Email / GitHub**.

---

## 2. KIẾN TRÚC ĐA NGÔN NGỮ (I18N ARCHITECTURE)

- Toàn bộ chuỗi hiển thị được lưu trữ trong file tài nguyên chuẩn .NET:
  - `Resources.resx` (Mặc định: Tiếng Anh - en-US).
  - `Resources.vi-VN.resx` (Tiếng Việt).
- Tạo `LocalizationManager` cho phép binding chuỗi động trong XAML bằng Markup Extension:
  `Text="{loc:Translate Habit_AddNew}"`.
- Các quy tắc định dạng số, ngày tháng, tên thứ và tháng phải sử dụng `CultureInfo` tương ứng:
  - Tiếng Việt: `Thứ Ba, 21/10/2026`, `Tháng 10`.
  - Tiếng Anh: `Tuesday, Oct 21, 2026`, `October`.

---

## 3. TRỢ NĂNG & TIÊU CHUẨN TRUY CẬP (ACCESSIBILITY & SHORTCUTS)

### 3.1 Bàn phím & Phím tắt toàn cục trong App
- Khi bảng điều khiển hoặc widget đang được chọn (Focus):
  - `Ctrl + N`: Mở hộp thoại thêm thói quen mới.
  - `Space` hoặc `Enter`: Đánh dấu hoàn thành ô đang chọn.
  - `Phím mũi tên (←, ↑, →, ↓)`: Di chuyển tiêu điểm giữa các ô trong lưới thói quen.
  - `Ctrl + Z`: Hoàn tác thao tác đánh dấu vừa thực hiện.
  - `Delete`: Lưu trữ thói quen đang chọn.
  - `Esc`: Đóng popup hoặc hộp thoại đang mở.

### 3.2 Hỗ trợ Windows Narrator & High Contrast
- Mỗi ô vuông trong control lưới được gắn nhãn trợ năng (Automation Properties):
  - Tên: `"Ngày 21 tháng 10 năm 2026, trạng thái: Đã hoàn thành, mục tiêu: 8 trên 8"`.
  - Loại control: `Button` hoặc `CheckBox`.
- Khi người dùng bật chế độ tương phản cao (High Contrast Mode) trong Windows:
  - Tự động chuyển đổi các ô vuông sang viền đơn sắc rõ nét, không dựa thuần túy vào màu sắc đổ bóng để phân biệt trạng thái.

---

## 4. TIÊU CHUẨN NGHIỆM THU (ACCEPTANCE CRITERIA)
- [ ] Chuyển đổi ngôn ngữ Việt $\leftrightarrow$ Anh thay đổi toàn bộ văn bản tức thì mà không bị crash.
- [ ] Thay đổi Theme Sáng / Tối / Tự động cập nhật mượt mà trên toàn bộ cửa sổ chính và các widget ngoài desktop.
- [ ] Phím tắt bàn phím hoạt động đầy đủ, người dùng khiếm thị có thể dùng Narrator và phím mũi tên để duyệt và đánh dấu thói quen.
