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

## Tiến độ đợt 1 (06/11/2026)
| # | Tính năng | Trạng thái |
|---|---|---|
| F5 | Sao lưu & khôi phục | **Xong.** Tự sao lưu mỗi ngày (giữ 14 bản tự động; bản tay không bị dọn), sao lưu ngay, khôi phục từ file hoặc danh sách; trước khi khôi phục luôn tự lưu hiện trạng nên quay lại được; file hỏng không đụng vào dữ liệu. Cài đặt → Sao lưu. |
| F3 | Tạm nghỉ | **Xong.** Đánh dấu trước các ngày nghỉ bằng trạng thái "đóng băng" nên chuỗi, nhắc nhở, XP, Insights tự đúng. Free 14 ngày/lần, Plus 60 ngày. Cho mọi thói quen (Cài đặt) hoặc một thói quen (trang chi tiết); dải thông báo + nút kết thúc sớm trên trang Hôm nay. |
| F4 | Check-in cảm xúc | **Xong.** 5 emoji một chạm + ghi chú ≤ 140 ký tự trên trang Hôm nay (cả ngày đang xem); Insights ưu tiên tâm trạng cả ngày. Lưu trong settings nên **chưa đồng bộ** — cần chuyển sang bảng riêng khi làm sync v2.1. |
| F1 | Routine | **Xong.** Free 2 routine, Plus không giới hạn; mỗi routine 2–12 bước, mỗi thói quen thuộc một routine; dải routine trên Hôm nay (bấm từng bước, "Xong cả routine" không đè lên ngày đóng băng); quản lý/soạn routine ở trang Thói quen. Thẻ thói quen bên dưới giữ nguyên (routine là lối tắt, chưa ẩn thẻ gốc). |

Chưa làm trong đợt 1: hẹn giờ từng bước của routine (P4, Plus), nhắc theo routine, kéo-thả đổi thứ tự bước (đang dùng nút ↑↓).

## Tiến độ đợt 2 – Plus (06/11/2026)
| # | Tính năng | Trạng thái |
|---|---|---|
| P3 | Sao lưu sang thư mục cloud | **Xong.** Chép mỗi bản sao lưu (tự động + tay) sang thư mục người dùng chọn (gợi ý OneDrive/Google Drive/Dropbox), ghi file tạm rồi đổi tên, chỉ dọn bản tự động cũ. App không đăng nhập hay gửi gì lên mây. Chưa kiểm thử với thư mục cloud thật. |
| P7 | Dải 4 ảnh | **Xong.** Khung photobooth 4 ảnh (mỗi ngày một ảnh, mới nhất trước) trong thẻ chia sẻ; Free thấy khóa → paywall. |
| P2 | Insights nâng cao | **Xong.** Tháng này vs tháng trước (cùng số ngày), thói quen đi cùng nhau (90 ngày, cần đủ dữ liệu), so sánh hai thói quen (12 tuần), lịch cả năm gộp. Free thấy thẻ giới thiệu. |
| P5 | Tùy biến giao diện | **Một phần.** Có màu nhấn riêng (8 màu gợi ý + hex, áp ngay, giữ lại khi hết Plus). **Chưa làm đổi font** (font là StaticResource, cần khởi động lại). |

## Tiến độ đợt 3 (06/11/2026)
| # | Tính năng | Trạng thái |
|---|---|---|
| F2 | Thử thách 30 ngày | **Xong.** 10 thử thách mẫu (7–100 ngày), tạo thói quen kèm mốc kết thúc; đếm liền mạch (lỡ một ngày là làm lại, ngày tạm nghỉ được miễn); dải tiến độ trên Hôm nay, huy hiệu hoàn thành. Free 1 thử thách một lúc, Plus 5. Lưu trong settings nên **chưa đồng bộ**; huy hiệu thử thách chưa nằm trong hệ huy hiệu Buddy. |
| F6 | Tổng kết tuần | **Xong.** Thứ Hai–thứ Tư hiện thẻ tuần trước (tỷ lệ, so với tuần trước đó, ngày tốt nhất, thói quen nổi bật/yếu, một gợi ý) + chọn "ý định tuần này" có thanh tiến độ cả tuần. Lưu trong settings (chưa đồng bộ). |
| P4 | Routine hẹn giờ | **Xong (Plus).** Đặt phút cho từng bước, chạy nối tiếp; hết giờ hoặc bấm Xong thì đánh dấu hoàn thành, Bỏ qua thì không. Bộ chạy xử lý cả trường hợp máy ngủ. Chưa kiểm thử tay hộp thoại đếm giờ thật (smoke chỉ kiểm bộ chạy). |
| P6 | Phụ kiện Buddy | **Xong.** 13 món (đầu/mặt/cổ): Free mở bằng cấp độ hoặc huy hiệu, Plus 6 món riêng; hết Plus thì món Plus ngừng hiển thị nhưng lựa chọn được giữ. Hình hiện là emoji ghép lên linh vật, chưa có hình vẽ riêng; "bộ riêng mỗi mùa" chưa làm (chưa có đồ theo mùa). |
| P1 | Wrapped tháng | **Xong.** 6 slide story (tổng check-in, thói quen số 1, chuỗi dài nhất, giờ vàng, ngày hoàn hảo, ảnh khoảnh khắc/Buddy), tháng trước hoặc tháng này, lưu PNG 1080×1920. Free xem slide đầu, Plus xem đủ và lưu. Nút ở trang Insights; chưa có banner nhắc đầu tháng. |

## Tiến độ đợt 4 – việc còn lại của đợt 2 và 3 (06/11/2026)
| # | Việc | Trạng thái |
|---|---|---|
| P5 | Đổi phông chữ | **Xong.** 4 bộ (Be Vietnam Pro mặc định, hệ thống, có chân, đơn cách), Plus mới đổi được. Phông được nạp trước khi giao diện dựng nên **có hiệu lực ở lần mở app sau**; hết Plus thì lần mở sau về mặc định, lựa chọn vẫn được giữ. Chưa kiểm chứng bằng mắt khi chạy thật với phông khác (smoke chỉ kiểm lựa chọn được ghi và từ điển phông lấy đúng). |
| P1 | Banner Wrapped đầu tháng | **Xong.** 7 ngày đầu mỗi tháng, chỉ khi tháng trước có check-in, ẩn bằng "Để sau" hoặc "Xem" (mỗi tháng một lần). |
| P6 | Đồ phụ kiện theo mùa | **Xong.** 8 món (mỗi mùa một món đầu + một món cổ), Plus nhận trong mùa của nó rồi giữ luôn khi còn Plus (mùa theo tháng dương lịch: xuân 3–5, hè 6–8, thu 9–11, đông 12–2). Hình vẫn là emoji. |
