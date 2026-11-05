# SPEC-V2-06 — Động lực & game hóa

> Phát hành: [v2.0] (XP, level, huy hiệu, Buddy, freeze, ăn mừng), [v2.2] (Wrapped) • Phụ thuộc: SPEC-V2-01, 04
> Nguyên tắc: **thưởng nhất quán, không phạt nặng**; không có tiền ảo mua bằng tiền thật; không cơ chế gây nghiện kiểu loot box.

## 1. XP
| Sự kiện | XP | Ghi chú |
|---|---|---|
| Check-in thói quen hoàn thành (Done) | 10 | Một lần/thói quen/ngày; bỏ check thì thu hồi |
| Partial (đếm/thời lượng đạt ≥ 50%) | 4 | |
| Ngày hoàn hảo (mọi thói quen có lịch đều xong) | +20 | |
| Mốc streak 7 / 30 / 100 / 365 | +50 / +150 / +500 / +2000 | Một lần mỗi mốc mỗi thói quen |
| Thêm ghi chú hoặc mood | +2 | Tối đa 10 XP/ngày từ nguồn này |
| Hoàn thành phiên Focus ≥ 25 phút | +5 | |
| Thử thách nhóm hoàn tất [v2.2] | +100 | |

- Trần: 200 XP/ngày (chống spam bằng cách tạo nhiều thói quen nhỏ).
- Sửa dữ liệu ngày cũ (> 2 ngày) **không** tạo XP (tránh gian lận), nhưng streak vẫn tính đúng.
- XP lưu dạng sự kiện (`xp_events`) để tính lại được khi đổi quy tắc.

## 2. Level
- Công thức: XP cần cho level n = `round(50 × n^1.6)`; level tối đa 100.
- Mốc level mở: hình dạng Buddy (tiến hóa ở level 5, 15, 30, 50), cosmetics, theme (một số theme Free mở khóa bằng level, giúp người không trả tiền vẫn có thứ để đạt).
- Lên level: modal nhỏ có Buddy ăn mừng + confetti + món vừa mở khóa.

## 3. Grid Buddy (linh vật)
- Mỗi người dùng một Buddy: tên, màu, giai đoạn tiến hóa (Trứng → Bé → Teen → Trưởng thành → Huyền thoại).
- **Năng lượng hôm nay** = % hoàn thành hôm nay → biểu cảm: ngủ (0%, buổi sáng), bình thường, vui (≥ 50%), rất vui (100%).
- Không bao giờ "chết" hay ốm vì người dùng bỏ app. Bỏ lâu thì Buddy chỉ "ngủ say" và mừng khi người dùng quay lại ("Welcome back" + tặng 1 freeze nếu vắng ≥ 7 ngày).
- Cosmetics: mũ, kính, khăn, phụ kiện cầm tay, nền phòng. Mở bằng level/huy hiệu; gói Plus có bộ riêng mỗi mùa.
- Buddy xuất hiện ở: trang Hôm nay, trang Buddy, widget Buddy, Widgets Board, share card, Wrapped.
- Tài sản đồ họa: vector (Rive/Lottie) để co giãn mọi DPI; mỗi animation ≤ 200 KB.

## 4. Streak freeze
- Nhận: 1 freeze mỗi 7 ngày giữ streak tổng; level up tặng 1; tối đa giữ 3 (Free) / 5 (Plus).
- Dùng: tự động áp cho ngày bị lỡ nếu bật "Tự dùng freeze", hoặc hỏi khi mở app ngày hôm sau. Ngày dùng freeze hiển thị màu `state/freeze` với icon bông tuyết.
- Freeze áp cho **streak tổng** và mọi thói quen bị lỡ ngày đó (một token/ngày, không phải mỗi thói quen).

## 5. Huy hiệu (30 cái ở v2.0)
Nhóm và ví dụ (key cố định, tên dịch được):
- **Bắt đầu**: `first_checkin`, `first_perfect_day`, `three_habits`, `first_note`.
- **Streak**: `streak_7`, `streak_30`, `streak_100`, `streak_365`, `comeback` (quay lại sau khi mất streak ≥ 7 và giữ lại 7 ngày).
- **Khối lượng**: `checkins_100`, `checkins_1000`, `focus_10h`, `water_100_glasses`.
- **Thời điểm**: `early_bird` (10 lần check trước 7h), `night_owl` (10 lần sau 23h), `weekend_warrior`.
- **Khám phá**: `widget_pinned`, `quick_checkin_used`, `theme_changed`, `shared_card`.
- **Bí mật** (ẩn tới khi đạt): 3 cái, ví dụ check-in đúng 00:00.
- **[v2.2] Xã hội**: `first_friend`, `buddy_7_days`, `challenge_won`.

