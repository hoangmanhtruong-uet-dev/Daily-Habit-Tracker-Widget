# ĐẶC TẢ UI CHI TIẾT: HABITGRID (Daily Habit Tracker Widget)

> Phiên bản 1.0 • 22/10/2026 • Bổ sung cho `habit-tracker-widget-spec.md` (thay thế mục 6 về mặt thị giác)
> Dùng để giao cho AI coding theo từng gói UI-01…UI-10 ở Phần 14. Stack: .NET 8 + WPF + WPF-UI.

---

## 0. VÌ SAO UI HIỆN TẠI "CHÁN" VÀ HƯỚNG SỬA

| Vấn đề thường gặp | Cách sửa trong spec này |
|---|---|
| Mọi thứ phẳng, xám, cùng một độ sâu | 3 lớp độ sâu rõ ràng (nền → surface → nổi) + ánh sáng (glow) có chủ đích |
| Màu nhấn dùng tràn lan | Chỉ 1 màu nhấn + màu heatmap; còn lại là trung tính ấm |
| Font một cỡ, một độ đậm | Thang chữ 7 bậc, số liệu (streak) dùng chữ số lớn đậm tạo điểm nhấn |
| Không có chuyển động hoặc chuyển động giật | Bộ motion thống nhất: easing, thời lượng, stagger có công thức |
| Layout giống form nhập liệu | Mỗi trang có 1 "nhân vật chính" (hero) và khoảng thở rộng |
| Không có cá tính | Ngôn ngữ thị giác "Ô sáng": mọi thứ xoay quanh những ô vuông phát sáng nhẹ trên nền tĩnh |

### 0.1 Ý tưởng thiết kế: "ÔSÁNG" (Glow Cells)
- **Dark mode là bản chính** (hợp desktop, widget nổi trên hình nền): nền gần đen ngả xanh, ô hoàn thành **phát sáng nhẹ** như đèn LED nhỏ.
- **Light mode = "giấy ấm"**: nền kem ấm, ô như mực in, không phát sáng nhưng có bóng đổ rất nhẹ.
- **Hình khối**: ô vuông bo góc mềm là họa tiết lặp lại ở mọi nơi: nút, chip, loading, empty state, icon app.
- **Giọng điệu**: tối giản, nhiều khoảng trắng, chữ số lớn, vi chuyển động nhẹ nhàng, không emoji (dùng icon Fluent).

---

## 1. DESIGN TOKENS (nguồn sự thật duy nhất)

Tất cả khai báo trong `Themes/Tokens.xaml` + `Themes/Dark.xaml` + `Themes/Light.xaml` (ResourceDictionary), **cấm hardcode màu/cỡ ở nơi khác**.

### 1.1 Màu (Dark / Light)
| Token | Dark | Light | Dùng cho |
|---|---|---|---|
| `Bg.Base` | #0E1014 | #F6F4EF | Nền cửa sổ quản lý |
| `Bg.Surface` | #161A21 | #FFFFFF | Thẻ, panel |
| `Bg.Surface2` | #1D222B | #EFECE5 | Thẻ lồng, ô nhập, hover nền |
| `Bg.Elevated` | #242A35 | #FFFFFF | Popup, dialog, tooltip |
| `Stroke.Subtle` | #262C37 | #E4E0D6 | Viền thẻ |
| `Stroke.Strong` | #3A4251 | #CFCABD | Viền ô nhập focus-less, divider |
| `Text.Primary` | #EEF0F5 | #1B1D22 | Chữ chính (tương phản ≥ 12:1) |
| `Text.Secondary` | #9BA3B3 | #6A6F7E | Chữ phụ (≥ 4.6:1) |
| `Text.Tertiary` | #6B7384 | #9A9DAA | Gợi ý, nhãn nhỏ (chỉ dùng ≥ 12px) |
| `Accent` | #6EE7B7 (mint) | #0F9D6B | Nút chính, focus, ô hôm nay |
| `Accent.Hover` | #8DF0C7 | #0B8459 | |
| `Accent.OnAccent` | #07251A | #FFFFFF | Chữ trên nền accent |
| `Danger` | #FF6B7A | #D93A4C | Xóa, lỗi |
| `Warning` | #FFC857 | #B97A00 | Cảnh báo |
| `Pro.Gradient` | #FFD27A → #FF9F6B → #F472B6 | giống | Chỉ dùng cho nhãn/nút Pro |
| `Overlay.Scrim` | #000000 @ 55% | #1B1D22 @ 35% | Nền mờ phía sau dialog |

### 1.2 Thang màu heatmap (5 mức)
| Mức | Dark (kèm glow) | Light |
|---|---|---|
| 0 trống | #232834 | #E7E3DA |
| 1 | #1E6B49 | #BFE9CF |
| 2 | #2FA36B | #82D3A3 |
| 3 | #47D18B | #45B079 |
| 4 | #86F5B8 (glow 8px @ 45%) | #1F7D4D |
- Preset Pro khác: sinh theo công thức OKLCH, giữ độ sáng L tăng đều (≈ 0.35 / 0.5 / 0.65 / 0.8 ở dark), chroma cố định, kiểm tra tương phản mức 1 vs mức 0 ≥ 1.6:1 và mức 4 vs nền ≥ 3:1.
- Ô hôm nay: viền 1.5px `Accent` + vòng ngoài 2px @ 30% (halo).

