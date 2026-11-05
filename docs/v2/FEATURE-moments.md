# Khoảnh khắc, thẻ chia sẻ photobooth, widget khoảnh khắc

> Ý tưởng tham khảo từ Locket / "Locket Photobooth" (06/11/2026). Chỉ mượn ý tưởng: HabitGrid **không** đăng nhập hay đăng bài lên Locket hay mạng xã hội nào (Locket không có API công khai; công cụ tham khảo bắt người dùng nhập mật khẩu Locket — rủi ro lộ tài khoản và vi phạm điều khoản).

| Tính năng | Free | Plus |
|---|---|---|
| Ảnh khoảnh khắc khi check-in (camera hoặc chọn file) | 1 ảnh/ngày cho mỗi thói quen | Không giới hạn |
| Thẻ chia sẻ photobooth 1080×1920 (lưu PNG / sao chép) | Khung Mặc định, Kẹo hồng; có logo nhỏ | Cả 6 khung (+ Nửa đêm, Sương mai, Tím mộng mơ, Quỹ đạo), ẩn được logo |
| Widget khoảnh khắc trên desktop (ảnh mới nhất, bấm để xem "ngày này tháng trước") | — | Có |

**Kỹ thuật**
- Ảnh lưu ở `{thư mục dữ liệu}/moments/{habitId}/{yyyy-MM-dd}_{giờ}.jpg`, không đổi lược đồ CSDL; WidgetHost đọc trực tiếp. Ảnh được thu nhỏ tối đa 1600 px và mã hóa lại JPEG (bỏ EXIF như vị trí GPS). Chỉ nhận JPEG/PNG theo chữ ký file, tối đa 15 MB. Ảnh chỉ nằm trên máy (chưa đồng bộ; khi có sync v2.1 cần quyết định có đưa ảnh lên cloud không).
- Thẻ chia sẻ: ô ảnh, chuỗi, Buddy + cấp, lưới 12 tuần + tỷ lệ 30 ngày; render bằng `RenderTargetBitmap` ở 1080×1920. Lưu/sao chép bật huy hiệu `shared_card`.
- Widget khoảnh khắc: WidgetHost bật khi app chính ghi `plan.isPlus = 1` và người dùng không tắt `widgets.moment.enabled`; ghim desktop, nhớ vị trí, dựng lại khi Explorer khởi động lại.
- Test: kho ảnh (giới hạn theo gói, chặn file lạ/ngày tương lai, xóa chỉ trong kho, ngày này tháng trước), dịch vụ thẻ (khung Plus khi Free → khung mặc định, logo), smoke (2 ảnh, thẻ PNG > 10 KB), WidgetHost tự kiểm tra (widget hiện).

**Chưa làm:** chụp video, nhiều ảnh trong một thẻ (dải 4 ô ảnh), chia sẻ thẳng qua bảng Share của Windows, đồng bộ ảnh.
