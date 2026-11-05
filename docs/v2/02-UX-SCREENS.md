# 02 — Đặc tả màn hình và luồng (UX v2)

> Kích thước tham chiếu cửa sổ chính: 1200×780, tối thiểu 860×600. Bố cục co giãn ở 3 mốc: hẹp (< 1000), vừa, rộng (≥ 1400).
> Mọi chuỗi hiển thị đi qua tài nguyên đa ngôn ngữ (VI/EN ở v2.0).

## 1. Sơ đồ điều hướng

```
Shell (NavigationView bên trái, thu gọn được)
├─ Hôm nay            (mặc định)
├─ Thói quen          → Chi tiết thói quen → Sửa
├─ Insights
├─ Buddy & Thành tích
├─ Bạn bè             [v2.2]
├─ (cuối) Cài đặt • Tài khoản • Nâng cấp Plus
Lớp phủ toàn cục: Command palette (Ctrl+K), Quick check-in (phím tắt toàn cục), Focus timer mini
Ngoài app: Widget desktop, Widgets Board, Tray, Toast
```

## 2. Onboarding (mục tiêu: widget trên desktop trong ≤ 45 giây)

| Bước | Nội dung | Ghi chú |
|---|---|---|
| 1. Chào | Grid Buddy vẫy tay + "Mỗi ngày một ô sáng." Nút "Bắt đầu" | Có link "Tôi có dữ liệu cũ" → đăng nhập/nhập file |
| 2. Mục tiêu | Chọn 1–3 chủ đề: Học, Sức khỏe, Tinh thần, Sáng tạo, Bỏ thói quen xấu, Tự chọn | Gợi ý thói quen bước 3 theo chủ đề |
| 3. Chọn thói quen | Lưới thẻ mẫu (icon, tên, tần suất gợi ý), chọn tối đa 3; mỗi thẻ sửa nhanh tên/mục tiêu | Có "Tạo mới" |
| 4. Đặt tên Buddy | Nhập tên linh vật, chọn màu lông | Gắn cảm xúc sớm, tăng giữ chân |
| 5. Đặt widget | Xem trước widget trên ảnh desktop của chính người dùng; chọn cỡ + theme; nút "Ghim lên desktop" | Thực sự tạo widget, nhấp nháy viền 2 giây để người dùng thấy |
| 6. Nhắc nhở | "Nhắc mình lúc nào?" 3 lựa chọn nhanh + tự chọn; xin quyền thông báo | Có thể bỏ qua |
| 7. Check-in đầu | "Thử bấm vào ô hôm nay nhé" → animation + confetti + XP đầu tiên | Kết thúc onboarding |

Không bắt đăng nhập. Sau 3 ngày dùng, gợi ý nhẹ "Sao lưu lên cloud" (v2.1).

## 3. Trang "Hôm nay"

Bố cục từ trên xuống:
1. **Header**: lời chào theo giờ ("Chào buổi tối, Linh"), ngày, vòng tiến độ hôm nay (x/y), Grid Buddy nhỏ với biểu cảm theo tiến độ.
2. **Chuỗi tổng**: số ngày liên tục có ít nhất 1 check-in, số freeze còn lại.
3. **Danh sách hôm nay** nhóm theo khung giờ: Sáng / Chiều / Tối / Bất kỳ lúc nào. Mỗi dòng gồm icon, tên, mini heatmap 7 ngày, streak, `HabitCheckButton`.
   - Loại có/không: bấm một lần là xong.
   - Đếm: bấm +1; giữ hoặc chuột phải để nhập số.
   - Thời lượng: bấm play mở Focus timer; hoặc nhập phút.
   - Cần bỏ: hiện "Đã giữ được X ngày", nút "Tôi đã lỡ" (xác nhận 2 bước, ngôn ngữ nhẹ nhàng).
   - Không có lịch hôm nay: mờ, nằm ở nhóm "Không cần hôm nay" (thu gọn).