### 1.3 Typography (Segoe UI Variable, dự phòng Segoe UI)
| Token | Cỡ/Dòng | Đậm | Dùng |
|---|---|---|---|
| `Display` | 40/44 | Semibold (600), tracking -1% | Con số streak trang chi tiết |
| `Title1` | 28/34 | Semibold | Tiêu đề trang |
| `Title2` | 20/26 | Semibold | Tiêu đề thẻ/dialog |
| `Body` | 14/20 | Regular | Nội dung |
| `BodyStrong` | 14/20 | Semibold | Nhấn |
| `Caption` | 12/16 | Regular | Nhãn, tooltip, nhãn tháng |
| `Numeric` | 14/20, `tabular-nums` | Semibold | Số trong chip, thống kê |
- Tiêu đề widget: 13px Semibold. Nhãn tháng trong lưới: 10.5px, `Text.Tertiary`.
- Không dùng in nghiêng; không quá 2 độ đậm trên một màn hình.

### 1.4 Khoảng cách, bo góc, viền
- Spacing: 4 / 8 / 12 / 16 / 24 / 32 / 48. Padding thẻ chuẩn 20px; khoảng cách giữa thẻ 16px.
- Radius: ô lưới 3px • nút/ô nhập 8px • thẻ 14px • dialog 18px • widget 14px • chip 999px.
- Viền thẻ 1px `Stroke.Subtle`; có thêm "viền sáng trên" (inner top highlight 1px, trắng @ 6% ở dark) tạo cảm giác kính.

### 1.5 Độ sâu (elevation)
| Cấp | Dùng | Dark | Light |
|---|---|---|---|
| E0 | Nền | không | không |
| E1 | Thẻ | viền subtle, không bóng | bóng 0 1 2 #1B1D22@6% |
| E2 | Widget, dropdown | bóng 0 8 24 #000@45% | 0 6 20 #1B1D22@12% |
| E3 | Dialog, popup | 0 20 60 #000@60% + scrim | 0 16 48 #1B1D22@18% |

### 1.6 Motion (bộ chuyển động thống nhất)
| Token | Giá trị | Dùng |
|---|---|---|
| `Ease.Out` | cubic-bezier(0.2, 0.8, 0.2, 1) | Xuất hiện, mở rộng |
| `Ease.InOut` | cubic-bezier(0.4, 0, 0.2, 1) | Di chuyển, đổi trạng thái |
| `Ease.Spring` | overshoot nhẹ 1.08 → 1 | Ô được đánh dấu, nút Pro |
| `Dur.Fast` | 120ms | Hover, nhấn |
| `Dur.Base` | 200ms | Đổi trạng thái, popup |
| `Dur.Slow` | 360ms | Chuyển trang, lưới xuất hiện |
| `Stagger` | 6ms mỗi ô theo đường chéo | Lưới vẽ lần đầu |
- Tuân thủ cài đặt "Hiệu ứng động" của Windows: tắt → thời lượng 0, chỉ đổi màu tức thì.
- Chỉ animate `opacity`, `transform` (scale/translate), màu. Không animate layout liên tục.
- 60fps; control lưới vẽ bằng `DrawingContext`, animation từng ô chạy bằng một `CompositionTarget.Rendering` duy nhất.

### 1.7 Icon & hình ảnh
Fluent System Icons (regular 20px, filled cho trạng thái chọn). Độ dày nét thống nhất 1.5px. Minh họa empty state vẽ bằng chính các ô lưới (không dùng clip-art).

---

## 2. TRANG 1: WIDGET TRÊN DESKTOP (trang quan trọng nhất)

### 2.1 Bố cục (cỡ M, 26 tuần)
```
┌──────────────────────────────────────────────────────┐ radius 14, E2
│  [▣ icon 28]  Đọc sách               ⌁ 12   [ ✓ ]    │ header 44px
│               Mỗi ngày                                │ phụ đề Caption (tùy chọn)
│                                                       │
│        Th9    Th10   Th11                             │ nhãn tháng 10.5px
│  T2   ▢▢▣▣▢▣▣▢▣▣▢▣▣▢▣▣▢▣▣▢▣▣▢▣▣▢                      │
│  T4   ▣▢▣▣▣▢▣▣▣▢▣▣▣▢▣▣▣▢▣▣▣▢▣▣▣                      │ 7 hàng
│  T6   ▢▣▣▢▣▣▢▣▣▢▣▣▢▣▣▢▣▣▢▣▣▢▣▣▢                      │
│                                                       │
│  Ít ▢▢▢▢▢ Nhiều                    Dài nhất: 31 ngày  │ footer 24px (chỉ cỡ M, L)
└──────────────────────────────────────────────────────┘
```
- Padding trong: 14px (S: 12px). Khoảng header→lưới 10px.
- Header: icon 28px nằm trong "ô nền" 32×32 radius 9, màu `Accent` @ 16%; tên 13px Semibold; chip streak bên phải; nút ✓ cuối.
- **Chip streak**: pill cao 24px, icon ngọn lửa/streak Fluent 14px + số `Numeric`. Nền `Accent` @ 14%, chữ `Accent`. Streak ≥ 7: viền gradient mảnh; ≥ 30: thêm glow nhẹ; ≥ 100: icon đổi sang filled.
- **Nút ✓ "Hôm nay"**: tròn 28px. Chưa xong: viền `Stroke.Strong`, icon xám. Đã xong: nền `Accent`, icon `Accent.OnAccent`, có glow. Count: vòng tiến độ (progress ring 2px) quanh nút, số `3/8` hiện ở tooltip.
- Footer chú giải "Ít … Nhiều" 5 ô 9px; chữ Caption `Text.Tertiary`. Cỡ S ẩn footer và nhãn thứ.

