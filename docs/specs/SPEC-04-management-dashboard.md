# Gói Công Việc 04: Bảng Điều Khiển Quản Lý & Onboarding (SPEC-04)

> **Mã công việc**: `SPEC-04-DASHBOARD`  
> **Thư viện mục tiêu**: `HabitGrid.UI` (WPF-UI, MVVM CommunityToolkit, .NET 8)  
> **Mục tiêu**: Xây dựng cửa sổ quản trị trung tâm (`MainWindow`), danh sách thói quen hỗ trợ kéo thả sắp xếp, trang chi tiết 52 tuần & biểu đồ tháng, hộp thoại tạo/sửa thói quen, và luồng làm quen ban đầu (Onboarding) hoàn thành trong dưới 30 giây.

---

## 1. CẤU TRÚC BẢNG ĐIỀU KHIỂN CHÍNH (`MainWindow.xaml`)

Giao diện áp dụng phong cách thiết kế Windows 11 Fluent Design thông qua thư viện `WPF-UI`:
- Bố cục 2 cột (Master-Detail hoặc Sidebar + Content Page):
  - **Cột trái (Sidebar / Habit List)**: Danh sách thẻ thói quen, trạng thái bật/tắt widget, nút "+ Thêm thói quen", nút chuyển đến Cài đặt.
  - **Cột phải (Detail Workspace)**: Chi tiết thói quen đang chọn: Lưới tổng quan 52 tuần, thẻ chỉ số thống kê, biểu đồ 12 tháng gần nhất, và các nút thao tác nghiệp vụ.

```
┌────────────────────────────────┬───────────────────────────────────────────────────────┐
│ HabitGrid            [⚙]       │ [Icon] Đọc sách (30 phút)          [Sửa] [Lưu trữ] [✕] │
├────────────────────────────────┼───────────────────────────────────────────────────────┤
│ [+ Thêm thói quen]             │ 🔥 Chuỗi hiện tại: 14 ngày   🏆 Kỷ lục: 42 ngày       │
│                                │ 📊 Tỷ lệ 30 ngày: 85%        ✅ Tổng hoàn thành: 120  │
│ [≡] Đọc sách           [Eye]   ├───────────────────────────────────────────────────────┤
│ [≡] Uống nước          [Eye]   │ Lưới lịch sử 52 tuần:                                 │
│ [≡] Chạy bộ (Pro)      [Lock]  │ [ 52 Weeks Heatmap Canvas Control                   ] │
│                                ├───────────────────────────────────────────────────────┤
│                                │ Biểu đồ hoàn thành 12 tháng:                         │
│ [Đã lưu trữ (1)]               │ [ Mini Bar Chart (Tháng 1 -> Tháng 12)              ] │
└────────────────────────────────┴───────────────────────────────────────────────────────┘
```

---

## 2. QUẢN LÝ DANH SÁCH THÓI QUEN (`HabitListView`)

### 2.1 Kéo thả sắp xếp (Drag & Drop Reordering)
- Sử dụng hành vi kéo thả trên danh sách: Người dùng giữ biểu tượng tay nắm `[≡]` để đổi thứ tự ưu tiên các thói quen.
- Khi nhả chuột, ViewModel tự động cập nhật trường `sort_order` và gọi `IHabitRepository.UpdateSortOrdersAsync()` dưới nền để lưu vị trí.

### 2.2 Công tắc hiển thị Widget (`Eye Icon`)
- Mỗi thẻ thói quen có một nút icon hình mắt:
  - Bật: Hiển thị widget thói quen đó trên desktop.
  - Tắt: Tạm ẩn widget khỏi desktop mà không làm thay đổi trạng thái theo dõi.

### 2.3 Rào chắn giới hạn Free (Soft Paywall Gate)
- Khi người dùng nhấn nút `[+ Thêm thói quen]`:
  - ViewModel kiểm tra tổng số thói quen chưa xóa (`CountNonDeletedAsync()`).
  - Nếu số lượng $\ge 2$ và người dùng chưa sở hữu bản quyền Pro:
    - **Không hiển thị hộp thoại báo lỗi hệ thống**.
    - Tự động mở ngay màn hình **Nâng cấp Pro (Paywall Modal)** được thiết kế trau chuốt (chi tiết tại `SPEC-07`), giải thích rõ ràng lý do và lợi ích mở khóa không giới hạn thói quen.

---

## 3. HỘP THOẠI TẠO & CHỈNH SỬA THÓI QUEN (`HabitEditDialog.xaml`)

Các trường nhập liệu trực quan:
1. **Tên thói quen** (`TextBox`): Bắt buộc, tối đa 40 ký tự, tự động focus khi mở hộp thoại.
2. **Biểu tượng (Icon Picker)**: Bảng lưới gồm 60 biểu tượng Fluent phổ biến (Sách vở, Nước uống, Quả tạ, Code, Ngủ, Trái tim, v.v.).
3. **Loại thói quen** (`SegmentedControl`):
   - `Đúng / Sai`: 1 click để hoàn thành.
   - `Đếm số lượng`: Hiện thêm ô nhập `Mục tiêu mỗi ngày` (ví dụ: 8 ly nước, 50 trang sách, 10000 bước).
