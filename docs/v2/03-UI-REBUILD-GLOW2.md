# 03 — Rebuild UI/UX "Glow 2" (bỏ phong cách to-do list, hướng GenZ)

> Ngày: 05/11/2026 • Trạng thái: **kế hoạch, chờ duyệt hướng thiết kế** • Thay thế phần bố cục của 01-DESIGN-SYSTEM và 02-UX-SCREENS (token màu/chữ cũ vẫn dùng làm nền).
> Phạm vi: chỉ tầng giao diện (`HabitGrid.WinUI`). ViewModel ở `HabitGrid.Presentation` giữ nguyên; chỉ thêm vài trạng thái phục vụ bố cục mới.

---

## 1. Vì sao hiện tại trông giống Microsoft To Do

| Dấu hiệu | Hiện tại | Hệ quả |
|---|---|---|
| Điều hướng | `NavigationView` dạng danh sách chữ bên trái | Giống mọi app văn phòng Windows |
| Danh sách | Thẻ xám xếp dọc, một cột, icon nhỏ, vòng check bên phải | Đúng khuôn của một to-do list |
| Màu | Nền xám/Mica, màu thói quen chỉ xuất hiện ở icon 42 px và viền vòng | Cả màn hình gần như đơn sắc |
| Phản hồi | Bấm vòng → tick + nảy nhẹ | Không có cảm giác "thắng" |
| Chữ | Segoe mặc định, cỡ chuẩn | Không có cá tính |
| Không gian | Nhiều khoảng trống chết bên phải và bên dưới | Trông trống và "công sở" |

Mục tiêu Glow 2: **mở app lên là thấy màu, thấy tiến độ, thấy cá tính**. Mỗi thói quen là một "thẻ sưu tầm" có màu riêng; check-in là một khoảnh khắc ăn mừng nhỏ; màn hình chính là một bảng bento chứ không phải danh sách.

---

## 2. Nguyên tắc thiết kế

1. **Bento, không phải list.** Màn chính là lưới ô kích thước khác nhau (hero, streak, thói quen), không có dòng xếp dọc dài.
2. **Màu là chủ thể.** Mỗi thói quen có gradient 2 điểm sinh từ màu gốc; thẻ thói quen phủ màu toàn bộ khi xong.
3. **Một chạm vẫn là luật số 1.** Bấm vào bất kỳ đâu trên thẻ là check-in (không phải nhắm vào vòng tròn 40 px).
4. **Mỗi lần check-in là một khoảnh khắc.** Màu "đổ đầy" thẻ, tia sáng, số streak nhảy, âm thanh nhỏ (tắt được).
5. **Dark-first, light vẫn đẹp.** GenZ dùng dark nhiều; mọi màn thiết kế dark trước, light được kiểm tra song song.
6. **Chữ to, đậm, có cá tính.** Tiêu đề lớn, số liệu to; chữ phụ ít và ngắn.
7. **Chừa chỗ cho Buddy và XP ngay từ bây giờ** (SPEC-V2-06) để khi làm game hóa không phải đập bố cục lần nữa.
8. **Đẹp để chụp màn hình.** Mọi màn chính phải đủ đẹp để đăng story mà không cần chỉnh.

---

## 3. Ba hướng phong cách (chọn một làm chuẩn, hai hướng còn lại thành theme)

| | A. **Candy Bento** (đề xuất) | B. **Neon Night** | C. **Soft Pastel** |
|---|---|---|---|
| Nền | Tối sâu `#0E0E14` + 2–3 "đốm" gradient mờ lớn trôi rất chậm | Đen tuyền + lưới chấm mờ | Kem `#FBF7F2` + giấy nhám nhẹ |
| Thẻ thói quen | Gradient 2 điểm rực (ví dụ tím → hồng), bo 28 px | Viền neon phát sáng, ruột tối, xong thì phát sáng toàn thẻ | Pastel phẳng, viền đậm 2 px kiểu sticker |
| Cảm giác | Vui, rực, kiểu Apple Fitness × Finch | Gamer, cyber, kiểu Discord/Arc | Dễ thương, nhẹ nhàng, kiểu Notion aesthetic |
| Rủi ro | Dễ "rối" nếu không kiểm soát số màu | Kém đọc ở light mode | Ít "wow" khi chụp màn hình |

