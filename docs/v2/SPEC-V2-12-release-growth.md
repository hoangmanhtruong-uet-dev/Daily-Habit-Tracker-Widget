# SPEC-V2-12 — Phát hành, Store, tăng trưởng GenZ

> Phát hành: cuối mỗi giai đoạn • Phụ thuộc: SPEC-V2-11

## 1. Đóng gói & ký
- MSIX một package chứa App + WidgetHost + WidgetsBoard provider; `runFullTrust`; khai báo: StartupTask, toast activator (COM), Widgets provider, protocol `habitgrid`, AppExecutionAlias `habitgrid` (gõ lệnh trong Terminal để quick check-in, điểm cộng với dân dev).
- Bản Store: Microsoft ký khi phát hành, không cần chứng chỉ riêng.
- Bản ngoài Store (beta tester, sideload): ký bằng **Azure Trusted Signing** để không bị Smart App Control chặn (máy dev hiện tại đã gặp chặn với exe chưa ký).
- Kênh: Store flight "Beta" cho tester → "Public".
- Phiên bản: `2.MINOR.PATCH.0`; ghi chú phát hành VI/EN.

## 2. Store listing
- Tên: "HabitGrid: Habit Tracker Widget" (giữ từ khóa "habit tracker", "widget" trong tên).
- Mô tả ngắn VI/EN nhấn 3 điểm: sống trên desktop, Buddy dễ thương, miễn phí đủ dùng.
- 8–10 ảnh chụp 16:9 dàn dựng (không phải ảnh chụp thô): widget trên desktop đẹp với hình nền thẩm mỹ, trang Hôm nay, Buddy, theme, share card, Focus, Insights, bạn bè.
- Video trailer 30 giây: hình nền đẹp → widget xuất hiện → bấm ô sáng lên → Buddy nhảy → quick check-in bằng phím tắt.
- Từ khóa: habit tracker, streak, daily planner, productivity widget, study, routine, pomodoro, thói quen, ghi chép thói quen.
- Danh mục: Productivity. Độ tuổi: 12+.
- Chính sách riêng tư cập nhật theo SPEC-V2-03 §5 và SPEC-V2-07 §3.4, đăng ở trang web.

## 3. Trang web tối thiểu
Landing 1 trang (tĩnh): video, 3 lợi ích, nút "Get it from Microsoft" badge, FAQ, chính sách riêng tư, điều khoản, trang hỗ trợ `/invite/<code>` (mở app hoặc dẫn tới Store).

## 4. Kế hoạch ra mắt GenZ
| Giai đoạn | Việc |
|---|---|
| T−4 tuần | Discord cộng đồng; danh sách chờ trên landing; tuyển 50 beta tester (nhóm sinh viên IT, cộng đồng study-with-me VN) |
| T−2 tuần | Gửi bản beta, thu feedback, sửa lỗi; xin testimonial + ảnh desktop đẹp của tester |
| T0 | Phát hành Store; giảm giá trọn đời 14 ngày; đăng Product Hunt (EN), Reddit r/Windows11, r/productivity, r/desktops (desk setup); Facebook group sinh viên VN, Spiderum/Viblo cho dân code |
| T+1 → T+8 tuần | Nội dung ngắn 3 bài/tuần: TikTok/Reels/Shorts "desk setup aesthetic", "study with me + HabitGrid", "30 ngày không lướt điện thoại khuya", timelapse heatmap lấp đầy; hợp tác 5–10 creator nhỏ (10k–100k) mảng study/desk setup, trả bằng mã Plus trọn đời + phí nhỏ |
| Liên tục | Theme theo mùa (Tết, Halloween, mùa thi) làm lý do đăng bài; Wrapped cuối năm là đợt viral chính |

Vòng lặp tăng trưởng trong sản phẩm: share card và Wrapped (có watermark), link mời bạn (cả hai nhận 1 tuần Plus), thử thách nhóm.

## 5. Phản hồi & hỗ trợ
- Trong app: "Gửi góp ý" (form → email/hệ thống ticket), "Đề xuất tính năng" (link board bình chọn công khai).
- Trả lời đánh giá Store trong 48 giờ.
- Hỏi đánh giá Store (`StoreRequestRateAndReviewAppAsync`) chỉ sau khoảnh khắc tích cực (mốc streak 7, ngày hoàn hảo thứ 3), tối đa 1 lần/90 ngày.

## 6. Tiêu chí nghiệm thu
1. Gói MSIX qua WACK, cài trên Win10 22H2 và Win11, StartupTask/toast/Widgets Board/alias hoạt động.
2. Listing đủ VI/EN, ảnh, video, chính sách riêng tư.
3. Landing hoạt động, link mời mở đúng app.
4. Kênh beta flight có ≥ 30 tester hoạt động trước ngày phát hành.
