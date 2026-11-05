# SPEC-V2-11 — Chất lượng, CI, telemetry, hiệu năng, trợ năng

> Phát hành: xuyên suốt, bắt đầu từ Giai đoạn 0 • Áp dụng cho mọi gói

## 1. Bài học từ v1
Bản v1 có 207 unit test pass nhưng app sập ngay khi mở vì: (1) markup extension trả về `Binding` thô; (2) handler `ValueChanged` của Slider chạy trong `InitializeComponent` trước khi control phía sau được tạo. Không test nào dựng XAML thật, và báo cáo "Go-Live Ready" không có bước chạy app. v2 sửa quy trình bằng các mục dưới.

## 2. Kim tự tháp test
| Tầng | Công cụ | Phạm vi | Bắt buộc |
|---|---|---|---|
| Unit | xUnit, FluentAssertions, FsCheck | Core, Sync merge, ViewModel | Core ≥ 90% line coverage |
| Integration | xUnit + SQLite thật; Testcontainers Postgres | Data, migration, server API | Mọi repository, mọi endpoint |
| Contract | Test chung DTO client/server | Contracts | Mọi DTO tuần tự hóa 2 chiều |
| UI smoke | FlaUI (UIA) trên app thật đóng gói | Mở app, đi qua mọi trang, mở mọi dialog, tạo/check/xóa 1 thói quen | Chạy mỗi PR |
| Visual | Ảnh chụp từng trang × theme × ngôn ngữ, so sánh với ảnh chuẩn (ngưỡng sai khác) | Trang chính, widget, share card | Chạy hằng đêm |
| Fuzz | Sync 3 thiết bị (SPEC-V2-02 C.3) | Sync | Hằng đêm |
| Thủ công | Checklist phát hành §8 | Toàn app | Mỗi bản phát hành |

## 3. CI/CD (GitHub Actions)
- Khởi tạo git repo + `.gitignore` (bin/obj/AppPackages/*.db) ngay Giai đoạn 0 (v1 chưa có version control).
- Nhánh: `main` luôn phát hành được; tính năng qua PR; bắt buộc review (người hoặc `/code-review`) + CI xanh.
- Pipeline PR (Windows runner): restore → build Release (warning = error ở dự án mới) → unit + integration → đóng gói MSIX test-signed → UI smoke → upload artifact.
- Hằng đêm: visual test, fuzz, benchmark, WACK.
- Server: build image → deploy staging → smoke API → duyệt tay → prod.
- Phân tích tĩnh: .NET analyzers mức `recommended`, `dotnet format` kiểm trong CI, quét lỗ hổng phụ thuộc (`dotnet list package --vulnerable`), quét secret.

## 4. Log, crash, telemetry
- **Log cục bộ**: Serilog → file xoay vòng `%LOCALAPPDATA%\HabitGrid\logs`, giữ 7 ngày, không ghi tên thói quen/ghi chú ở mức Info.
- **Crash report**: Sentry (hoặc tương đương) **opt-in** ở onboarding/cài đặt; khi tắt, crash vẫn ghi log cục bộ và trang lỗi cho phép người dùng tự gửi file log.
- **Telemetry sản phẩm** (opt-in, ẩn danh, ID ngẫu nhiên xoay vòng 90 ngày): sự kiện `app_open`, `onboarding_step`, `habit_created(kind, frequency_type)`, `checkin(source: app|widget|toast|quick|board)`, `widget_pinned(type,size)`, `paywall_view(source)`, `purchase(product)`, `feature_used(key)`. Không gửi tên, ghi chú, mood, giá trị.
- Dashboard KPI theo 00-MASTER-PLAN §7.

## 5. Hiệu năng
- Ngân sách tại SPEC-V2-04 §7 và SPEC-V2-05 §6.
- BenchmarkDotNet cho Core (streak, stats, insight) và merge; cảnh báo nếu chậm hơn 20% so với main.
- Kiểm rò rỉ bộ nhớ: chạy app 8 giờ với script check-in ngẫu nhiên, RAM không tăng quá 10%.

## 6. Trợ năng
- Mục tiêu WCAG 2.2 AA tương đương cho desktop.
- Kiểm tra: Accessibility Insights (FastPass) trong UI smoke; tương phản token kiểm bằng test; Narrator duyệt tay mỗi bản phát hành.
- Hỗ trợ: điều hướng bàn phím đầy đủ, high contrast, chữ phóng to 200% không vỡ bố cục, giảm chuyển động.

## 7. Đa ngôn ngữ
- VI + EN ở v2.0; chuỗi trong `.resw`, khóa có tiền tố theo màn hình.
- Test: không còn chuỗi cứng trong XAML/ViewModel (analyzer hoặc script), mọi khóa có đủ 2 ngôn ngữ, giả lập "pseudo-locale" dài hơn 40% để bắt lỗi tràn chữ.
- Định dạng ngày/số theo locale; số nhiều đúng ngữ pháp EN.

## 8. Checklist trước mỗi bản phát hành
- [ ] CI xanh, hằng đêm xanh 3 ngày liên tiếp
- [ ] Cài sạch trên Win11 và Win10 máy thật (không có .NET SDK)
- [ ] Nâng cấp từ bản trước với dữ liệu thật, dữ liệu còn đủ
- [ ] Onboarding → widget ≤ 45 s
- [ ] Mua/khôi phục Plus trên tài khoản test
- [ ] Toast khi app tắt, phím tắt toàn cục, Widgets Board
- [ ] Ngắt mạng 1 ngày rồi nối lại (từ v2.1)
- [ ] Narrator đọc được trang Hôm nay và widget
- [ ] WACK pass
- [ ] Ghi chú phát hành VI/EN

## Tiến độ triển khai (06/11/2026)
- **Báo lỗi cục bộ**: mọi ngoại lệ không bắt được (UI, AppDomain, lỗi khởi động) ghi một file `crash-*.txt` trong thư mục log (phiên bản app, hệ điều hành, nơi xảy ra, ngoại lệ, 40 dòng log gần nhất; giữ 20 file). Lần mở sau app hỏi một lần, cho mở thư mục hoặc sao chép nội dung để người dùng **tự gửi** — chưa có Sentry/máy chủ nên không tải gì lên. Cài đặt → Hỗ trợ: phiên bản + mở thư mục log.
- **Trợ năng tự động trong smoke test**: duyệt cây giao diện 6 trang (Hôm nay, Thói quen, Chi tiết, Insights, Buddy, Cài đặt), mọi control tương tác đang hiện phải có tên cho trình đọc màn hình; lần chạy đầu bắt được 5 nút thiếu tên (đã sửa). Test độ tương phản token màu đã có từ R5.
- **Windows 10/11**: thêm khai báo `supportedOS` vào manifest (thiếu thì một số API báo sai phiên bản). App yêu cầu tối thiểu Windows 10 2004 (19041).
- **Chưa kiểm được trên CI**: Narrator bằng tai người, DPI cao/nhiều màn hình, Windows 10 (GitHub Actions không còn máy Win10) → xem `QA-CHECKLIST-v2.0.md`. Telemetry sản phẩm opt-in, FlaUI trên bản MSIX, visual diff hằng đêm, WACK: chưa làm.