**Đề xuất:** dùng **A** làm giao diện mặc định; B và C trở thành theme trong bộ sưu tập (khớp với "theme thẩm mỹ" ở 01-DESIGN-SYSTEM §2.3). Kiến trúc theme ở §8 đảm bảo đổi theme chỉ là đổi bộ token.

---

## 4. Hệ thống thị giác mới (token bổ sung, hướng A)

### 4.1 Màu
- **Canvas:** dark `#0E0E14`; light `#F6F4FB`.
- **Đốm nền (backdrop blobs):** 3 hình tròn mờ (blur 120 px, opacity 35%) màu lấy từ 3 thói quen đầu tiên của người dùng → nền "mang màu" của chính họ. Trôi 60–90 s/vòng; dừng khi bật giảm chuyển động hoặc cửa sổ không active.
- **Gradient thói quen:** từ màu gốc (OKLCH) sinh `start = L+0.06, H−18°` và `end = L−0.04, H+22°`, chroma giữ nguyên. Hàm thuần trong Core (`HabitGradient.From(hex)`), có test tương phản.
- **Chữ trên gradient:** luôn trắng; nếu tương phản < 4.5:1 thì thêm lớp tối 18% dưới chữ (tự tính).
- **Màu ngữ nghĩa giữ nguyên** (success/warning/freeze) nhưng chỉ dùng cho trạng thái, không dùng làm nền lớn.

### 4.2 Chữ
- **Display/Số:** *Be Vietnam Pro* ExtraBold/Black (OFL, hỗ trợ tiếng Việt đầy đủ), nhúng trong app.
- **Thân chữ:** Segoe UI Variable (giữ cảm giác Windows, hiển thị tốt mọi DPI).
- Thang: Display 44/48, Title 28/34, Card title 18/22 SemiBold, Body 14/20, Caption 12/16, Number XL 56/56 (số streak).

### 4.3 Hình khối & độ sâu
- Bo góc: thẻ 28, tile lớn 32, nút 999 (pill), chip 999.
- Bóng: thẻ màu có "bóng màu" (glow) cùng màu gradient, blur 32, opacity 30% ở dark; không dùng bóng xám.
- Viền: 1 px trắng 8% phía trên thẻ (highlight) để thẻ "nổi" trên nền tối.

### 4.4 Icon & minh họa
- Emoji là icon chính (to, 32–40 px trên thẻ). Fluent icon chỉ cho điều hướng và nút hệ thống.
- Buddy (placeholder 🐣 tới khi có asset Rive/Lottie ở SPEC-V2-06) xuất hiện ở hero tile.

---

## 5. Bố cục từng màn hình

### 5.1 Khung app
```
┌───────────────────────────────────────────────────────────────┐
│ ● HabitGrid                     [⌘K Tìm…]      🔥 12   ⚙      │  thanh tiêu đề trong suốt, kéo được
├────┬──────────────────────────────────────────────────────────┤
│ 🏠 │                                                          │
│ 🗂 │                 NỘI DUNG (bento)                         │
│ 📊 │                                                          │
│ 🐣 │                                                          │
│    │                                                          │
│ ＋ │  ← nút tạo thói quen nổi, gradient brand                  │
└────┴──────────────────────────────────────────────────────────┘
```
- Thay `NavigationView` bằng **rail icon 72 px** dạng "dock": icon trong pill, mục đang chọn có nền gradient + chấm sáng; tooltip tên trang.
- Nút **＋** tròn lớn gradient ở cuối rail (hành động chính luôn trong tầm tay).
- Chip **🔥 streak tổng** luôn ở thanh tiêu đề.

