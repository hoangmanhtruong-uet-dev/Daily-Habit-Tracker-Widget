# HabitGrid — Hướng Dẫn & Lộ Trình Đặc Tả Kỹ Thuật (Master Spec Guide)

> **Tên ứng dụng chính thức**: **HabitGrid**  
> **Mô tả cốt lõi**: Widget theo dõi thói quen dạng lưới kiểu GitHub (Heatmap), ghim trực tiếp trên desktop Windows 10/11, phát hành trên Microsoft Store (MSIX).  
> **Nền tảng công nghệ**: C# 12, .NET 8, WPF + WPF-UI, SQLite, Windows.Services.Store.  
> **Trạng thái**: Đã chốt kiến trúc & phân rã thành các gói công việc độc lập cho AI Coding.

---

## 1. CẤU TRÚC BỘ TÀI LIỆU ĐẶC TẢ (`docs/specs/`)

Bộ tài liệu đặc tả được module hóa theo từng gói công việc khép kín, kèm tiêu chuẩn kiểm thử (Acceptance Criteria) và hợp đồng interface cụ thể để AI coding có thể thực thi tuần tự mà không bị xung đột ngữ cảnh:

| Mã tài liệu | Tên module & Gói công việc | Mô tả & Trọng tâm | Tham chiếu mã nguồn dự kiến |
|---|---|---|---|
| [`POC-desktop-pinning.md`](file:///d:/MyProjects/MS/Daily%20Habit%20Tracker%20Widget/docs/specs/POC-desktop-pinning.md) | **Giai đoạn 0: POC Ghim Desktop** | Thử nghiệm kỹ thuật Win32 đưa widget xuống lớp Desktop (WorkerW / Bottom-most fallback), đo đạc tiêu chí sống sót sau Win+D và nhận chuột | `HabitGrid.Platform/Desktop/` |
| [`SPEC-01-core-domain.md`](file:///d:/MyProjects/MS/Daily%20Habit%20Tracker%20Widget/docs/specs/SPEC-01-core-domain.md) | **Gói 1: Core Domain & Test** | Thực thể Habit, Entry, Logic tính Streak, Lịch lặp (Schedule), Mốc chuyển ngày (DayProvider), Bộ sinh màu (Palette) | `HabitGrid.Core/`, `HabitGrid.Core.Tests/` |
| [`SPEC-02-data-layer.md`](file:///d:/MyProjects/MS/Daily%20Habit%20Tracker%20Widget/docs/specs/SPEC-02-data-layer.md) | **Gói 2: Data Layer & Backup** | SQLite WAL, Migration có phiên bản, Repository pattern, Dịch vụ xuất/nhập JSON & CSV (Gộp/Thay thế) | `HabitGrid.Data/`, `HabitGrid.Data.Tests/` |
| [`SPEC-03-widget-desktop.md`](file:///d:/MyProjects/MS/Daily%20Habit%20Tracker%20Widget/docs/specs/SPEC-03-widget-desktop.md) | **Gói 3: Widget & Lưới Desktop** | Control tự vẽ DrawingVisual/OnRender (tối ưu 371 ô), Hit-test tọa độ, Cỡ S/M/L, Per-monitor DPI v2, Cửa sổ Widget & Chế độ Ghim | `HabitGrid.UI/Controls/`, `HabitGrid.UI/Widgets/` |
| [`SPEC-04-management-dashboard.md`](file:///d:/MyProjects/MS/Daily%20Habit%20Tracker%20Widget/docs/specs/SPEC-04-management-dashboard.md) | **Gói 4: Bảng Điều Khiển Quản Lý** | Cửa sổ chính WPF-UI Fluent, Quản lý CRUD Habit (kéo thả sắp xếp), Trang chi tiết habit 52 tuần & Thống kê, Onboarding < 30s | `HabitGrid.UI/Views/`, `HabitGrid.UI/ViewModels/` |
| [`SPEC-05-settings-theming-i18n.md`](file:///d:/MyProjects/MS/Daily%20Habit%20Tracker%20Widget/docs/specs/SPEC-05-settings-theming-i18n.md) | **Gói 5: Cài Đặt, Theme & Đa Ngôn Ngữ** | Cài đặt tự động lưu, Sáng/Tối/Hệ thống, Nền Acrylic/Đặc/Không nền, Bo góc & Độ mờ, `.resx` Việt/Anh, Hỗ trợ Trợ năng (WCAG AA) | `HabitGrid.UI/Settings/`, `HabitGrid.UI/Resources/` |
| [`SPEC-06-system-integration.md`](file:///d:/MyProjects/MS/Daily%20Habit%20Tracker%20Widget/docs/specs/SPEC-06-system-integration.md) | **Gói 6: Tích Hợp Hệ Sinh Thái Windows** | Single Instance (Mutex + NamedPipe), Khay hệ thống (Tray Icon), Toast Notifications tương tác nhanh, StartupTask, Xử lý Power/Clock change | `HabitGrid.App/`, `HabitGrid.Platform/` |
| [`SPEC-07-licensing-pro.md`](file:///d:/MyProjects/MS/Daily%20Habit%20Tracker%20Widget/docs/specs/SPEC-07-licensing-pro.md) | **Gói 7: Giấy Phép & Nâng Cấp Pro** | MS Store `StoreContext` Durable Add-on (`pro_unlock`), DPAPI cache offline 30 ngày, Soft paywall, Giới hạn 2 habits (tính cả archive) | `HabitGrid.Platform/Licensing/`, `HabitGrid.UI/Views/Pro/` |
| [`SPEC-08-packaging-store-release.md`](file:///d:/MyProjects/MS/Daily%20Habit%20Tracker%20Widget/docs/specs/SPEC-08-packaging-store-release.md) | **Gói 8: Đóng Gói MSIX & Phát Hành Store** | Dự án WAP, Manifest `runFullTrust`, Asset sizes & Visual elements, Kiểm tra WACK, Listing Song ngữ VI/EN, Privacy Policy | `HabitGrid.Package/`, `build/` |

---

## 2. QUY TRÌNH GIAO VIỆC CHO AI CODING (PROMPT PROTOCOL)

Khi làm việc với các công cụ AI (Cline, Claude Code, Antigravity), cần tuân thủ nghiêm ngặt nguyên tắc **Single-Task Execution**:

1. **Chỉ giao 1 file SPEC tại một thời điểm**: Không bao giờ yêu cầu AI làm đồng thời nhiều SPEC.
2. **Quy trình 5 bước bắt buộc trong mỗi prompt giao task**:
   - **Bước 1 (Đọc & Hiểu)**: Yêu cầu AI đọc kỹ file SPEC tương ứng và codebase hiện có, đối chiếu các Interface phụ thuộc.
   - **Bước 2 (Kế hoạch hành động)**: AI trình bày ngắn gọn các file dự kiến tạo/sửa và phương án kiểm thử.
   - **Bước 3 (Thực thi code)**: Viết code sạch, đúng kiến trúc, chú thích rõ ràng.
   - **Bước 4 (Chạy Build & Unit Test)**: Chạy `dotnet test` hoặc build kiểm chứng, đảm bảo không có warning nghiêm trọng và 100% test pass.
   - **Bước 5 (Báo cáo)**: Liệt kê các thay đổi và xác nhận tiêu chuẩn nghiệm thu (Acceptance Criteria) đã đạt.

---

## 3. CÁC QUYẾT ĐỊNH THIẾT KẾ ĐÃ ĐƯỢC CHỐT

1. **Công nghệ nền tảng**: `.NET 8` + `WPF` + thư viện `WPF-UI` (Fluent Design).
2. **Kiến trúc giải pháp**: Phân lớp rõ ràng (`Core` -> `Data` -> `Platform` -> `UI` -> `App` -> `Package`).
3. **Mô hình bản quyền (Free vs Pro)**:
   - **Free**: Tối đa 2 thói quen. **Thói quen đã lưu trữ (Archived) vẫn tính vào hạn mức 2 thói quen** (người dùng phải xóa hẳn để lấy chỗ nếu không nâng cấp Pro). 1 bảng màu xanh lá mặc định. Không có tính năng tự động xuất/nhập sao lưu, nhưng có nút "Mở thư mục dữ liệu" (`%LocalAppData%\HabitGrid`) để đảm bảo minh bạch dữ liệu.
   - **Pro ($4.99 mua đứt, sale $3.99 hai tuần đầu)**: Không giới hạn thói quen, tùy biến màu sắc heatmap (preset + color picker), xuất/nhập JSON & CSV tự động, tự động sao lưu.
   - **Hạ cấp/Mất license**: Dữ liệu người dùng được bảo vệ tuyệt đối (không bị xóa), 2 thói quen đầu tiên tiếp tục dùng bình thường, các thói quen còn lại chuyển sang trạng thái "Chỉ đọc" (Read-only) với biểu tượng khóa mờ.
