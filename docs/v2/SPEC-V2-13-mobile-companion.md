# SPEC-V2-13 — App mobile companion (định hướng)

> Phát hành: Giai đoạn 4 (sau v2.2) • Tài liệu này chỉ chốt định hướng để v2.0–2.2 không đi ngược; đặc tả chi tiết viết khi bắt đầu giai đoạn.

## 1. Mục tiêu
Check-in khi rời máy tính, nhận nhắc trên điện thoại, widget màn hình chính iOS/Android. Dùng chung tài khoản, dữ liệu, Plus.

## 2. Lựa chọn công nghệ (chốt khi bắt đầu giai đoạn)
| Phương án | Ưu | Nhược |
|---|---|---|
| **.NET MAUI** (khuyến nghị mặc định) | Dùng lại trực tiếp Core, Contracts, Sync, MergeEngine bằng C# | Widget màn hình chính vẫn phải viết native (Swift/Kotlin) |
| Flutter | UI đẹp, mượt, cộng đồng lớn | Phải viết lại Core/merge bằng Dart, nguy cơ lệch logic |
| Native (Swift + Kotlin) | Trải nghiệm tốt nhất | Gấp đôi công sức |

Lý do khuyến nghị MAUI: logic streak/merge là phần dễ sai nhất, chạy cùng một mã nguồn trên mọi nền tảng là lợi thế lớn.

## 3. Phạm vi MVP mobile
- Đăng nhập (Apple, Google, Microsoft, email), sync.
- Trang Hôm nay, chi tiết thói quen, tạo/sửa.
- Nhắc nhở qua APNs/FCM do server gửi + local notification.
- Widget màn hình chính: Hôm nay, Streak.
- Buddy, XP, huy hiệu (đọc từ dữ liệu chung).
- Mua Plus qua App Store / Google Play → server entitlement (SPEC-V2-10 §4).

## 4. Những gì v2.0–2.2 MUST giữ để mobile làm được
1. Core, Contracts, Sync không phụ thuộc Windows (đã quy định ở SPEC-V2-01/02).
2. API không có giả định riêng Windows (ví dụ push channel là trường chung `push_token` + `push_provider`).
3. Tài sản Buddy, theme, icon ở định dạng đa nền tảng (vector, JSON).
4. Chuỗi đa ngôn ngữ có thể xuất sang định dạng khác (giữ khóa ổn định).
