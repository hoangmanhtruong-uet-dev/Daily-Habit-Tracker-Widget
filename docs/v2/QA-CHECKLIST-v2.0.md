# Checklist kiểm thử tay trước beta v2.0

> Những mục CI không tự kiểm được (cần máy thật). Mỗi mục: làm theo bước, đánh dấu ✅/❌, ❌ thì chụp màn hình + gửi thư mục log (Cài đặt → Hỗ trợ → Mở thư mục log và báo lỗi).

## 1. Trình đọc màn hình (Narrator) — Win+Ctrl+Enter để bật/tắt
CI đã kiểm tự động: mọi nút/công tắc/ô nhập đang hiện trên Hôm nay, Thói quen, Chi tiết, Insights, Buddy, Cài đặt đều có tên đọc được. Phần còn lại cần tai người:
- [ ] Tab đi hết trang Hôm nay: mỗi thẻ đọc "tên thói quen + trạng thái", thứ tự hợp lý, không kẹt focus.
- [ ] Space/Enter trên thẻ check-in được; Narrator đọc trạng thái mới.
- [ ] Trang Insights: các ô số liệu đọc "Tỷ lệ hoàn thành: 80%"…; cột biểu đồ đọc "Thứ Hai: 75%".
- [ ] Paywall: đọc đủ tên gói + giá; nút "Để sau" tới được bằng bàn phím.
- [ ] Hộp thoại (sửa thói quen, chọn thói quen đang chạy, báo lỗi): Esc đóng được, focus quay lại chỗ cũ.

## 2. DPI cao / chữ lớn
- [ ] Cài đặt Windows → Hiển thị → Tỷ lệ 150% và 200%: không chữ nào bị cắt, nút không chồng nhau (Hôm nay, Insights, Cài đặt, paywall).
- [ ] Hai màn hình khác tỷ lệ (100% + 150%): kéo cửa sổ app qua lại, kéo widget qua lại — không mờ, không sai vị trí.
- [ ] Trợ năng → Cỡ chữ 150%: bố cục vẫn đọc được.
- [ ] Chế độ tương phản cao (Left Alt+Left Shift+Print Screen): chữ và nút vẫn thấy được.

## 3. Windows 10 (22H2, build 19045)
GitHub Actions không còn máy Windows 10 nên CI không kiểm được. App yêu cầu Windows 10 bản 2004 (19041) trở lên.
- [ ] Cài sạch trên máy Windows 10 chưa có .NET SDK: app mở được, onboarding chạy hết.
- [ ] Icon trên thanh bên và nút hiện đúng (Windows 10 dùng font Segoe MDL2 Assets thay Segoe Fluent Icons).
- [ ] Emoji hiện được (Windows 10 dùng emoji phẳng kiểu cũ, chấp nhận được).
- [ ] Widget ghim xuống desktop, sống sót Win+D và khởi động lại Explorer.
- [ ] Toast nhắc hiện đúng giờ khi đã tắt app và widget; bấm "Xong" ghi được.

## 4. Hành trình chính (cả Windows 10 và 11)
- [ ] Onboarding → widget hiện trên desktop ≤ 45 giây.
- [ ] Toast nhắc khi app và widget đều tắt; nút Xong / +1 / Nhắc lại / Bỏ qua.
- [ ] Phím tắt toàn cục Win+Alt+H, Win+Alt+F (hoặc Ctrl+Alt nếu bị chiếm).
- [ ] Plus tặng 30 ngày: Cài đặt hiện "còn N ngày"; đổi ngày hệ thống qua 31 ngày → về Free, hỏi chọn 5 thói quen, không mất dữ liệu.
- [ ] Gây lỗi giả (đổi tên tạm file CSDL khi app đang tắt rồi mở lại) → có báo lỗi lần sau, nút mở thư mục / sao chép hoạt động. Nhớ đổi lại tên file.
- [ ] Nâng cấp từ v1 với dữ liệu thật: thói quen và lịch sử còn đủ.