### 5.2 Trang Hôm nay (màn quan trọng nhất)
```
┌──────────────────────────────────────────────────────────────────┐
│  Tối rồi, Linh ✨                               Thứ Năm · 05/11 │
│  ┌─ T2 ─┬─ T3 ─┬─ T4 ─┬─ T5 ─┬─ T6 ─┬─ T7 ─┬─ CN ─┐  ← dải tuần  │
│  │  ◕   │  ●   │  ◔   │ [◑]  │  ○   │  ○   │  ○   │  vòng % ngày │
│  └──────┴──────┴──────┴──────┴──────┴──────┴──────┘              │
│ ┌───────────────── HERO ────────────────┐ ┌──── STREAK ────┐     │
│ │  🐣  Mochi đang vui                   │ │   🔥 12         │     │
│ │  ████████████░░░░  3/5 hôm nay        │ │   ngày liền     │     │
│ │  [ XP ▓▓▓▓▓░░ lv 4 ]  (chỗ cho §06)   │ │  ❄ 2 freeze     │     │
│ └───────────────────────────────────────┘ └─────────────────┘     │
│  BUỔI SÁNG                                                       │
│ ┌──────────────┐ ┌──────────────┐ ┌──────────────┐               │
│ │ 🧘           │ │ 💧    6/8    │ │ 📚  ✓ XONG   │ ← thẻ gradient│
│ │ Thiền        │ │ Uống nước    │ │ Đọc sách     │   đầy màu khi │
│ │ 10 phút      │ │ ▓▓▓▓▓▓░░     │ │ 🔥 23        │   hoàn thành  │
│ │ ▪▪▪▫▪▪▫      │ │ [−]   [＋1]  │ │ ▪▪▪▪▪▪▪      │               │
│ └──────────────┘ └──────────────┘ └──────────────┘               │
│  BUỔI TỐI …                                                      │
│  ✓ Đã xong (2)  ▸                    😴 Không cần hôm nay (1) ▸   │
└──────────────────────────────────────────────────────────────────┘
```
- **Dải tuần**: 7 bong bóng, mỗi bong bóng là vòng % hoàn thành của ngày đó; bấm vào ngày cũ để xem và **sửa ngày đó** (tính năng còn thiếu ở bản hiện tại).
- **Hero tile**: Buddy + câu trạng thái + thanh tiến độ hôm nay (to, gradient brand) + chỗ cho XP/level.
- **Streak tile**: số streak tổng cỡ 56, lửa đổi màu theo mốc (7/30/100/365), số freeze.
- **Thẻ thói quen** (tile 1×1, rộng ~220 px, lưới tự co giãn 2–5 cột):
  - Chưa xong: nền tối 6% + viền gradient 2 px + emoji to + tên + phụ đề + mini heatmap 7 ngày.
  - Xong: **cả thẻ đổ gradient** (animation "liquid fill" từ chỗ bấm lan ra), dấu ✓ lớn, chữ trắng.
  - Đếm/thời lượng: thanh tiến độ trong thẻ; nút **＋1** lớn (bấm thẻ = ＋1), nút **−** nhỏ.
  - Cần bỏ: thẻ hiển thị "🛡 12 ngày sạch", nút "Tôi đã lỡ" ở menu chuột phải (không bấm nhầm).
  - Chuột phải / `…`: Sửa, Bỏ qua hôm nay, Ghi chú, Mood, Lưu trữ.
- **Đã xong / Không cần hôm nay**: hai "kệ" thu gọn ở cuối, mở ra thành hàng thẻ nhỏ.
- **Ngày hoàn hảo**: khi 100%, hero tile chuyển gradient cầu vồng nhẹ + confetti + nút "Chia sẻ" (dẫn tới share card, SPEC-V2-08 §1).

### 5.3 Trang Thói quen (bộ sưu tập)
- Lưới **thẻ sưu tầm** 2–4 cột: mỗi thẻ có header gradient + emoji, **heatmap 12 tuần** đầy đủ bằng thang màu của chính thói quen, streak, tỷ lệ 30 ngày.
- Thanh lọc dạng chip: Tất cả · theo chủ đề/thẻ · Đã lưu trữ.
- Kéo-thả đổi thứ tự giữ nguyên (fractional index).
- Bấm thẻ → **trang chi tiết** (hiện chưa có): hero gradient full-width với số streak khổng lồ, heatmap 52 tuần, biểu đồ theo tháng/thứ, nhật ký ghi chú.