Huy hiệu có 3 bậc (đồng/bạc/vàng) với nhóm khối lượng. Điều kiện định nghĩa bằng dữ liệu (JSON) đọc bởi `IAchievementEngine` để thêm huy hiệu không cần đổi code logic.

## 6. Ăn mừng
| Sự kiện | Hiệu ứng |
|---|---|
| Check-in | Ô nảy + glow; số XP bay lên "+10" |
| Ngày hoàn hảo | Confetti (màu theo theme) + chime + Buddy nhảy |
| Mốc streak | Toàn màn hình nhẹ 1,5 s: số streak lớn + lửa, nút "Chia sẻ" |
| Huy hiệu mới | Thẻ huy hiệu lật ra, nút "Xem" / "Chia sẻ" |
| Level up | Modal Buddy + món mở khóa |
Tôn trọng "Giảm chuyển động" và tắt âm.

## 7. Wrapped [v2.2]
- **Tháng** (ngày 1 hằng tháng) và **Năm** (từ 15/12).
- 6–8 "slide" dọc kiểu story: tổng check-in, thói quen số 1, streak dài nhất, giờ vàng (giờ check-in nhiều nhất), ngày hoàn hảo, Buddy đã lớn thế nào, "tính cách thói quen" (ví dụ "Cú đêm kỷ luật" dựa trên quy tắc đơn giản), lời nhắn tháng tới.
- Mỗi slide xuất ảnh 1080×1920 (story) để chia sẻ; slide cuối là card tổng hợp 1080×1080.
- Tính hoàn toàn trên máy (không cần server, không cần AI).

## 8. Tiêu chí nghiệm thu
1. `IXpEngine` và `IAchievementEngine` là hàm thuần trên dữ liệu; test tái tính toàn bộ XP từ nhật ký cho cùng kết quả.
2. Bỏ check thu hồi đúng XP; trần 200 XP/ngày được tôn trọng.
3. 30 huy hiệu có test điều kiện; huy hiệu bí mật không lộ điều kiện trong UI.
4. Freeze: các kịch bản lỡ 1 ngày, lỡ 2 ngày với 1 token, tự dùng/hỏi đều có test.
5. Animation ăn mừng không làm rớt khung hình trang Hôm nay (< 16 ms/khung trung bình).

## Tiến độ triển khai (05/11/2026)
Đã có (lõi thuần, có test): `XpEngine` (XP tính lại được từ nhật ký, trần 200/ngày, sửa muộn > 2 ngày không có XP, mốc streak 7/30/100/365), `LevelCurve`, `AchievementEngine` đọc **23 huy hiệu từ `achievements.json`** (3 bí mật), `FreezeEngine` (chỉ dùng token khi cứu được chuỗi). `GamificationService` lưu XP/level/huy hiệu, thưởng freeze (mỗi 7 ngày chuỗi tổng, mỗi lần lên level), áp freeze (một token/ngày cho mọi thói quen), lần tính đầu không ăn mừng. UI: trang **Buddy** (tiến hóa theo level 5/15/30/50, XP, freeze, lưới huy hiệu, bí mật không lộ điều kiện), hộp thoại lên cấp/huy hiệu mới/hỏi dùng freeze, công tắc "Tự dùng freeze".
Quyết định diễn giải: "XP cần cho level n = round(50·n^1.6)" được hiểu là **tổng XP để đạt level n** (level 100 ≈ 79 nghìn XP); cộng dồn từng bậc cần ~3 triệu XP, không đạt được với trần 200 XP/ngày.
**Chưa làm:** huy hiệu 3 bậc chỉ có dữ liệu bậc (chưa có huy hiệu đồng/bạc/vàng riêng cho nhóm khối lượng), 3 huy hiệu xã hội [v2.2] và Focus (focus_10h luôn 0 cho tới khi có Focus), Buddy dùng emoji (chưa có Rive/Lottie), cosmetics, biểu cảm theo năng lượng hôm nay ở trang Buddy, animation ăn mừng riêng (số XP bay, confetti theo mốc), widget Buddy, Wrapped [v2.2], đo khung hình (tiêu chí 5).
