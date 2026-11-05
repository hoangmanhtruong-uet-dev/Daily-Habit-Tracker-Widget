# SPEC-V2-08 — Xã hội: bạn bè, buddy, thử thách, share card

> Phát hành: share card [v2.0], còn lại [v2.2] • Phụ thuộc: SPEC-V2-03 (tài khoản), 06
> Nguyên tắc: **riêng tư mặc định, động viên thay vì so bì**. Không feed công khai, không bình luận tự do (tránh kiểm duyệt nội dung), chỉ reaction emoji cố định.

## 1. Share card [v2.0, không cần tài khoản]
| Loại | Kích thước | Nội dung |
|---|---|---|
| Heatmap thói quen | 1080×1080, 1080×1920 | Tên, icon, heatmap 52 tuần, streak, tỷ lệ, Buddy góc dưới |
| Ngày hoàn hảo | 1080×1920 | Danh sách thói quen hôm nay đã xong + Buddy ăn mừng |
| Mốc streak / huy hiệu | 1080×1080 | Số lớn + huy hiệu |
| Wrapped [v2.2] | 1080×1920 | Theo SPEC-V2-06 §7 |

- Dựng bằng `ShareCardRenderer` (Win2D) theo theme đang dùng; watermark nhỏ "HabitGrid" (Plus tắt được).
- Hành động: Sao chép ảnh, Lưu file, Windows Share sheet. Không tải ảnh lên server.
- Tùy chọn ẩn tên thói quen (chỉ hiện icon) trước khi xuất.

## 2. Bạn bè [v2.2]
- Kết bạn bằng: link mời `habitgrid://invite/<code>` (kèm link web dự phòng), mã 6 ký tự, QR. Mã hết hạn 7 ngày.
- Hồ sơ công khai tối thiểu: tên hiển thị, avatar (hoặc Buddy), level, streak tổng.
- Mỗi thói quen có cài đặt chia sẻ: **Riêng tư** (mặc định) / **Bạn bè**. Thói quen "Bạn bè" chỉ chia sẻ: tên, icon, đã xong hôm nay chưa, streak. Không bao giờ chia sẻ giá trị chi tiết, ghi chú, mood.
- Danh sách bạn tối đa 50 (Free 10).
- Chặn / hủy kết bạn bất kỳ lúc nào, phía kia không nhận thông báo.

## 3. Buddy đồng hành (accountability partner)
- Ghép 1–1 trên **một thói quen** của mỗi bên (có thể khác tên, ví dụ "Gym" và "Chạy bộ").
- Thấy trạng thái hôm nay của nhau trên trang Bạn bè và widget Squad.
- **Nudge**: nút "Nhắc nhẹ" khi bạn chưa xong sau 18h; tối đa 1 nudge/người/ngày; người nhận nhận toast "Linh đang chờ bạn tập gym 💪". Tắt nhận nudge được.
- Streak chung: số ngày cả hai cùng xong; mốc 7/30/100 có huy hiệu chung.

## 4. Thử thách nhóm
- Chủ phòng tạo: tên, thói quen mẫu (loại, mục tiêu, tần suất), thời gian 7–90 ngày, tối đa 20 người (Free tham gia được, tạo thử thách: Plus).
- Thành viên tham gia → app tạo thói quen tương ứng trong danh sách của họ (gắn `SharedChallengeId`); check-in bình thường, tiến độ tự đồng bộ lên thử thách.
- Bảng tiến độ: mỗi người một hàng heatmap của thời gian thử thách + % hoàn thành. Sắp xếp theo % nhưng **không** hiển thị thứ hạng số để giảm áp lực; chỉ đánh dấu "Hoàn thành thử thách" cho ai ≥ 80%.
- Reaction emoji cố định (🔥👏💪🎉😴) lên check-in của người khác.
- Kết thúc: card tổng kết chia sẻ được, huy hiệu `challenge_won` cho người ≥ 80%.

## 5. Thông báo xã hội
Qua WNS (toast) và trong app: lời mời kết bạn, nudge, bạn đạt mốc streak, thử thách sắp bắt đầu/kết thúc, reaction (gộp, tối đa 1 toast/giờ). Mỗi loại tắt riêng được. Giờ yên lặng áp dụng.

## 6. An toàn & lạm dụng
- Không nhắn tin tự do, không bình luận → không cần kiểm duyệt nội dung người dùng ngoài **tên hiển thị, tên thử thách, avatar**: lọc từ ngữ xấu (danh sách VI/EN), avatar qua kiểm duyệt ảnh tự động hoặc chỉ cho chọn Buddy/emoji ở v2.2.
- Báo cáo người dùng/thử thách → hàng đợi xử lý thủ công.
- Giới hạn tạo lời mời/thử thách để chống spam.
- Người dưới 16 tuổi: không có kiểm tra tuổi ở v2.2; vì vậy giữ thiết kế không có kênh liên lạc tự do.

## 7. Tiêu chí nghiệm thu
1. Thói quen riêng tư không bao giờ xuất hiện trong bất kỳ API social nào (test).
2. Nudge giới hạn đúng 1/người/ngày, tôn trọng tắt nhận và giờ yên lặng.
3. Thử thách 20 người × 90 ngày: bảng tiến độ tải < 500 ms.
4. Share card xuất đúng kích thước, đẹp ở mọi theme (duyệt ảnh chụp từng theme).
5. Chặn người dùng: hai bên không còn thấy nhau ở mọi màn hình.