### 5.4 Trình tạo/sửa ("sheet" thay cho hộp thoại form)
```
┌──────────── Thói quen mới ────────────┐
│  ┌──── XEM TRƯỚC THẺ (sống) ────┐     │  ← đúng thẻ sẽ hiện ở trang Hôm nay
│  │  💧 Uống nước     0/8 ly      │     │
│  └───────────────────────────────┘     │
│  [💧][📚][🏃][🧘] … [ + emoji ]        │  emoji to, cuộn ngang
│  ● ● ● ● ● ● ● ● ● ● ● ●              │  12 chấm gradient
│  Loại:   (✓ Có/không) (🔢 Đếm) (⏱ Phút) (🛡 Bỏ)   ← chip
│  Bao lâu: (Mỗi ngày) (Thứ…) (X/tuần) (X/tháng) (Mỗi N ngày)
│  Lúc nào: (☀️ Sáng) (🌤 Chiều) (🌙 Tối) (✨ Bất kỳ)
│  Nhắc:    [20:00 ✕] [+]                │
│  ▸ Nâng cao                            │
│                [ Hủy ]  [ Lưu ✨ ]     │
└────────────────────────────────────────┘
```
- Mọi lựa chọn dạng **chip** (thay ComboBox/RadioButton).
- **Thẻ xem trước sống** ở đầu: đổi màu/emoji/tên thấy ngay.
- Mở dạng sheet trượt từ phải (panel 520 px phủ lên trang) thay vì ContentDialog giữa màn.

### 5.5 Onboarding
- Mỗi bước một **nền gradient riêng** (tím → hồng, xanh → teal, cam → hồng…), chuyển màu mượt giữa các bước.
- Thanh tiến độ dạng 5 "viên kẹo" ở trên cùng thay cho chấm nhỏ ở dưới.
- Chủ đề: chip lớn có emoji, nảy khi chọn. Thói quen: thẻ mẫu dạng **thẻ gradient thu nhỏ** (đúng như sẽ xuất hiện).
- Bước Buddy: Buddy lớn ở giữa, đổi màu theo màu chọn, chữ "Chào, mình là Mochi!".
- Kết thúc: confetti + chuyển cảnh vào trang Hôm nay với các thẻ vừa tạo "bay" vào lưới.

### 5.6 Trạng thái trống, lỗi, giữ chỗ
- Trống: Buddy + câu thân thiện + nút gradient; không còn chữ xám giữa màn hình.
- Trang chưa làm (Insights/Buddy/Cài đặt): thẻ "sắp có" với ảnh minh họa, không để trang trống trơn.

---

## 6. Chuyển động & phản hồi (Composition API, không chặn luồng UI)

| Khoảnh khắc | Hiệu ứng | Thời gian |
|---|---|---|
| Bấm thẻ | Thẻ co 0.96 → bật lại (spring) | 220 ms |
| Check xong | "Liquid fill": hình tròn gradient lan từ điểm bấm phủ thẻ; ✓ vẽ nét | 420 ms |
| Streak +1 | Số lật như đồng hồ; lửa phập phồng | 600 ms |
| ＋1 (đếm) | Thanh tiến độ trượt; "+1" bay lên mờ dần | 300 ms |
| Ngày hoàn hảo | Confetti (Win2D, ≤ 120 hạt) + hero chuyển cầu vồng | 1.2 s |
| Đổi trang | Trượt + mờ 12 px | 250 ms |
| Hover thẻ | Nghiêng 3D ±3° theo chuột + glow mạnh hơn | liên tục, dừng khi rời |
| Nền | Đốm gradient trôi | 60–90 s/vòng |

Bắt buộc: tắt toàn bộ khi Windows tắt hiệu ứng hoặc bật "Giảm chuyển động" trong app (chỉ còn đổi màu tức thì); dừng chuyển động nền khi cửa sổ không active; không animation nào > 1.5 s chặn thao tác.

