# SPEC-V2-07 — Insights & AI coach

> Phát hành: insight tự động [v2.0], AI coach [v2.2] (Plus) • Phụ thuộc: SPEC-V2-01, 03, 04

## 1. Thống kê (Core `IStatsCalculator`)
| Chỉ số | Định nghĩa |
|---|---|
| Tỷ lệ hoàn thành | Theo SPEC-V2-01 §3.3, cho từng thói quen và gộp |
| Ngày hoàn hảo | Ngày mọi thói quen có lịch đều Done/Skipped/Frozen, có ít nhất 1 Done |
| Theo thứ trong tuần | Tỷ lệ cho từng thứ, 90 ngày gần nhất |
| Theo giờ | Phân bố `CheckedAt` theo giờ (24 cột) |
| Xu hướng | Tỷ lệ theo tuần, đường trung bình trượt 4 tuần |
| Tổng/Trung bình | Với Count/Duration: tổng, trung bình ngày có làm, tốt nhất |
| Mood | Trung bình mood theo tuần; mood trung bình ngày Done vs không Done |

Tính nền, cache theo (khoảng, phiên bản dữ liệu). Trang Insights mở < 300 ms với 20 thói quen × 2 năm.

## 2. Insight tự động không dùng AI [v2.0]
`IInsightEngine` chạy bộ luật, mỗi luật trả về `Insight(key, severity, params, habitId?)`, UI dịch thành câu. Chọn tối đa 3 insight có điểm cao nhất, không lặp lại cùng key trong 7 ngày.

Ví dụ luật:
| Key | Điều kiện | Câu (VI) |
|---|---|---|
| `weak_weekday` | Tỷ lệ của một thứ thấp hơn trung bình ≥ 25 điểm, ≥ 8 mẫu | "Bạn hay lỡ *{habit}* vào {weekday}. Thử dời giờ nhắc ngày đó?" |
| `golden_hour` | ≥ 60% check-in trong cùng khung 2 giờ | "Giờ vàng của bạn là {start}–{end}." |
| `momentum_up` | Tỷ lệ 14 ngày gần nhất cao hơn 14 ngày trước ≥ 15 điểm | "*{habit}* đang lên phong độ: +{delta}%!" |
| `slipping` | Ngược lại ≥ 20 điểm | "*{habit}* hơi chững lại. Giảm mục tiêu xuống {suggested} một tuần thử?" |
| `mood_link` | Mood ngày Done cao hơn ngày không Done ≥ 0,7, ≥ 14 mẫu | "Những ngày *{habit}*, mood của bạn cao hơn rõ rệt." |
| `stack_suggestion` | Hai thói quen thường check cách nhau < 15 phút | "Gộp *{a}* và *{b}* thành routine?" |
| `near_milestone` | Còn ≤ 2 ngày tới mốc streak | "Còn {n} ngày nữa là {milestone} ngày *{habit}*!" |

Insight dùng tham số, không sinh văn bản tự do ở client, nên dịch được và không có rủi ro nội dung.

## 3. AI coach [v2.2, Plus]

### 3.1 Tính năng
1. **Tóm tắt tuần**: mỗi thứ Hai (hoặc khi người dùng bấm), 4–6 câu: điều làm tốt, điều nên chỉnh, một gợi ý cụ thể cho tuần này.
2. **Hỏi coach**: chat ngắn trong ngữ cảnh dữ liệu thói quen ("Sao mình hay bỏ gym thứ 4?", "Gợi ý routine buổi sáng 20 phút").
3. **Trợ lý tạo thói quen**: người dùng gõ mục tiêu tự nhiên ("muốn ngủ sớm hơn") → đề xuất 1–3 thói quen có cấu hình sẵn (loại, tần suất, nhắc) để bấm thêm.

### 3.2 Kiến trúc
- Client → `POST /v1/ai/*` (server) → nhà cung cấp LLM. **API key chỉ nằm ở server.**
- Mô hình đề xuất: Claude (họ Claude 5). Dùng model nhỏ, nhanh (Haiku 5.5) cho tóm tắt tuần và tạo thói quen; model mạnh hơn (Sonnet 5.5) cho chat coach nếu chất lượng Haiku chưa đủ. Kiểm tra giá và model ID hiện hành khi triển khai.
- Ngữ cảnh gửi đi là **bản tóm tắt thống kê** do Core tính (tỷ lệ, xu hướng, insight key), không gửi nguyên nhật ký. **Ghi chú và mood chỉ gửi khi người dùng bật "Cho coach đọc ghi chú"** (mặc định tắt).
- Đầu ra cho "trợ lý tạo thói quen" dùng structured output (JSON schema khớp `HabitDraft`), server validate bằng `HabitValidator` trước khi trả.
- Streaming (SSE) cho chat để phản hồi nhanh.
- Prompt hệ thống: giọng văn theo 01-DESIGN-SYSTEM §1, ngôn ngữ theo locale, không đưa lời khuyên y tế/tâm lý chuyên sâu; với dấu hiệu khủng hoảng (tự hại…) trả lời an toàn kèm thông tin đường dây hỗ trợ theo quốc gia.

### 3.3 Chi phí & giới hạn
- Plus: 30 lượt chat/tháng + tóm tắt tuần không giới hạn (1/tuần). Free: 1 tóm tắt tuần thử nghiệm mỗi tháng (kích thích nâng cấp).
- Server ghi `ai_usage`, chặn khi vượt; đặt trần chi phí toàn hệ thống mỗi ngày (ngắt AI, không ngắt app).
- Cache tóm tắt tuần theo (user, tuần, phiên bản dữ liệu).

### 3.4 Quyền riêng tư
- Màn hình bật AI lần đầu giải thích rõ dữ liệu nào được gửi, gửi đi đâu; người dùng phải bấm đồng ý.
- Không lưu nội dung chat trên server quá 30 ngày; người dùng xóa lịch sử chat bất kỳ lúc nào.
- Ghi vào chính sách riêng tư tên nhà cung cấp AI và mục đích xử lý.

## 4. Tiêu chí nghiệm thu
1. Mỗi luật insight có test dữ liệu dương tính/âm tính; không insight nào hiện khi thiếu mẫu tối thiểu.
2. Trang Insights đạt ngân sách thời gian §1.
3. AI: bộ 30 ca đánh giá (eval) gồm dữ liệu giả lập đa dạng + ca nhạy cảm; đạt ≥ 90% "hữu ích, đúng dữ liệu, đúng giọng", 100% ca nhạy cảm trả lời an toàn.
4. Không có request AI nào chứa ghi chú/mood khi tùy chọn tắt (test kiểm payload).
5. Vượt hạn mức → thông báo thân thiện + gợi ý Plus/đợi tháng sau, không lỗi thô.