4. **Đã xong**: thu xuống cuối, có thể ẩn.
5. Khi xong 100%: Buddy ăn mừng + confetti + thẻ "Chia sẻ ngày hoàn hảo".

Tương tác thêm: kéo-thả sắp xếp; chuột phải → bỏ qua hôm nay / thêm ghi chú / sửa; phím `1–9` check nhanh dòng tương ứng; `N` thêm ghi chú cho dòng đang chọn.

Sau check-in: InfoBar "Đã xong Đọc sách · +10 XP" có nút **Hoàn tác** 5 giây; nếu thói quen bật "hỏi mood" thì hiện chip mood 5 mức (tùy chọn, bỏ qua được).

## 4. Trang "Thói quen"

- Lưới thẻ (hoặc danh sách) mọi thói quen đang chạy; bộ lọc theo chủ đề/tag; ô tìm kiếm.
- Mỗi thẻ: icon, tên, heatmap 12 tuần, streak, tỷ lệ 30 ngày.
- Tab "Lưu trữ": thói quen đã lưu trữ, khôi phục được (v1 chưa làm được).
- Nút "+ Thói quen mới" mở trình tạo; nút "Mẫu" mở thư viện mẫu (≥ 60 mẫu, 8 chủ đề).
- Routine (nhóm thói quen theo thứ tự, ví dụ "Sáng thức dậy"): hiển thị như thẻ gộp, bấm vào chạy từng bước.

## 5. Chi tiết thói quen

1. Header: icon lớn, tên, mô tả, tần suất, nút Sửa / Lưu trữ / Chia sẻ.
2. Hàng StatCard: streak hiện tại, kỷ lục, tỷ lệ 30 ngày (so với 30 ngày trước), tổng lần, (đếm/thời lượng) tổng và trung bình.
3. Heatmap 52 tuần, chuyển được năm; bấm ô mở popover sửa giá trị/ghi chú/mood/bỏ qua của ngày đó.
4. Biểu đồ: tỷ lệ theo tháng; theo thứ trong tuần; theo giờ check-in (v2.0, chỉ khi có dữ liệu thời gian).
5. Nhật ký: danh sách ghi chú + mood theo ngày, tìm được.
6. Insight tự động: 1–3 câu (SPEC-V2-07).

## 6. Trình tạo/sửa thói quen (dialog nhiều phần, cuộn được)

| Phần | Trường |
|---|---|
| Cơ bản | Tên (≤ 40 ký tự), icon/emoji, màu, mô tả (≤ 200 ký tự), tag |
| Loại | Có/không • Đếm (mục tiêu + đơn vị: ly, trang, lần, tùy chọn) • Thời lượng (phút) • Cần bỏ |
| Tần suất | Mỗi ngày • Các thứ cố định • X lần/tuần • X lần/tháng • Mỗi N ngày |
| Khung giờ | Sáng / Chiều / Tối / Bất kỳ |
| Nhắc nhở | Nhiều giờ nhắc; "Nhắc thông minh" (SPEC-V2-09); nhắc lần 2 nếu chưa làm |
| Ghi nhận thêm | Hỏi mood sau check-in; cho phép ghi chú |
| Widget | Hiện trên widget nào, cỡ, theme riêng |
| Nâng cao | Ngày bắt đầu; cho phép freeze; thuộc routine nào |

Xem trước heatmap trực tiếp theo màu đang chọn. Lưu bằng Ctrl+Enter.

## 7. Insights

- Bộ chọn khoảng: 7 ngày, 30 ngày, 90 ngày, năm, tùy chọn.
- Thẻ tổng: tỷ lệ hoàn thành chung, ngày hoàn hảo, XP kiếm được, thói quen tốt nhất / cần chú ý.
- Heatmap tổng hợp (mọi thói quen gộp, độ đậm = % hoàn thành ngày).
- Biểu đồ: xu hướng tuần; theo thứ; mood vs hoàn thành (nếu có dữ liệu mood).
- "Phát hiện" (insight tự động) và, ở v2.2, nút "Hỏi coach" (Plus).