---

## 7. Thành phần giao diện mới (`HabitGrid.WinUI/Controls`)

| Control | Việc | Ghi chú kỹ thuật |
|---|---|---|
| `GradientBackdrop` | Nền canvas + đốm màu trôi | Composition `CompositionRadialGradientBrush` + blur; tạm dừng khi inactive |
| `IconRail` | Điều hướng dạng dock | Thay `NavigationView`; điều hướng vẫn qua `ShellViewModel` |
| `BentoGrid` | Lưới ô tự co 2–5 cột theo bề rộng | `ItemsRepeater` + `UniformGridLayout` (min 210 px) |
| `HabitCard` | Thẻ thói quen đủ trạng thái | Thay `HabitCheckButton` + dòng hiện tại; toàn thẻ là vùng bấm; có peer trợ năng (Invoke) |
| `LiquidFill` | Hiệu ứng đổ màu khi xong | Composition ellipse geometry + clip, scale từ điểm chạm |
| `WeekStrip` | 7 ngày + vòng % | Mỗi ngày là `ProgressRing` dày, chọn được |
| `HeroTile`, `StreakTile` | Ô tổng quan | Có slot cho XP/Buddy (§06) |
| `Chip`, `ChipGroup` | Thay ComboBox/Radio | Chọn 1 hoặc nhiều, bàn phím đầy đủ |
| `GradientButton` | Nút hành động chính | Pill gradient brand, glow khi hover |
| `ConfettiCanvas` | Ăn mừng | Win2D `CanvasAnimatedControl`, chỉ chạy khi cần |
| `Sheet` | Panel trượt cho editor | Lớp phủ + trượt từ phải, Esc để đóng |

Thư viện thêm: **Win2D** (`Microsoft.Graphics.Win2D`, bản stable mới nhất khi làm) cho heatmap và confetti. Không thêm thư viện UI nặng khác.

---

## 8. Kiến trúc theme

- Token chia 3 lớp: **nguyên thủy** (màu, cỡ) → **ngữ nghĩa** (`canvas`, `card.idle`, `card.done`, `text.onGradient`…) → **thành phần** (HabitCard dùng token ngữ nghĩa, không dùng mã màu).
- Theme = file JSON ghi đè lớp ngữ nghĩa (A Candy Bento, B Neon Night, C Soft Pastel, và các theme Plus sau này). `ThemeService` nạp JSON → `ResourceDictionary` lúc chạy; đổi theme không cần khởi động lại.
- Gradient thói quen luôn sinh từ màu gốc qua hàm của Core nên mọi theme đều nhất quán.
- Widget desktop (SPEC-V2-05) dùng **cùng token và cùng HabitCard thu nhỏ** → làm UI trước giúp widget không phải thiết kế riêng.

---

## 9. Thay đổi phía ViewModel (nhỏ, có test)

| Việc | Ở đâu |
|---|---|
| Chọn ngày đang xem (dải tuần) + check-in cho ngày cũ | `TodayViewModel.SelectedDay`, `CheckInAsync` dùng ngày đã chọn |
| Tỷ lệ hoàn thành từng ngày trong tuần cho dải tuần | `TodayViewModel.Week` (7 phần tử) |
| Streak tổng (ngày có ≥ 1 check-in) + số freeze | `TodayViewModel.OverallStreak` |
| Skip/ghi chú/mood từ menu thẻ | `TodayViewModel.SkipAsync`, `SetNoteAsync`, `SetMoodAsync` |
| Trang chi tiết thói quen | `HabitDetailViewModel` mới (heatmap 52 tuần, thống kê) |
| Gradient thói quen | `HabitGradient` trong Core (hàm thuần, test tương phản) |

---

## 10. Trợ năng & chất lượng (không được hy sinh vì "màu mè")

