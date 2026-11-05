# SPEC-V2-05 — Widget v2 (desktop ghim + Widgets Board + quick check-in)

> Phát hành: [v2.0] • Phụ thuộc: POC-V2, SPEC-V2-01, 02, 04 • Đây là **vũ khí khác biệt** của sản phẩm, ưu tiên chất lượng cao nhất.

## 1. POC-V2 (tuần 1, bắt buộc trước khi code widget)

Câu hỏi: cửa sổ WinUI 3 có ghim được xuống lớp desktop (child của WorkerW/Progman như v1) mà vẫn render, nhận chuột, sống sót Win+D, Explorer khởi động lại, đổi DPI/màn hình hay không?

| Phương án | Mô tả | Đánh giá cần đo |
|---|---|---|
| A. WinUI 3 `AppWindow` + `SetParent` | Cùng kỹ thuật v1 | Render có bị đen/không cập nhật? input? |
| B. Win2D/Composition trên cửa sổ Win32 thuần | Tiến trình riêng nhẹ, tự vẽ lưới | Nhẹ nhất, tự xử lý input |
| C. Giữ WPF cho riêng widget host | Dùng lại code v1 đã POC | Chắc chắn chạy; khác công nghệ với app chính |

Dùng lại bộ 7 tiêu chí POC v1 (`docs/specs/POC-desktop-pinning.md`) + thêm: RAM < 40 MB cho 3 widget, CPU nhàn rỗi ≈ 0%, Windows 11 24H2/25H2 và Windows 10 22H2.
**Quyết định (05/11/2026, xem `POC-V2-report.md`):** WinUI 3 ghim được nhưng không đạt ngân sách RAM/CPU (≈89 MB/tiến trình, 1 cửa sổ trống). **Chọn phương án C: `HabitGrid.WidgetHost` dùng WPF, tái dùng control/widget v1 đã kiểm chứng, cho v2.0.** App chính vẫn WinUI 3. Xem lại phương án B (Win32 + Direct2D) ở v2.1 nếu số đo RAM của WPF với 3 widget vượt ngân sách.

## 2. Widget ghim desktop

### 2.1 Loại widget
| Loại | Nội dung | Cỡ |
|---|---|---|
| Habit Grid | 1 thói quen: tiêu đề, streak, heatmap N tuần, nút check hôm nay | S (13 tuần), M (26), L (52), XL (52 + thống kê) |
| Today List | Danh sách thói quen hôm nay, check trực tiếp, vòng % | M, L |
| Buddy | Linh vật + vòng % hôm nay + streak tổng | S, M |
| Multi Grid | 2–4 thói quen, mỗi dòng 1 heatmap mỏng | M, L |
| Focus | Đồng hồ Focus đang chạy / nút bắt đầu | S |
| Squad [v2.2] | Tiến độ hôm nay của buddy/nhóm | M |

### 2.2 Tương tác
- Bấm ô hôm nay / nút check → check-in (animation `motion/check`, không âm thanh mặc định).
- Đếm: bấm = +1; chuột phải = menu (nhập số, bỏ qua, ghi chú, mood).
- Bấm ô ngày cũ → popover sửa (tùy cài đặt "cho phép sửa trên widget").
- Hover ô → tooltip (ngày, giá trị, ghi chú rút gọn).
- Kéo widget ở chế độ chỉnh sửa (giữ Alt hoặc bật "Chế độ sắp xếp" từ tray); khi tắt, widget **không di chuyển nhầm**.
- Chuột phải vùng trống → menu: đổi cỡ, đổi theme, ẩn, mở app, cài đặt widget.
- Bàn phím: widget không nhận focus bàn phím (tránh cướp focus khỏi desktop). Thao tác bằng bàn phím đi qua Quick check-in (§4).

### 2.3 Hiển thị
- Theme theo 01-DESIGN-SYSTEM §2.3; nền: Acrylic / Đặc / Trong suốt / Ảnh nền mờ (Plus).
- Độ mờ 0–100%, bo góc 0–24, bóng bật/tắt.
- "Thông minh với hình nền": tự chọn chữ sáng/tối theo độ sáng vùng hình nền phía sau (lấy mẫu khi đặt widget và khi đổi hình nền).
- Lưới snap 8 px + căn với widget khác; nhớ vị trí theo **từng cấu hình màn hình** (khóa = tập màn hình + độ phân giải); thiếu màn hình thì đưa widget về màn hình chính, không để mất ngoài vùng nhìn thấy.

