# SPEC-V2-09 — Nhắc nhở thông minh, Focus timer, tích hợp Windows

> Phát hành: [v2.0], WNS [v2.1] • Phụ thuộc: SPEC-V2-01, 04, 05

## 1. Nhắc nhở
### 1.1 Loại
| Loại | Mô tả |
|---|---|
| Cố định | Một hoặc nhiều giờ, theo ngày trong tuần |
| Thông minh | App đề xuất giờ = phân vị 40% giờ check-in 21 ngày gần nhất của thói quen − 15 phút; cần ≥ 7 mẫu, trước đó dùng giờ mặc định theo `TimeOfDay` (Sáng 8:00, Chiều 14:00, Tối 20:00, Bất kỳ 19:00) |
| Nhắc lần 2 | Nếu chưa xong sau giờ nhắc + 2 giờ (không quá 22:30) |
| Cứu streak | 21:00 nếu có thói quen streak ≥ 3 chưa xong hôm nay (một toast gộp) |
| Tổng kết sáng | Tùy chọn, 1 toast buổi sáng liệt kê việc hôm nay |

### 1.2 Toast có hành động
- Nút: **Xong** (check-in ngay, không mở app), **+1** (loại đếm), **Nhắc lại sau 30 phút**, **Bỏ qua hôm nay**.
- Gộp: nhiều thói quen cùng giờ → một toast có danh sách.
- Kích hoạt toast khi app không chạy: COM activator của Windows App SDK ghi trực tiếp vào DB (cùng đường ghi như WidgetHost).

### 1.3 Lập lịch
- Dùng `ScheduledToastNotification` của Windows để toast vẫn hiện khi app/WidgetHost không chạy; lên lịch trước 48 giờ, làm mới khi dữ liệu đổi, khi qua ngày, khi khởi động.
- Hủy toast đã lên lịch nếu thói quen đã xong trước giờ nhắc.
- Giờ yên lặng (mặc định 23:00–07:00) và tôn trọng Do Not Disturb / Focus của Windows.
- Giới hạn: tối đa 6 toast nhắc/ngày cho toàn app (gộp nếu vượt).

## 2. Focus timer
- Chế độ: Pomodoro (25/5, chỉnh được), đếm ngược tự chọn, bấm giờ tự do.
- Gắn với thói quen loại Thời lượng (hoặc bất kỳ): kết thúc phiên → cộng phút vào `DayLog.Value`.
- Giao diện: mini-player nổi luôn trên cùng (thu gọn được), widget Focus, icon tray hiện thời gian còn lại.
- Tùy chọn bật Windows Focus session / Do Not Disturb khi bắt đầu (nếu API khả dụng; không thì chỉ gợi ý).
- Âm thanh nền (Plus): mưa, quán cà phê, lo-fi loop ngắn đóng gói trong app (bản quyền rõ ràng).
- Phiên vẫn tiếp tục nếu đóng cửa sổ chính; máy sleep thì tạm dừng và hỏi khi resume.

## 3. Phím tắt
| Phím | Phạm vi | Hành động |
|---|---|---|
| `Win+Alt+H` | Toàn cục | Quick check-in (SPEC-V2-05 §4) |
| `Win+Alt+F` | Toàn cục | Bắt đầu/tạm dừng Focus |
| `Ctrl+K` | Trong app | Command palette |
| `Ctrl+N` | Trong app | Thói quen mới |
| `Ctrl+Z` | Trong app | Hoàn tác thao tác gần nhất (check, xóa, sắp xếp) |
| `1`–`9` | Trang Hôm nay | Check dòng tương ứng |
| `Ctrl+,` | Trong app | Cài đặt |
Phím tắt toàn cục đổi/tắt được trong Cài đặt; xung đột thì báo rõ.

## 4. Khởi động & nền
- StartupTask (MSIX) khởi động **WidgetHost** khi đăng nhập Windows (nhẹ); app chính chỉ chạy khi người dùng mở.
- Xử lý: sleep/resume, đổi giờ hệ thống, đổi múi giờ, khóa/mở khóa màn hình → làm mới ngày logic và lịch nhắc (giữ `SystemEventMonitor` v1).

## 5. Tích hợp ngoài [v3, ghi nhận để thiết kế không chặn]
- Xuất lịch ICS các giờ nhắc.
- Nhập dữ liệu từ app khác (CSV của Loop, HabitKit, Habitify… theo định dạng công khai) [v2.1 nếu kịp].
- Strava/Apple Health/Google Fit cho thói quen tự đo (cần mobile).