### 2.2 Kích thước ô
| Cỡ | Tuần | Ô | Gap | Kích thước ngoài |
|---|---|---|---|---|
| S | 13 | 14 | 3 | 260×196 |
| M | 26 | 14 | 3 | 480×236 |
| L | 52 | 12 | 3 | 820×256 |

### 2.3 Nền widget (3 kiểu, đã có ở spec chính)
1. **Kính mờ (mặc định)**: Acrylic/blur thủ công, tint `Bg.Surface` @ 72% (độ mờ chỉnh được), viền 1px trắng @ 8% (dark) / đen @ 6% (light), inner top highlight.
2. **Đặc**: `Bg.Surface` 100%.
3. **Không nền**: chỉ ô; header chữ có bóng chữ nhẹ (0 1 3 #000@50%) để đọc được trên mọi hình nền. Thêm tùy chọn tự đảo màu chữ theo độ sáng ảnh nền (lấy mẫu 1 lần khi khởi động).
- Acrylic trên widget con của WorkerW có thể không khả dụng: fallback sang nền bán trong suốt đặc + vẽ noise 2% để tránh banding.

### 2.4 Trạng thái và vi chuyển động
| Trạng thái | Biểu hiện |
|---|---|
| Xuất hiện lần đầu | Widget fade-in + trượt lên 8px (360ms Ease.Out); lưới vẽ theo đường chéo, stagger 6ms/ô |
| Hover ô | Ô sáng +12%, phóng 1.0 → 1.12 (Dur.Fast), tooltip sau 350ms |
| Nhấn ô | Thu 0.88 (80ms) |
| Đánh dấu xong | Ô bật màu mức tương ứng + **pop spring** 1.0 → 1.25 → 1.0 (220ms) + vòng sóng (ripple) 0 → 14px, mờ dần 400ms; chip streak đếm lên (số cuộn dọc 160ms); nút ✓ thành filled |
| Bỏ đánh dấu | Ô co 0.9 → 1.0, màu chuyển về trống 160ms, không ripple |
| Count +1 | Ô đổi mức (nếu qua ngưỡng), số trong tooltip nhảy; đạt mục tiêu: hiệu ứng "hoàn thành" (như xong) kèm vòng ring đầy |
| Đạt mốc streak 7/30/100/365 | Chip bùng sáng 600ms + 8 hạt nhỏ bắn ra từ chip (confetti ô vuông 3px, 700ms). Chỉ chạy 1 lần mỗi mốc, tắt được trong Cài đặt |
| Ô hôm nay | Viền accent + halo thở chậm (opacity 30% ↔ 55%, chu kỳ 3s). Tắt khi giảm hiệu ứng |
| Ô tương lai | Mờ 30%, con trỏ cấm, không hover |
| Ô ngoài lịch | Chấm 4px ở giữa, không phải ô đặc |
| Lỗi ghi DB | Widget rung ngang 4px × 3 lần (240ms), toast lỗi |
| Chỉ đọc (khóa Pro) | Phủ mờ `Bg.Surface` @ 55% + icon khóa 20px giữa + nhãn nhỏ "Mở khóa Pro" (gradient Pro); hover → sáng nút |
| Chế độ chỉnh sửa | Viền nét đứt accent chạy (dash offset animate 8s), 4 tay nắm tròn 10px ở góc, nút ✕ ẩn widget ở góc trên phải, con trỏ dạng di chuyển, widget nổi E3 nhẹ |
| Kéo thả | Bóng E3, phóng 1.015, độ trong 92%; đường gióng (guide) accent 1px khi snap lưới 8px/mép màn hình |

### 2.5 Tooltip ô
- Nền `Bg.Elevated`, radius 10, E3, padding 8×12. Mũi tên 6px.
- Dòng 1: ngày đầy đủ (`BodyStrong`). Dòng 2: trạng thái kèm chấm màu mức (`Caption`, `Text.Secondary`), ví dụ "● Đã xong", "● 5/8 ly".
- Hiện 120ms fade + dịch 4px; ẩn 80ms.

### 2.6 Mini popup (Count: Shift+click/chuột phải)
Kích thước 220×~150, E3, radius 14. Gồm: tiêu đề ngày; stepper `−  [ 5 ]  +` (nút tròn 36px, số `Display` 32px); thanh tiến độ mỏng 4px; hai nút phụ chữ: "Đặt về 0", "Hoàn thành mục tiêu". Mở: scale 0.96 → 1 + fade 180ms từ điểm click. Đóng bằng Esc/click ngoài.

---

## 3. TRANG 2: CỬA SỔ QUẢN LÝ (shell chính)

### 3.1 Khung cửa sổ
- Kích thước mặc định 1040×680, tối thiểu 820×560. Title bar tùy biến (Mica nếu Win11, nền `Bg.Base` nếu Win10), nút hệ thống tự vẽ đúng kiểu Windows.
- Bố cục 2 cột: **Sidebar 248px** + **Vùng nội dung** (padding 32px, max-width nội dung 960px canh giữa khi cửa sổ rộng).

```
┌────────────────────────────────────────────────────────────┐
│ ▣ HabitGrid                                      ─  ▢  ✕   │ title bar 40px
├──────────────┬─────────────────────────────────────────────┤
│  [Tìm kiếm]  │  Hôm nay · Thứ Tư, 22 tháng 10              │
│              │  ┌ Hero: tiến độ hôm nay ┐                  │
│  ● Hôm nay   │  │  3/5 xong   ●●●○○     │                  │
│  ○ Thói quen │  └────────────────────────┘                  │
│  ○ Thống kê  │  Danh sách thẻ habit (lưới 2 cột)            │
│  ○ Lưu trữ   │  ┌──────────┐ ┌──────────┐                  │
│              │  │ habit    │ │ habit    │                  │
│  ──────────  │  └──────────┘ └──────────┘                  │
│  ⚙ Cài đặt   │                                             │
│  ★ Pro       │                                             │
└──────────────┴─────────────────────────────────────────────┘
```

### 3.2 Sidebar
- Nền `Bg.Surface`, viền phải 1px subtle. Logo (lưới 3×3 với 1 ô accent) + tên app ở trên; ô tìm kiếm radius 10, cao 36.
- Mục điều hướng: cao 40, radius 10, icon 20 + chữ Body. Chọn: nền `Accent` @ 14%, chữ `Accent`, **thanh chỉ báo 3px×18px bên trái** trượt mượt giữa các mục (Ease.InOut 200ms). Hover: nền `Bg.Surface2`.
- Dưới cùng: Cài đặt; thẻ "Pro" cao 64 nền gradient mờ (Pro.Gradient @ 14%), viền gradient 1px, icon vương miện/sao, chữ "Mở khóa Pro". Ẩn thẻ này nếu đã Pro, thay bằng nhãn "Pro ✓" nhỏ.
- Thu gọn dưới 900px: sidebar còn 64px (chỉ icon, tooltip tên).

### 3.3 Trang "Hôm nay" (trang mặc định)
1. **Hero tiến độ** (thẻ cao 132, radius 14, nền gradient rất nhẹ accent @ 10% → trong suốt):
   - Trái: lời chào theo giờ (`Title1`) + câu phụ "Còn 2 thói quen nữa là hoàn thành hôm nay" (`Body`, secondary).
   - Phải: vòng tiến độ tròn 88px (ring 8px, bo đầu tròn, accent; nền ring `Bg.Surface2`) với "3/5" ở giữa (`Title2`). Khi 100%: ring đổi gradient + hiệu ứng confetti ô vuông 1 lần, câu chào đổi "Hôm nay xong rồi. Giỏi lắm."
2. **Lưới thẻ habit hôm nay** (2 cột, gap 16): mỗi thẻ cao 96:
   - Trái: nút tròn đánh dấu 40px (hành vi như nút ✓ widget).
   - Giữa: tên (`BodyStrong`), dòng phụ: streak + lịch ("Mỗi ngày", "T2–T6").
   - Phải: mini-lưới 7 ngày gần nhất (7 ô 10px) cho cảm giác "lướt nhìn".
   - Hoàn thành: nền thẻ chuyển accent @ 8%, tên gạch nhẹ? **Không gạch**; thay bằng icon check nhỏ cạnh tên.
   - Hover: nổi E2, dịch lên 2px (Dur.Fast). Click vào thẻ (ngoài nút) → mở trang chi tiết.
3. Habit ngoài lịch hôm nay: nhóm "Không có lịch hôm nay" thu gọn, chữ phụ.
4. Nút "+ Thêm thói quen": thẻ viền nét đứt `Stroke.Strong` cuối lưới, hover viền accent, biểu tượng + xoay 90° khi hover.
- Chuyển vào trang: các thẻ stagger fade-up 24ms mỗi thẻ (Ease.Out), tối đa 8 thẻ.

### 3.4 Trang "Thói quen" (quản lý)
- Danh sách dạng hàng (không phải thẻ) cao 72, kéo thả sắp xếp: tay nắm 6 chấm bên trái hiện khi hover; khi kéo hàng nổi E3, các hàng khác dịch mượt 160ms.
- Cột: icon-ô • tên + lịch • streak • công tắc "Hiện widget" (toggle Fluent) • nút ⋯ (Sửa, Nhân bản, Lưu trữ, Xóa).
- Đầu trang: `Title1` + bộ lọc dạng segmented (Tất cả / Đang chạy / Count / Boolean) + nút chính "+ Thêm".

### 3.5 Trang "Thống kê" (tổng hợp)
- Hàng 4 thẻ số: Tổng lần hoàn thành • Streak dài nhất • Tỷ lệ 30 ngày • Habit tốt nhất. Số `Display`, nhãn `Caption`. Đếm lên (count-up 600ms) khi vào trang.
- Heatmap tổng hợp (mọi habit gộp: màu theo % hoàn thành trong ngày) 52 tuần.
- Biểu đồ cột 12 tháng (cột bo góc trên 4px, cột hiện tại accent, còn lại accent @ 40%), có tooltip.

### 3.6 Trang "Lưu trữ"
Danh sách habit đã lưu trữ, nền thẻ mờ 70%; nút "Khôi phục" (phụ) và "Xóa hẳn" (danger, xác nhận). Empty: minh họa hộp lưu trữ bằng ô lưới + câu "Chưa có gì ở đây."

### 3.7 Empty state toàn app
Minh họa: lưới 7×10 các ô xám, 1 ô mint đang thở (halo). Tiêu đề `Title2` "Bắt đầu bằng một ô", mô tả 1 dòng, nút chính "Tạo thói quen đầu tiên". Dưới là 5 chip gợi ý nhanh (Uống nước, Đọc sách, Tập thể dục, Học tiếng Anh, Thiền): bấm chip tạo ngay.

---

## 4. TRANG 3: CHI TIẾT THÓI QUEN

### 4.1 Bố cục
```
← Thói quen          Đọc sách                [Sửa] [⋯]
┌──────────────────────────────────────────────────────┐
│ Hero: lưới 52 tuần cỡ lớn (ô 16px) trong thẻ rộng    │
└──────────────────────────────────────────────────────┘
┌───────────┐ ┌───────────┐ ┌───────────┐ ┌───────────┐
│ 12        │ │ 31        │ │ 248       │ │ 83%       │
│ Streak    │ │ Dài nhất  │ │ Tổng      │ │ 30 ngày   │
└───────────┘ └───────────┘ └───────────┘ └───────────┘
┌──────────────────────────┐ ┌─────────────────────────┐
│ Biểu đồ cột theo tháng   │ │ Theo thứ trong tuần     │
└──────────────────────────┘ └─────────────────────────┘
Lịch sử gần đây (danh sách 10 mục: ngày • giá trị • sửa)
```
### 4.2 Chi tiết
- **Tiêu đề trang**: nút quay lại tròn 36, icon + tên `Title1`, dưới là mô tả lịch/mục tiêu.
- **Hero lưới**: thẻ nền `Bg.Surface`, padding 24; ô 16px gap 4; nhãn tháng 11px; chọn một ô → hiện thanh bên dưới lưới ("Thứ Ba, 21/10 • [ Đánh dấu ]" hoặc stepper cho Count) trượt xuống 200ms thay vì mở popup.
- **Thẻ số liệu**: số `Display` màu `Text.Primary`; streak hiện tại dùng accent + icon streak; mỗi thẻ có đường sparkline 7 ngày mờ phía sau (opacity 8%).
- **Biểu đồ tháng**: cột rộng 20, bo 4, trục chữ `Caption`; hover cột sáng + tooltip. Biểu đồ "Theo thứ": 7 cột ngang nhỏ, cho thấy ngày nào hay làm nhất.
- Lịch sử: hàng 48, bấm vào để sửa giá trị inline.
- Vào trang: hero fade + lưới vẽ stagger; số đếm lên.

---

## 5. TRANG 4: HỘP THOẠI TẠO/SỬA THÓI QUEN

Dialog 520×(auto, tối đa 640), radius 18, E3, scrim phía sau. Mở: scale 0.97 → 1 + fade (200ms), đóng ngược lại 140ms.

### 5.1 Bố cục (cuộn nếu thấp)
1. **Xem trước trực tiếp** (trên cùng, thẻ cao 108): widget thu nhỏ cỡ S cập nhật theo mọi lựa chọn (tên, icon, màu). Giúp UI bớt "form khô".
2. **Tên**: ô nhập cao 44, radius 10, đếm ký tự `x/40` bên phải.
3. **Icon**: lưới 10 cột × 6 hàng icon 20px trong ô 40; chọn: viền accent + nền accent @ 14%; cuộn trong khung cao 132 có tìm kiếm.
4. **Loại**: segmented 2 nút lớn có minh họa nhỏ (Có/Không: 1 ô; Đếm: 4 ô đậm dần). Chọn Đếm → mục "Mục tiêu mỗi ngày" trượt mở (stepper).
5. **Lịch lặp**: 7 nút tròn 40px (T2…CN); chọn = nền accent; lối tắt "Mỗi ngày", "Ngày làm việc", "Cuối tuần" dạng chip.
6. **Màu**: hàng chấm tròn 28px cho preset (Pro khóa từ chấm thứ 2 trở đi: icon khóa nhỏ phủ lên); chấm "+" mở bộ chọn màu (Pro).
7. **Nhắc nhở**: công tắc; bật thì hiện chọn giờ (time picker tùy biến, không dùng hộp mặc định) + các thứ.
8. **Ngày bắt đầu**: chọn ngày dạng chip ("Hôm nay", "Chọn…").
- Chân dialog: trái "Hủy" (phụ), phải "Lưu" (chính, accent). Phím Enter lưu, Esc hủy. Lỗi hiện ngay dưới ô (đỏ, 12px, rung nhẹ).
- Sửa: thêm nút "Lưu trữ" và "Xóa" (danger, chữ, nằm góc trái chân dialog).

---

## 6. TRANG 5: CÀI ĐẶT

### 6.1 Bố cục
Cột trái 220px: danh sách nhóm (Chung, Giao diện, Widget, Nhắc nhở, Dữ liệu, Giấy phép, Giới thiệu). Cột phải: tiêu đề nhóm `Title1` + các **thẻ cài đặt** (mỗi thẻ gom 2–5 dòng).
- Mỗi dòng: cao 56; nhãn `Body` + mô tả `Caption` bên dưới; điều khiển bên phải (toggle, dropdown, slider, nút). Dòng cách nhau bằng divider subtle.
- Tìm kiếm cài đặt ở đầu trang (lọc tức thì).
- Thay đổi áp dụng ngay; hiện toast nhỏ "Đã lưu" ở góc dưới phải 1.5s (không chặn).

### 6.2 Trang "Giao diện" (trang cần đẹp nhất)
- **Chọn chủ đề**: 3 thẻ xem trước lớn 180×120 (Sáng / Tối / Hệ thống) mô phỏng widget thật bên trong; chọn: viền accent 2px + dấu check góc.
- **Kiểu nền widget**: 3 thẻ xem trước (Kính / Đặc / Không nền) trên nền ảnh mẫu.
- **Độ mờ**: slider với thanh nền gradient sọc caro để thấy độ trong suốt; hiển thị % ở bong bóng khi kéo.
- **Bo góc**: slider 0–20 và xem trước trực tiếp trên thẻ mẫu.
- **Màu nhấn** (Pro mở rộng): hàng chấm 8 màu.
- **Hiệu ứng ăn mừng**: toggle.

### 6.3 Trang "Widget"
Chọn chế độ ghim bằng **3 thẻ radio có sơ đồ minh họa** (mỗi thẻ vẽ cửa sổ xếp chồng thể hiện widget nằm ở tầng nào), thẻ đang dùng hiện nhãn "Đang dùng"; nếu fallback tự động xảy ra hiện banner vàng một lần giải thích. Dưới là kích thước mặc định (segmented S/M/L), "Đặt lại vị trí" (nút phụ), công tắc chế độ chỉnh sửa.

### 6.4 Trang "Dữ liệu" (Pro)
Hai thẻ lớn "Xuất" và "Nhập" có icon, mô tả ngắn, nút. Dưới: đường dẫn dữ liệu (chữ mono, có nút "Sao chép", "Mở thư mục"). Với Free: toàn bộ thẻ xuất/nhập phủ lớp khóa mờ và nút "Mở khóa Pro", phần đường dẫn dữ liệu vẫn dùng được.

### 6.5 Trang "Giấy phép"
Thẻ trạng thái lớn: Free (viền subtle) hoặc Pro (viền gradient + nhãn "Pro ✓", ngày xác minh cuối). Nút "Nâng cấp" / "Khôi phục giao dịch".

### 6.6 Trang "Giới thiệu"
Logo lớn 72, tên + phiên bản, 3 liên kết dạng nút chữ (Riêng tư, Hỗ trợ, Đánh giá), nút "Sao chép thông tin phiên bản".

---

## 7. TRANG 6: MÀN HÌNH NÂNG CẤP PRO

- Dialog lớn 760×520 (hai cột), radius 18, E3. Nền trái `Bg.Elevated`, nền phải gradient tối tinh tế (Pro.Gradient @ 10% trên nền).
- **Cột trái (nội dung)**: nhãn "PRO" (pill gradient, chữ đen 11px đậm), tiêu đề `Title1` "Mở khóa toàn bộ HabitGrid", 3 lợi ích mỗi dòng: icon trong ô 36 + tiêu đề `BodyStrong` + mô tả `Caption`. Giá `Display` 36px "4,99 USD" + chú thích "Mua một lần, dùng mãi mãi"; nếu khuyến mãi: giá gốc gạch + nhãn "-20%".
- **Cột phải (minh họa sống)**: một widget mẫu tự đổi bảng màu mỗi 2.5s (Xanh lá → Tím → Cam → Hồng) với chuyển màu cross-fade 600ms; bên dưới hàng chấm màu preset (bấm thử đổi ngay).
- **Nút chính** "Mở khóa Pro": cao 48, nền gradient Pro, chữ đen đậm, hiệu ứng ánh sáng quét ngang (shine) 1.2s mỗi 6s; hover nâng lên 2px + glow. Nút phụ "Khôi phục giao dịch" chữ. Dòng nhỏ "Thanh toán an toàn qua Microsoft Store".
- **Trạng thái**: đang xử lý (nút thành spinner 20px, khóa dialog) • thành công (nút → check, confetti ô vuông gradient 1 lần, dialog đóng sau 1.4s, thẻ Pro trong sidebar đổi sang "Pro ✓") • hủy (im lặng, về trạng thái ban đầu) • lỗi mạng (banner đỏ nhẹ trong dialog + "Thử lại").
- Không đếm ngược, không giả khan hiếm, không popup tự bật.

---

## 8. TRANG 7: ONBOARDING (≤ 30 giây, 4 bước)

Cửa sổ riêng 720×480, không viền hệ thống, góc bo 18, nền `Bg.Base`, chấm tiến trình 4 chấm ở dưới (chấm hiện tại giãn thành thanh 20px).
1. **Chào**: nền phía sau là lưới ô lớn mờ, các ô lần lượt sáng lên ngẫu nhiên (hiệu ứng "thở" nền, 8% opacity). Tiêu đề `Display` "Một ô mỗi ngày.", phụ đề 1 dòng, nút "Bắt đầu".
2. **Chọn thói quen đầu**: 5 thẻ gợi ý lớn (icon + tên), bấm chọn; hoặc ô "Tự nhập tên". Có xem trước widget bên phải cập nhật tức thì.
3. **Xem widget trên desktop**: bản chụp động desktop mô phỏng, widget rơi vào đúng vị trí (spring), bong bóng chỉ "Bấm vào ô hôm nay"; người dùng thử bấm thật vào ô (bước hoàn thành khi bấm, có pop + ripple).
4. **Tùy chọn**: hai công tắc (Nhắc nhở lúc nào, Chạy cùng Windows), đều có thể bỏ qua; nút "Xong" → cửa sổ thu về khay, widget hiện.
- Chuyển bước: nội dung trượt ngang 24px + fade (Dur.Slow Ease.InOut).

---

## 9. TRANG 8: TOAST NHẮC NHỞ & TOAST TRONG APP

### 9.1 Toast Windows (nhắc nhở)
- Dùng toast hệ thống để tôn trọng Focus Assist. Tiêu đề: tên habit; nội dung: câu ngắn, thân thiện, xoay vòng 5 biến thể (không lặp liên tiếp), kèm streak nếu có ("Giữ chuỗi 12 ngày nhé").
- Logo ứng dụng: icon habit hoặc logo app dạng ô vuông; ảnh hero tùy chọn không dùng.
- Nút: "Đã xong" (hành động nền, không mở app) • "Để sau 1 giờ".

### 9.2 Toast trong app (undo, đã lưu)
Pill nổi ở góc dưới phải, cao 44, radius 12, E3, nền `Bg.Elevated`, icon trạng thái 18 + chữ + nút chữ "Hoàn tác" (accent). Hiện: trượt lên 12px + fade 200ms; ẩn tự động sau 5s với thanh đếm mỏng 2px ở đáy chạy hết; xếp chồng tối đa 3, cái mới đẩy cái cũ lên.

---

## 10. TRANG 9: MENU KHAY & CÁC MENU NGỮ CẢNH

- Menu khay dùng kiểu Fluent tự vẽ (không dùng ContextMenu cũ): nền `Bg.Elevated`, radius 12, E3, padding 6, mục cao 36 radius 8, hover nền `Bg.Surface2`, icon 18, phím tắt chữ phụ bên phải; divider 1px.
- Đầu menu: dòng trạng thái nhỏ "3/5 hôm nay • Pro" (không click được).
- Icon khay: logo lưới 3×3; khi hôm nay còn habit chưa xong, thêm chấm accent 6px góc dưới phải; xong hết thì chấm biến mất.
- Mở menu: scale 0.97 + fade 140ms từ phía cạnh khay.

---

## 11. TRẠNG THÁI HỆ THỐNG DÙNG CHUNG

| Thành phần | Quy cách |
|---|---|
| Nút chính | Cao 40 (lớn 48), radius 10, nền `Accent`, chữ `Accent.OnAccent` Semibold; hover sáng hơn + glow 12px @ 25% (dark); nhấn thu 0.97; focus: vòng 2px `Accent` cách 2px |
| Nút phụ | Nền `Bg.Surface2`, viền subtle; hover viền `Stroke.Strong` |
| Nút chữ | Không nền, chữ accent; hover nền accent @ 10% |
| Nút danger | Giống nút phụ, chữ `Danger`; xác nhận xóa mới dùng nền `Danger` |
| Toggle | 40×22, thumb 16, bật = accent; thumb giãn khi nhấn (Fluent) |
| Ô nhập | Cao 40–44, nền `Bg.Surface2`, viền subtle; focus: viền accent 1.5px + glow 3px @ 20%; lỗi: viền `Danger` |
| Slider | Rãnh 4px, phần đã chọn accent, thumb tròn 18 có viền `Bg.Surface` |
| Dropdown | Danh sách nổi E3, mục cao 36, mục chọn có check |
| Skeleton | Khối `Bg.Surface2` + ánh sáng quét 1.2s (chỉ khi tải > 300ms) |
| Scrollbar | Mỏng 6px, tự ẩn, hover phình 10px |
| Con trỏ | Tay trên ô/nút bấm được; mũi tên ở nơi khác |

---

## 12. TRỢ NĂNG VÀ CÁC CHẾ ĐỘ ĐẶC BIỆT

- **Màu mù màu**: ô có thể bật "kiểu dấu": mức 1 chấm, 2 gạch chéo nhẹ, 3 gạch chéo dày, 4 đặc. Bảng màu xanh dương/cam.
- **High Contrast**: bỏ glow/acrylic, viền 2px hệ thống (`SystemColors`), ô đã xong dùng `Highlight`, ô trống dùng `Window` có viền.
- **Cỡ chữ hệ thống**: layout co giãn đến 200%; widget cho phép cỡ phóng, không cắt chữ (elide + tooltip).
- **Bàn phím**: vòng focus rõ ràng; trong lưới dùng phím mũi tên; thứ tự Tab hợp lý từng trang.
- **Narrator**: mỗi ô có `AutomationProperties.Name` "21 tháng 10, đã xong"; thông báo live region khi đánh dấu.
- **Giảm hiệu ứng**: tắt pop, ripple, confetti, halo thở, shine; giữ đổi màu tức thì.
- **Tiết kiệm pin**: khi pin yếu/tiết kiệm pin, tắt animation lặp (halo, shine, nền onboarding).

---

## 13. TÀI SẢN (ASSET) CẦN CHUẨN BỊ

| Hạng mục | Quy cách |
|---|---|
| Logo/icon app | Lưới 3×3 ô bo góc, 1 ô mint phát sáng; xuất 16/24/32/48/256 + tile Store (44, 71, 150, 310, 50, StoreLogo) |
| Icon khay | 16/20/24/32, phiên bản sáng và tối, có biến thể có chấm |
| Bộ icon habit | ~60 icon Fluent chọn lọc theo nhóm (sức khỏe, học tập, công việc, thói quen xấu cần bỏ, gia đình) |
| Minh họa empty state | Vẽ bằng code (ô lưới) để theo được chủ đề sáng/tối |
| Ảnh nền mẫu cho xem trước | 4 ảnh bản quyền sạch (tối, sáng, màu, đơn sắc) cho trang Giao diện và ảnh Store |
| Ảnh chụp Store | 6 ảnh 1920×1080: chèn tiêu đề ngắn phía trên, khung widget thật trên nền đẹp |
| Video 20–30s | Quay màn hình: cài → tạo habit → bấm ô (pop+ripple) → đổi màu Pro |

---

## 14. GÓI CÔNG VIỆC UI CHO AI CODING

Giao lần lượt, mỗi gói kèm Phần 1 (tokens) + mục liên quan. Yêu cầu AI: đọc code hiện có → đề xuất kế hoạch ngắn → làm → build + chụp ảnh màn hình để so với spec → báo cáo.

| Gói | Nội dung | Phụ thuộc |
|---|---|---|
| UI-01 | Hệ token: `Tokens.xaml`, `Dark.xaml`, `Light.xaml`, styles nút/ô nhập/toggle/slider/dropdown/scrollbar (Phần 1, 11); chuyển chủ đề runtime | không |
| UI-02 | Bộ motion: helper animation, bảng easing/duration, tôn trọng cài đặt hiệu ứng hệ thống, chế độ giảm hiệu ứng | UI-01 |
| UI-03 | Control lưới + widget (Phần 2): vẽ ô có glow, trạng thái, ripple/pop, chip streak, nút ✓, tooltip, mini popup, chế độ chỉnh sửa, nền kính/đặc/không nền | UI-01, 02 |
| UI-04 | Shell cửa sổ quản lý + sidebar + title bar (Phần 3.1–3.2) | UI-01, 02 |
| UI-05 | Trang Hôm nay + Thói quen + Lưu trữ + empty state (3.3, 3.4, 3.6, 3.7) | UI-03, 04 |
| UI-06 | Trang chi tiết + Thống kê tổng hợp + biểu đồ (Phần 4, 3.5) | UI-03, 04 |
| UI-07 | Dialog tạo/sửa có xem trước trực tiếp (Phần 5) | UI-03 |
| UI-08 | Cài đặt đầy đủ + trang Giao diện/Widget có thẻ xem trước (Phần 6) | UI-04 |
| UI-09 | Màn Pro + onboarding + toast + menu khay (Phần 7–10) | UI-03, 04 |
| UI-10 | Trợ năng, High Contrast, mù màu, hiệu năng, đánh bóng cuối (Phần 12) | tất cả |

---

## 15. TIÊU CHÍ NGHIỆM THU UI

1. Không còn giá trị màu/cỡ hardcode ngoài file tokens (kiểm bằng tìm kiếm trong mã).
2. Chuyển Sáng ↔ Tối tức thì, không nháy, mọi trang đúng bảng token.
3. Mọi tương tác của Phần 2.4 có hiệu ứng đúng thời lượng; tắt "Hiệu ứng động" của Windows thì hết hiệu ứng.
4. Tương phản chữ đạt WCAG AA ở cả hai chủ đề; mức 1 và mức 0 của heatmap phân biệt được.
5. Widget ở 100%, 150%, 200% DPI không mờ nét, không lệch ô.
6. Mỗi trang có ảnh chụp "trước/sau" so với spec; so ảnh bằng mắt từng mục ở Phần 2, 3, 7.
7. Mở app → thấy widget có hiệu ứng xuất hiện trong < 1,5 giây; hiệu ứng không làm CPU idle vượt ~1% (halo thở dừng khi widget bị che).
8. Một người lạ nhìn ảnh chụp widget phải nhận ra ngay cá tính của app (ô phát sáng trên nền tối).

---

## 16. QUYẾT ĐỊNH THIẾT KẾ CÒN MỞ

| # | Câu hỏi | Gợi ý |
|---|---|---|
| D-01 | Dark là mặc định hay theo hệ thống? | Theo hệ thống, nhưng ảnh Store và onboarding dùng dark |
| D-02 | Màu nhấn mint hay đổi sang màu khác để khác biệt hơn với các app xanh lá kiểu GitHub? | Giữ mint cho UI, heatmap mặc định có thể chuyển sang mint–lục lam thay vì xanh GitHub để tránh giống |
| D-03 | Có làm hiệu ứng confetti không? | Có nhưng nhỏ, 1 lần mỗi mốc, tắt được |
| D-04 | Acrylic thật cho widget con của WorkerW | Quyết định sau POC ghim; dự phòng nền bán trong suốt + noise |
