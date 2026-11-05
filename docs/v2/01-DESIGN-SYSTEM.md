# 01 — Design System v2 "Glow"

> Mục tiêu: nhìn như một app Windows 11 hiện đại (Mica, Fluent) nhưng **có cá tính**: màu tươi, bo tròn mềm, chuyển động nảy nhẹ, linh vật, âm thanh nhỏ. Tham chiếu cảm giác: Finch × Arc Browser × GitHub contributions.
> Mọi giá trị dưới đây là **token**; code chỉ dùng token, không viết mã màu/kích thước cứng.

## 1. Thương hiệu

| Thành phần | Quy định |
|---|---|
| Tính cách | Thân thiện, hơi tinh nghịch, tích cực, không dạy đời |
| Giọng văn | Ngắn, xưng "bạn", dùng emoji tiết chế (tối đa 1/câu). Ví dụ: "Ngon! 7 ngày liền rồi 🔥", không viết "Bạn đã hoàn thành mục tiêu". |
| Logo | Ô vuông bo góc 2×2 với 1 ô phát sáng; dùng được ở 16 px |
| Linh vật | **Grid Buddy**: sinh vật hình khối nhỏ, mềm, có mắt; tiến hóa theo level (SPEC-V2-06). Bộ biểu cảm tối thiểu: vui, rất vui, ngủ, buồn nhẹ, ngạc nhiên, ăn mừng. Định dạng: Lottie/Rive hoặc chuỗi PNG 2x. |

## 2. Màu

### 2.1 Màu nền và chữ (theme Sáng / Tối)

| Token | Sáng | Tối | Dùng cho |
|---|---|---|---|
| `bg/base` | Mica (fallback `#F7F7FA`) | Mica (fallback `#121216`) | Nền cửa sổ |
| `bg/surface` | `#FFFFFF` | `#1C1C22` | Thẻ |
| `bg/surface-2` | `#F0F0F5` | `#26262E` | Thẻ lồng, input |
| `bg/overlay` | `#000000` 40% | `#000000` 60% | Phủ dưới dialog |
| `fg/primary` | `#16161D` | `#F4F4F8` | Chữ chính |
| `fg/secondary` | `#5B5B6B` | `#A9A9B8` | Chữ phụ |
| `fg/muted` | `#8E8E9E` | `#6E6E7E` | Nhãn, placeholder |
| `stroke/subtle` | `#000000` 8% | `#FFFFFF` 8% | Viền thẻ |
| `accent/brand` | `#6C5CE7` | `#8B7CFF` | Nút chính, focus ring |
| `state/success` | `#16A34A` | `#22C55E` | Hoàn thành |
| `state/warning` | `#D97706` | `#F59E0B` | Sắp mất streak |
| `state/danger` | `#DC2626` | `#F87171` | Xóa |
| `state/freeze` | `#0EA5E9` | `#38BDF8` | Ngày dùng freeze |

Tương phản chữ/nền MUST ≥ 4.5:1 (WCAG AA), kiểm bằng test tự động (SPEC-V2-11).

### 2.2 Màu thói quen (12 preset + tùy chỉnh)

Mỗi thói quen chọn một **hue**; Core sinh thang 5 mức (0 = trống, 1–4 = độ đậm) bằng OKLCH để đều mắt ở cả hai theme (thay thuật toán HSL của v1).

Preset: Grape `#8B5CF6`, Ocean `#3B82F6`, Mint `#10B981`, Lime `#84CC16`, Sun `#FACC15`, Peach `#FB923C`, Coral `#F43F5E`, Bubblegum `#EC4899`, Sky `#06B6D4`, Lavender `#A78BFA`, Mocha `#A16207`, Graphite `#64748B`.

### 2.3 Theme thẩm mỹ (bộ sưu tập)

Theme đổi cả nền widget, màu ô, font tiêu đề, hiệu ứng ô hoàn thành.

| Theme | Free/Plus | Mô tả |
|---|---|---|
| Classic Glow | Free | Mặc định, theo màu thói quen |
| Midnight | Free | Tối sâu, ô phát sáng neon nhẹ |
| Matcha | Plus | Xanh trà, kem, giấy |
| Y2K Chrome | Plus | Gradient bạc-hồng, ô bóng |
| Cottage | Plus | Pastel, ô hình hoa nhỏ khi đạt 100% |
| Terminal | Plus | Monospace, xanh lá trên đen, ô ký tự `█` |
| Lo-fi Sunset | Plus | Cam-tím, hạt nhiễu nhẹ |
| Pixel | Plus | Ô pixel cứng, âm thanh 8-bit |

Theme được định nghĩa bằng file JSON (`themes/*.json`) gồm token ghi đè, để thêm theme mới không cần sửa code.

## 3. Chữ

| Token | Font | Cỡ / dòng | Đậm |
|---|---|---|---|
| `type/display` | Segoe UI Variable Display | 40 / 48 | Semibold |
| `type/title-1` | Segoe UI Variable Display | 28 / 36 | Semibold |
| `type/title-2` | Segoe UI Variable Text | 20 / 28 | Semibold |
| `type/body` | Segoe UI Variable Text | 14 / 20 | Regular |
| `type/body-strong` | Segoe UI Variable Text | 14 / 20 | Semibold |
| `type/caption` | Segoe UI Variable Small | 12 / 16 | Regular |
| `type/number` | Segoe UI Variable Display, số tabular | 32 / 40 | Bold |