## 6. Tiêu chí nghiệm thu
1. Toast nhắc hiện đúng giờ khi app và WidgetHost đều tắt.
2. Bấm "Xong" trên toast khi app tắt → dữ liệu ghi đúng, widget cập nhật khi chạy lại.
3. Thói quen xong trước giờ nhắc → không có toast.
4. Nhắc thông minh: test với dữ liệu giả lập cho ra giờ đúng công thức; < 7 mẫu dùng giờ mặc định.
5. Focus 25 phút → `DayLog.Value` của thói quen Thời lượng tăng 25, XP +5.
6. Giờ yên lặng chặn mọi toast nhắc (trừ khi người dùng tắt giờ yên lặng).

## Tiến độ triển khai (06/11/2026)
**Đã có**
- Lõi thuần có test: `ReminderPlanner` (cố định, thông minh, nhắc lần 2 +2 giờ ≤ 22:30, cứu streak 21:00 chỉ cho hôm nay, tổng kết sáng, bỏ thói quen đã xong/bỏ qua/đóng băng, giờ yên lặng qua nửa đêm, gộp cùng giờ, tối đa 6 toast/ngày, cửa sổ 48 giờ), `SmartReminder` (phân vị 40% − 15 phút, ≥ 7 mẫu trong 21 ngày), `FocusTimer` (Pomodoro/đếm ngược/bấm giờ, tạm dừng, khôi phục trạng thái), XP +5 cho phiên ≥ 25 phút, huy hiệu `focus_10h`.
- WidgetHost lên lịch `ScheduledToastNotification` (CommunityToolkit.WinUI.Notifications 7.1.2) nên Windows tự hiện toast kể cả khi app và WidgetHost tắt; nút Xong / +1 / Nhắc lại 30 phút / Bỏ qua hôm nay ghi thẳng vào CSDL (Windows khởi động WidgetHost nếu cần). Lập lại lịch khi dữ liệu đổi, qua ngày, khởi động, đổi giờ/múi giờ.
- Focus: mini-player nổi luôn trên cùng (thu gọn được), `Win+Alt+F` (dự phòng `Ctrl+Alt+F`), phiên sống qua khởi động lại, tạm dừng khi máy ngủ và hiện lại khi thức dậy, cộng phút vào thói quen Thời lượng; bắt đầu từ trang chi tiết thói quen hoặc command palette (lệnh gửi qua bảng settings).
- Trình sửa thói quen đã có sẵn giờ nhắc kèm ô "Thông minh" và "Nhắc lần 2"; planner dùng đúng các cờ này.
- Phím tắt trong app: `Ctrl+K` command palette (lọc không dấu), `Ctrl+N`, `Ctrl+Z` (Hôm nay / xóa thói quen), `Ctrl+,`, `1`–`9` trên trang Hôm nay. Cài đặt: bật/tắt nhắc, giờ yên lặng, cứu streak, tổng kết sáng, bật/tắt hai phím toàn cục và hiện phím thực tế đã đăng ký.
- Lưu phiên Focus: dùng bảng `xp_events` có sẵn (`kind = "focus"`, `amount` = phút) thay vì thêm migration v4 (tránh phải sửa importer/backup đang khóa phiên bản lược đồ v3).

**Đã kiểm chứng trên CI:** Windows nhận đủ toast đã lên lịch (4/4), nút Xong ghi check-in và gỡ nhắc hôm nay, phiên Focus 1 phút được ghi. Trên runner `Win+Alt+F` bị chiếm nên dùng `Ctrl+Alt+F`.
**Chưa kiểm chứng:** toast hiện thật đúng giờ khi mọi tiến trình đã tắt (tiêu chí 1) và bấm nút khi WidgetHost chưa chạy (tiêu chí 2) — CI chỉ kiểm tra lịch đã được Windows nhận và bộ xử lý nút; cần thử tay trên máy thật. Tôn trọng Do Not Disturb dựa vào Windows (không tự kiểm tra).
**Chưa làm:** thời gian còn lại trên icon tray, widget Focus, bật Focus session/DND của Windows, âm thanh nền (Plus), đổi tổ hợp phím (mới chỉ bật/tắt), xuất ICS / nhập CSV [v3].