### 2.4 Kỹ thuật
- Tiến trình `HabitGrid.WidgetHost.exe` riêng, khởi động cùng Windows (StartupTask) kể cả khi app chính không chạy. App chính tắt không làm widget mất.
- Đọc DB trực tiếp (chỉ đọc) + ghi check-in qua IPC tới app; nếu app không chạy, WidgetHost tự ghi qua cùng repository (SPEC-V2-02 A.1).
- Làm mới: khi có `DataChanged` (named pipe), khi qua ngày logic, khi resume từ sleep, khi đổi theme hệ thống.
- Giữ 3 chế độ ghim v1 (Desktop-child / Bottom-most / Normal) + tự phát hiện khi Explorer khởi động lại (`TaskbarCreated`) và ghim lại.
- Vẽ bằng một bề mặt duy nhất cho mỗi widget; chỉ vẽ lại vùng thay đổi.

## 3. Windows 11 Widgets Board
- Hiện thực `IWidgetProvider` (Windows App SDK) trong `HabitGrid.WidgetsBoard`, đăng ký trong manifest.
- Template Adaptive Cards: **Hôm nay** (danh sách + nút check), **Streak** (thói quen chọn + số ngày + 7 ô gần nhất), **Buddy**.
- Cỡ small/medium/large; hành động `Action.Execute` để check-in ngay trên board.
- Cập nhật khi dữ liệu đổi và tối thiểu mỗi lần qua ngày.

## 4. Quick check-in (phím tắt toàn cục)
- `RegisterHotKey` (mặc định `Win+Alt+H`; nếu trùng thì báo và gợi ý phím khác).
- Cửa sổ nổi 420×(tự co) giữa màn hình đang có con trỏ, nền Acrylic, danh sách thói quen hôm nay chưa xong ở trên.
- Gõ để lọc (không dấu vẫn khớp: "doc" khớp "Đọc sách"); `Enter` check; `+`/`-` tăng giảm cho loại đếm; `Tab` sang ghi chú; `Esc` đóng. Mất focus thì tự đóng.
- Mở < 150 ms (cửa sổ tạo sẵn, ẩn).

## 5. Tray
Menu: Hôm nay (check trực tiếp tối đa 8 thói quen) • Bắt đầu Focus • Hiện/ẩn widget • Chế độ sắp xếp widget • Mở HabitGrid • Cài đặt • Thoát. Icon tray đổi trạng thái: vòng % hôm nay nhỏ.

## 6. Tiêu chí nghiệm thu
1. POC-V2 có báo cáo (như `poc-report.md` v1) và quyết định phương án.
2. 7 tiêu chí ghim v1 đạt trên Win11 24H2+ và Win10 22H2.
3. 3 widget L + 1 Today List: RAM WidgetHost < 60 MB, CPU nhàn rỗi < 0,5% trung bình 10 phút.
4. Check-in từ widget hiện trên app chính và Widgets Board < 1 s.
5. Rút màn hình phụ khi widget nằm ở đó → widget về màn hình chính; cắm lại → về vị trí cũ.
6. Quick check-in: từ lúc bấm phím tắt tới lúc check xong một thói quen ≤ 2 s (đo tay 10 lần).

## Tiến độ triển khai (05/11/2026)
Đã có `HabitGrid.WidgetHost` (WPF, tiến trình riêng): widget lưới (tối đa 3, đọc CSDL v3), widget **Today List**, **Quick check-in** (`Win+Alt+H`, dự phòng `Ctrl+Alt+H`; lọc không dấu, Enter/+/−/↑↓/Esc), tray, nhớ vị trí, làm mới theo `PRAGMA data_version` và khi qua ngày/thức dậy từ sleep, **tự chạy cùng Windows** (khóa Run, tắt/bật từ Cài đặt app chính), **dựng lại và ghim lại sau khi Explorer khởi động lại** (đã kiểm chứng trên CI bằng cách kết thúc `explorer.exe` thật), đưa widget ngoài vùng nhìn thấy về màn hình chính khi đổi màn hình.
Ghi chú kỹ thuật: v1 dùng cửa sổ message-only để nghe `TaskbarCreated` nên **không bao giờ nhận được** thông điệp broadcast này; WidgetHost dùng cửa sổ top-level ẩn và có thêm kiểm tra handle còn sống.
**Chưa làm:** widget Buddy / Multi Grid / Focus, Windows 11 Widgets Board, nền Acrylic thật, popover sửa ngày cũ, thông minh theo hình nền, snap lưới, đo tiêu chí 3–6 trên máy thật.