4. **Lịch lặp trong tuần**: 7 nút bật/tắt (T2, T3, T4, T5, T6, T7, CN). Mặc định chọn cả tuần.
5. **Bảng màu Heatmap**:
   - Bản Free: Mặc định cố định màu Xanh lá GitHub.
   - Bản Pro: Lựa chọn 6 presets màu sắc phong phú hoặc click vào ColorPicker để chọn màu thương hiệu riêng. Các lựa chọn màu sắc có icon vương miện nhỏ nếu người dùng chưa nâng cấp.
6. **Nhắc nhở hằng ngày**:
   - Checkbox bật nhắc nhở.
   - Bộ chọn thời gian `TimePicker` (ví dụ: `21:00`).

---

## 4. TRANG CHI TIẾT & BIỂU ĐỒ THỐNG KÊ (`HabitDetailView.xaml`)

1. **4 Thẻ thống kê (Metric Cards)**:
   - **Chuỗi hiện tại (Current Streak)**: Hiển thị số ngày kèm icon ngọn lửa 🔥.
   - **Kỷ lục (Longest Streak)**: Chuỗi chu kỳ dài nhất trong lịch sử kèm icon cúp 🏆.
   - **Tỷ lệ 30 ngày**: Phần trăm số ngày hoàn thành trên tổng số ngày có lịch.
   - **Tổng số lần hoàn thành**: Số ngày tích lũy từ ngày bắt đầu.
2. **Lưới tổng thể 52 tuần**: Tái sử dụng control `HeatmapGridControl` với cấu hình kích thước L, cho phép người dùng click sửa lại dữ liệu của bất kỳ ngày nào trong năm qua.
3. **Biểu đồ cột mini (12 Months Bar Chart)**:
   - Hiển thị 12 cột tương ứng từ tháng 1 đến tháng 12 của năm hiện tại.
   - Chiều cao cột tương ứng với tỷ lệ % hoàn thành trong tháng đó.
   - Hover cột hiển thị tooltip: `"Tháng 10/2026: 25/31 ngày (80.6%)"`.
4. **Quy trình Xóa thói quen an toàn (Safe Deletion)**:
   - Nhấn nút "Xóa": Hiển thị hộp thoại xác nhận 2 bước (người dùng phải bấm giữ nút "Xác nhận xóa" trong 2 giây hoặc nhập lại đúng tên thói quen để tránh lỡ tay).
   - Khi xóa: Thực hiện **Soft Delete** (`deleted_at = UtcNow`) và hiển thị thanh thông báo nhỏ (Toast/Snackbar) ở đáy màn hình: `"Đã xóa thói quen [Tên] • [Hoàn tác]"` trong 10 giây. Sau thời gian này mới thực hiện xóa vĩnh viễn khỏi CSDL nếu không hoàn tác.

---

## 5. LUỒNG LÀM QUEN BAN ĐẦU (ONBOARDING $\le 30$ GIÂY)

Khi người dùng mở ứng dụng lần đầu tiên (CSDL chưa có thói quen nào):
- **Bước 1 (Chào đón)**: Màn hình tối giản với thông điệp: *"Theo dõi thói quen tối giản ngay trên màn hình nền Windows của bạn."* kèm nút **"Bắt đầu ngay"**.
- **Bước 2 (Gợi ý 1-Click)**: Hiển thị 5 thẻ thói quen mẫu được tạo sẵn:
  1. 💧 *Uống đủ 2L nước* (Count, mục tiêu 8 ly)
  2. 📖 *Đọc sách 20 phút* (Boolean)
  3. 🏃 *Tập thể dục / Đi bộ* (Boolean)
  4. 💻 *Học lập trình / Kỹ năng mới* (Boolean)
  5. 🧘 *Ngồi thiền 10 phút* (Boolean)
  Người dùng chỉ cần click chọn 1 thẻ hoặc bấm "Tự tạo thói quen riêng".
- **Bước 3 (Hiển thị ngay trên Desktop)**: Cửa sổ quản lý tạm thu nhỏ, một widget xuất hiện ngay lập tức trên Desktop với một bong bóng chỉ dẫn mờ (Coachmark): *"Bấm vào ô vuông hôm nay để đánh dấu lần đầu tiên!"*.
- **Bước 4 (Cấu hình nhanh)**: Hiện một hộp thoại nhỏ hỏi: *"Bạn có muốn HabitGrid tự động chạy khi khởi động máy tính không?"* (`Có` / `Để sau`).

---

## 6. TIÊU CHUẨN NGHIỆM THU (ACCEPTANCE CRITERIA)
- [ ] Luồng Onboarding từ lúc mở app đến khi có widget trên desktop và đánh dấu được ô đầu tiên mất dưới 30 giây.
- [ ] Sắp xếp kéo thả thói quen mượt mà, DB cập nhật ngay lập tức không bị lag UI.
- [ ] Tính năng xóa có hoàn tác (Undo) hoạt động tin cậy 100%.
- [ ] Bấm thêm thói quen thứ 3 khi chưa có Pro tự động điều hướng sang Paywall modal một cách lịch sự, không có thông báo lỗi thô thiển.