- Chữ trên gradient đạt 4.5:1 (test tự động trên cả 12 màu preset, cả 3 theme).
- Toàn thẻ bấm được bằng chuột và **bàn phím** (Tab tới thẻ, Space/Enter để check); Narrator đọc "Uống nước, 6 trên 8 ly, chuỗi 12 ngày, nút".
- High contrast: bỏ gradient, dùng màu hệ thống.
- Hiệu năng: Today với 20 thẻ giữ 60 fps khi cuộn; nền động < 1% CPU; RAM app < 150 MB.
- Smoke test giữ nguyên hành trình hiện có + chụp ảnh **cả dark và light**, cả 3 theme, ở 2 kích thước cửa sổ (1280×800 và 900×700).

---

## 11. Kế hoạch triển khai

| Bước | Nội dung | Ra được gì | Ước lượng |
|---|---|---|---|
| **R0** | Duyệt hướng phong cách (A/B/C) + bản mockup tĩnh 3 màn (Hôm nay, editor, onboarding) dạng trang HTML để xem trước khi code | Ảnh/trang mockup được duyệt | 0,5 phiên |
| **R1** | Nền móng: token 3 lớp, `ThemeService` JSON, font Be Vietnam Pro, `GradientBackdrop`, `IconRail`, `GradientButton`, `Chip`, `HabitGradient` (Core + test) | Khung app mới, chưa đổi nội dung trang | 1 phiên |
| **R2** | Trang Hôm nay: `BentoGrid`, `HabitCard` (đủ 4 loại + trạng thái), `HeroTile`, `StreakTile`, `WeekStrip` + ViewModel chọn ngày, `LiquidFill`, confetti | Màn chính mới hoàn chỉnh | 1,5 phiên |
| **R3** | Trang Thói quen (thẻ sưu tầm + heatmap 12 tuần Win2D) + trang chi tiết thói quen | Bộ sưu tập + chi tiết | 1 phiên |
| **R4** | Editor dạng sheet + chip + thẻ xem trước; onboarding gradient + thẻ mẫu | Luồng tạo và làm quen mới | 1 phiên |
| **R5** | Đánh bóng: chuyển động, giảm chuyển động, light mode, theme B/C, trợ năng, hiệu năng, ảnh chụp smoke cho mọi tổ hợp | Sẵn sàng làm widget trên cùng ngôn ngữ thiết kế | 0,5–1 phiên |

Tổng ≈ 5–6 phiên. Mỗi bước kết thúc bằng: build sạch, toàn bộ test xanh, smoke test đạt kèm ảnh chụp, commit riêng.

### Tiêu chí nghiệm thu
1. Đặt ảnh chụp màn Hôm nay mới cạnh Microsoft To Do: không còn bố cục danh sách một cột, không còn điều hướng dạng danh sách chữ.
2. Check-in bằng chuột (bấm bất kỳ đâu trên thẻ) và bằng bàn phím đều chạy; có hiệu ứng liquid fill; tắt được hoàn toàn bằng "giảm chuyển động".
3. Sửa được ngày hôm qua từ dải tuần.
4. 12 màu × 3 theme × dark/light: chữ trên thẻ đạt 4.5:1 (test).
5. Smoke test xuất đủ ảnh cho mọi màn ở cả dark và light.
6. Không ViewModel cũ nào bị phá: toàn bộ test hiện có vẫn xanh.

---

## 12. Ảnh hưởng tới lộ trình

```
✅ 1. Trình tạo/sửa + onboarding (logic)       ← xong, giữ nguyên ViewModel
▶ 1.5 Rebuild UI "Glow 2" (R0 → R5)            ← chèn vào đây
   2. Widget desktop (SPEC-V2-05)              ← dùng lại token + HabitCard thu nhỏ
   3. Game hóa (SPEC-V2-06)                    ← lấp vào slot Buddy/XP đã chừa ở hero tile
   4. Nhắc nhở/Focus/phím tắt → thu phí → phát hành v2.0
```
Rủi ro kỹ thuật lớn của widget (RAM của WidgetHost WPF) độc lập với giao diện; có thể đo nhanh trong nửa phiên trước hoặc ngay sau R1 nếu muốn chốt sớm.