## 8. Buddy & Thành tích

- Buddy lớn ở giữa, tên, level, thanh XP, trạng thái hôm nay.
- Tủ đồ (cosmetics): mũ, phụ kiện, nền phòng; mở khóa bằng level/huy hiệu; một số chỉ Plus.
- Lưới huy hiệu: đã có (màu) / chưa có (bóng mờ + điều kiện).
- Freeze: số đang có, cách nhận thêm.
- Lối vào "Wrapped" khi đến kỳ.

## 9. Bạn bè [v2.2]

- Danh sách bạn: avatar, Buddy của họ, streak tổng, đã xong hôm nay chưa (chỉ hiện nếu bạn chia sẻ).
- Buddy (đồng hành): ghép 1–1 theo một thói quen; thấy check-in của nhau; nút "Nudge" (tối đa 1 lần/ngày/người).
- Thử thách nhóm: tên, thói quen chung, thời hạn, bảng tiến độ thành viên, reaction emoji.
- Mời bạn bằng link/mã 6 ký tự hoặc QR.
- Quyền riêng tư: mặc định chỉ chia sẻ **tên thói quen + đã xong/chưa**; không bao giờ chia sẻ ghi chú, mood.

## 10. Cài đặt

Nhóm trang: Chung (ngôn ngữ, giờ chuyển ngày, ngày đầu tuần, khởi động cùng Windows) • Giao diện (theme, theme thẩm mỹ, giảm chuyển động, âm thanh) • Widget (chế độ ghim, cỡ mặc định, độ mờ, chặn tương tác, đặt lại vị trí) • Thông báo (bật/tắt, giờ yên lặng, tôn trọng Focus) • Phím tắt (đổi phím tắt toàn cục) • Tài khoản & đồng bộ (v2.1) • Quyền riêng tư (telemetry opt-in, không sync ghi chú, xuất dữ liệu, xóa tài khoản) • Plus • Giới thiệu.

Mọi thay đổi lưu ngay, áp dụng ngay (giữ nguyên tắc v1).

## 11. Command palette (Ctrl+K) và Quick check-in

- Ctrl+K trong app: tìm thói quen, check-in, mở trang, đổi theme, bắt đầu Focus, tạo thói quen.
- Phím tắt toàn cục (mặc định `Win+Alt+H`, đổi được): mở cửa sổ nổi nhỏ giữa màn hình gồm danh sách hôm nay; gõ chữ để lọc, Enter để check, Esc để đóng. Mục tiêu: check-in trong ≤ 2 giây không rời bàn phím.

## 12. Paywall Plus

Hiện khi: chạm giới hạn Free, bấm tính năng Plus, hoặc mở từ menu. Không bao giờ hiện tự động lúc mở app hoặc giữa lúc check-in.
Nội dung: Buddy cầm vé, 4–6 lợi ích có icon, 3 gói (Tháng / Năm có nhãn "Tiết kiệm X%" / Trọn đời), dùng thử 7 ngày (gói năm), nút khôi phục mua hàng, link điều khoản.

## 13. Trạng thái đặc biệt

| Trạng thái | Xử lý |
|---|---|
| Trống (chưa có thói quen) | Buddy + "Thêm thói quen đầu tiên" + mẫu gợi ý |
| Offline (v2.1+) | Chấm nhỏ ở thanh tiêu đề "Đang offline, sẽ đồng bộ sau". Không chặn thao tác nào |
| Xung đột sync | Tự gộp; chỉ báo nếu phải bỏ một thay đổi (InfoBar + xem lịch sử) |
| Lỗi nghiêm trọng | Trang lỗi thân thiện, nút "Gửi báo lỗi" (đính log nếu người dùng đồng ý), nút "Mở thư mục dữ liệu" |
| Qua ngày mới khi app đang mở | Tự làm mới; nếu hôm qua chưa xong mà có freeze thì hỏi "Dùng freeze cho hôm qua?" |