Font tiêu đề riêng theo theme (ví dụ Terminal dùng Cascadia Mono) đóng gói trong app, có giấy phép OFL.

## 4. Khoảng cách, bo góc, độ sâu

- Lưới 4 px. Token: `space/1`=4, `2`=8, `3`=12, `4`=16, `5`=20, `6`=24, `8`=32, `10`=40.
- Bo góc: `radius/sm`=6 (input), `md`=10 (nút), `lg`=16 (thẻ), `xl`=24 (dialog, widget), `cell`=3–5 tùy cỡ ô.
- Độ sâu: `elevation/1` thẻ (bóng 0 1 2 8%), `elevation/2` menu (0 4 12 12%), `elevation/3` dialog (0 16 40 20%). Theme tối giảm bóng, tăng viền.

## 5. Chuyển động

| Token | Thời gian | Easing | Dùng cho |
|---|---|---|---|
| `motion/tap` | 120 ms | ease-out | Nhấn nút, ô co 0.92 → 1 |
| `motion/check` | 320 ms | spring (damping 0.6) | Ô hoàn thành: nảy 1 → 1.18 → 1, glow lan ra |
| `motion/page` | 250 ms | decelerate | Chuyển trang (Connected animation) |
| `motion/celebrate` | 1200 ms | — | Confetti khi xong hết hôm nay / lên level |
| `motion/streak` | 600 ms | spring | Số streak lật lên kiểu đồng hồ |
| `motion/buddy` | Lặp | — | Linh vật thở/chớp mắt khi rảnh (tạm dừng khi cửa sổ không hiển thị) |

Quy tắc:
- MUST tôn trọng cài đặt "Animation effects" của Windows và tùy chọn "Giảm chuyển động" trong app: khi bật, thay chuyển động bằng đổi màu tức thì, không confetti.
- Animation chạy bằng Composition API, không chiếm luồng UI. Widget nhàn rỗi không được chạy animation lặp.

## 6. Âm thanh và phản hồi

- Bộ âm mặc định "Soft": check (pop nhẹ), uncheck, hoàn tất ngày (chime 3 nốt), lên level, huy hiệu. Mỗi file < 50 KB, ≤ 400 ms.
- Bộ âm thêm (Plus): "8-bit", "Wood", "Bubble".
- Mặc định **bật âm trong app, tắt âm trên widget**. Tắt toàn bộ bằng một công tắc. Không phát khi Windows đang Do Not Disturb.

## 7. Icon và minh họa

- Icon UI: Fluent System Icons (Regular/Filled).
- Icon thói quen: 150+ icon chọn được, kèm **emoji** (người dùng gõ emoji bất kỳ làm icon). Lưu dạng `fluent:<tên>` hoặc `emoji:<ký tự>`.
- Minh họa trạng thái trống, lỗi, offline đều dùng Grid Buddy.

## 8. Component chuẩn (thư viện `HabitGrid.App/Controls`)

| Component | Mô tả | Trạng thái bắt buộc |
|---|---|---|
| `HabitCheckButton` | Nút tròn check-in; với loại đếm hiện vòng tiến độ; thời lượng hiện nút play | trống, một phần, xong, bỏ qua, freeze, khóa (read-only), loading |
| `HeatmapGrid` | Lưới tự vẽ (Win2D/Composition), 7×N, hỗ trợ cỡ ô 6–20 px | hover, focus bàn phím, chọn, hôm nay, tương lai, trước ngày bắt đầu |
| `StreakBadge` | Lửa + số, đổi màu theo độ dài (≥7, ≥30, ≥100, ≥365) | bình thường, nguy cơ mất (cuối ngày chưa check), đóng băng |
| `StatCard` | Số lớn + nhãn + so sánh kỳ trước (▲▼) | loading skeleton |
| `BuddyView` | Hiển thị linh vật theo biểu cảm | 6 biểu cảm |
| `ProgressRing` | Vòng % hôm nay | — |
| `ShareCardRenderer` | Dựng ảnh 1080×1920 / 1080×1080 từ dữ liệu | — |
| `EmptyState` | Minh họa + tiêu đề + nút | — |
| `Toast/InfoBar` | Thông báo trong app, có hành động "Hoàn tác" | info, success, warning, error |
| `PaywallSheet` | Tấm nâng cấp Plus | — |

Mỗi component MUST có: chế độ sáng/tối, focus bàn phím rõ, `AutomationProperties.Name`, trang demo trong "Component Gallery" ẩn (bật bằng `--gallery`) để QA soi nhanh.

## 9. Bàn giao thiết kế

- Figma: 1 file gồm Tokens, Components, các màn ở 02-UX-SCREENS, prototype onboarding.
- Token xuất bằng plugin sang JSON → script sinh `Tokens.xaml` (không chép tay).
