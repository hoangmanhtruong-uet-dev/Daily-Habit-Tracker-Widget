# Kế hoạch tính năng tiếp theo (Free / Plus)

> 06/11/2026 • Trước v2.0. Nguyên tắc giữ nguyên từ SPEC-V2-10: **Free đủ dùng hằng ngày, Plus bán thẩm mỹ, chiều sâu và tiện lợi** — không khóa tính năng cơ bản. Chỉ liệt kê việc làm được **không cần máy chủ** (sync, bạn bè, AI coach để v2.1/v2.2).
> Ước lượng: S ≈ nửa phiên, M ≈ 1 phiên, L ≈ 2 phiên.

## 1. Gói Free — làm app "dính" hơn mỗi ngày

| # | Tính năng | Mô tả | Vì sao (GenZ) | Công | Tận dụng sẵn |
|---|---|---|---|---|---|
| F1 | **Routine (chuỗi thói quen)** | Gom nhiều thói quen thành routine "Sáng", "Trước khi ngủ"; trên Hôm nay hiện thành một thẻ, bấm lần lượt từng bước hoặc "Xong cả routine". Free: tối đa 2 routine. | Thói quen đi theo cụm dễ giữ hơn (habit stacking); insight `stack_suggestion` đã gợi ý gộp. | M | Bảng `routines` + `RoutineRepository` đã có, chưa có giao diện |
| F2 | **Thử thách 30 ngày** | Chọn thử thách mẫu ("30 ngày không trà sữa", "Đọc 10 trang mỗi ngày"…), tạo thói quen kèm mốc ngày kết thúc, thanh tiến độ, huy hiệu khi hoàn thành. | Mục tiêu có hạn rõ ràng, dễ khoe, dễ rủ bạn. | M | Mẫu thói quen (64 mẫu), engine huy hiệu JSON |
| F3 | **Tạm nghỉ thói quen (vacation mode)** | Tạm dừng một thói quen hoặc cả app vài ngày (ốm, du lịch): ngày nghỉ không tính lỡ, chuỗi giữ nguyên, không nhắc. | Bỏ app vì "lỡ chuỗi khi đi chơi" là lý do hay gặp nhất; khác freeze (freeze là cứu ngày đã lỡ). | S–M | `DayStatus.Frozen`/`Skipped`, planner nhắc nhở |
| F4 | **Check-in cảm xúc trong ngày** | Một hàng emoji trên trang Hôm nay ("Hôm nay bạn thấy sao?"), ghi kèm câu ngắn tùy chọn; Insights đã có phần mood sẵn để dùng. | Nhanh 1 chạm, GenZ thích ghi mood; làm dữ liệu cho insight `mood_link`. | S | Trường `Mood` trong `DayLog`, thẻ mood ở Insights |
| F5 | **Sao lưu & khôi phục thủ công** | Cài đặt → "Sao lưu ngay" (file `.hgbackup`), "Khôi phục từ file", tự sao lưu cục bộ mỗi ngày (giữ 14 bản). | Niềm tin: dữ liệu không mất khi cài lại máy (chưa có sync). | S | `SnapshotBackupService` đã có, chưa có giao diện |
| F6 | **Tổng kết tuần** | Mỗi thứ Hai, một thẻ trên trang Hôm nay: tuần trước làm được gì, ngày tốt nhất, 1 gợi ý; chọn "ý định tuần này" (1 thói quen ưu tiên). | Nhịp tuần nhẹ nhàng, không cần AI. | S–M | `InsightsCalculator`, `InsightEngine` |

## 2. Gói Plus — chiều sâu, thẩm mỹ, tiện lợi

| # | Tính năng | Mô tả | Vì sao trả tiền | Công | Phụ thuộc |
|---|---|---|---|---|---|
| P1 | **Wrapped tháng** | Ngày 1 hằng tháng: 6 "slide" kiểu story (tổng check-in, thói quen số 1, chuỗi dài nhất, giờ vàng, ngày hoàn hảo, ảnh khoảnh khắc nổi bật), xuất ảnh 1080×1920 từng slide. Free xem slide đầu. | Rất "share-able", đúng gu GenZ; kéo người dùng quay lại mỗi tháng. | L | Insights, thẻ chia sẻ, ảnh khoảnh khắc |
| P2 | **Insights nâng cao** | So sánh 2 thói quen, tương quan "làm A thì hay làm B", so sánh tháng này với tháng trước, heatmap năm toàn màn hình. | Người dùng lâu năm muốn nhìn sâu dữ liệu của mình. | M | `InsightsCalculator` |
| P3 | **Tự sao lưu vào thư mục chọn (OneDrive/Google Drive)** | Chọn thư mục đồng bộ của dịch vụ cloud; app tự ghi bản sao lưu mỗi ngày vào đó. | "Sync nghèo" trước khi có sync thật; spec đã ghi là quyền Plus. | S | F5 |
| P4 | **Routine không giới hạn + hẹn giờ từng bước** | Plus: routine không giới hạn; mỗi bước có thời lượng, chạy như Focus nối tiếp ("Routine sáng 20 phút"). | Tiện lợi cho người dùng nặng. | M | F1, Focus timer |
| P5 | **Tùy biến giao diện** | Chọn màu nhấn riêng (bánh xe màu), icon app thay thế, phông chữ hiển thị. | Thẩm mỹ là lý do mua chính của GenZ. | M | `ThemeService`, `HabitGradient` |
| P6 | **Phụ kiện Buddy** | Mũ, kính, khăn cho Buddy; Free mở bằng cấp/huy hiệu, Plus có bộ riêng mỗi mùa. | Gắn bó cảm xúc với linh vật; spec SPEC-V2-06 §3. | M–L | Cần tài sản đồ họa (emoji ghép trước, vẽ sau) |
| P7 | **Dải ảnh 4 khoảnh khắc** | Thẻ chia sẻ dạng photobooth 4 ô ảnh của 4 ngày gần nhất / 4 thói quen. | Mở rộng tự nhiên của thẻ chia sẻ vừa làm. | S | Ảnh khoảnh khắc, thẻ chia sẻ |

## 3. Không đưa vào lúc này (và lý do)
- **Âm thanh nền Focus**: cần file âm thanh có bản quyền rõ ràng; làm khi có nguồn nhạc.
- **AI coach, bạn bè, thử thách nhóm, sync**: cần máy chủ (v2.1/v2.2).
- **Đăng thẳng lên Locket/Instagram**: không có API công khai, không xin mật khẩu người dùng.

## 4. Đề xuất thứ tự
1. **Đợt 1 (Free, nhỏ mà giá trị cao):** F5 Sao lưu → F3 Tạm nghỉ → F4 Check-in cảm xúc → F1 Routine. Lý do: F5 là niềm tin dữ liệu trước khi phát hành; F3/F4 nhỏ, giữ chân tốt; F1 dùng lại phần đã có.
2. **Đợt 2 (Plus):** P3 Sao lưu vào thư mục cloud → P7 Dải 4 ảnh → P2 Insights nâng cao → P5 Tùy biến.
3. **Đợt 3:** F2 Thử thách 30 ngày, F6 Tổng kết tuần, P1 Wrapped tháng (lớn nhất, nên để khi đã có dữ liệu thật của người dùng beta), P4, P6.

Mỗi tính năng làm như các đợt trước: lõi thuần có test → giao diện → smoke test + ảnh CI → cập nhật paywall chỉ khi tính năng Plus đã thật sự có.
