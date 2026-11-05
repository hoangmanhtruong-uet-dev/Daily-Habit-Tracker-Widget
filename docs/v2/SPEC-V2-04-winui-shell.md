# SPEC-V2-04 — Ứng dụng WinUI 3 (`HabitGrid.App`)

> Phát hành: [v2.0] • Phụ thuộc: SPEC-V2-01, SPEC-V2-02 (A), 01-DESIGN-SYSTEM, 02-UX-SCREENS

## 1. Nền tảng
- Windows App SDK bản stable mới nhất (kiểm tra khi bắt đầu; tối thiểu hỗ trợ Mica, `AppWindow`, Widgets provider), .NET 10, C# 13+.
- Đóng gói **MSIX** (packaged) để có identity: cần cho Store, toast, StartupTask, Widgets Board.
- Kiến trúc x64 + ARM64. Trim + ReadyToRun bật cho Release; NativeAOT thử nghiệm sau.
- Thư viện: CommunityToolkit.Mvvm (MVVM source generators), CommunityToolkit.WinUI (controls/helpers), Microsoft.Extensions.Hosting + DI, Win2D (vẽ heatmap), H.NotifyIcon (tray) hoặc giữ tray Win32 của v1, Lottie/Rive cho linh vật (chọn trong tuần 2).

## 2. Cấu trúc solution mới
```
src/
  HabitGrid.Core            (nâng từ v1)
  HabitGrid.Contracts       (mới: DTO API, phiên bản)
  HabitGrid.Data            (nâng từ v1: schema v3)
  HabitGrid.Sync            (mới)
  HabitGrid.Platform        (nâng từ v1: bỏ WPF, thêm hotkey, WNS, credential)
  HabitGrid.App             (mới, WinUI 3, thay HabitGrid.UI + App WPF)
  HabitGrid.WidgetHost      (mới: tiến trình widget ghim desktop — công nghệ theo POC-V2)
  HabitGrid.WidgetsBoard    (mới: COM provider cho Windows Widgets Board)
  HabitGrid.Server          (mới)
  HabitGrid.Package         (MSIX)
tests/
  *.Tests (giữ) + HabitGrid.App.UiTests (WinAppDriver/FlaUI) + HabitGrid.Server.Tests
```
Project WPF cũ (`HabitGrid.UI`, `HabitGrid.App` v1) chuyển vào `legacy/` tới khi v2.0 phát hành rồi xóa.

## 3. Kiến trúc trong app
- **Generic Host + DI**: mọi service đăng ký ở `App.xaml.cs → ConfigureServices`. Không dùng static service locator kiểu `App.Services` của v1 ngoài điểm khởi động.
- **MVVM**: `ObservableObject`, `[ObservableProperty]`, `[RelayCommand]`. ViewModel không tham chiếu kiểu WinUI (test được bằng xUnit thường).
- **Điều hướng**: `INavigationService` (Frame + NavigationView), tham số kiểu mạnh, connected animation giữa danh sách và chi tiết.
- **Dữ liệu phản ứng**: `IDataChangeBus` (SPEC-V2-02) → ViewModel tải lại phần bị ảnh hưởng, không tải lại toàn trang.
- **Luồng**: mọi I/O async; cập nhật UI qua `DispatcherQueue`. Không `async void` ngoài event handler, và mọi event handler async MUST có try/catch ghi log.
- **Lỗi toàn cục**: `UnhandledException`, `TaskScheduler.UnobservedTaskException`, `AppDomain.UnhandledException` → ghi log + crash report (SPEC-V2-11) + trang lỗi thân thiện thay vì thoát im lặng.
- **Khởi động**: single-instance qua `AppInstance.FindOrRegisterForKey` (Windows App SDK) **trước** khi mở DB; kích hoạt lại chuyển tham số/protocol tới instance chính.
- **Protocol** `habitgrid://` cho: link mời bạn, mở thói quen, quick check-in từ toast, callback OAuth (nếu không dùng loopback).

## 4. Shell
- Cửa sổ `AppWindow` với title bar tùy biến (kéo được, có ô tìm kiếm mở Ctrl+K, chấm trạng thái sync, avatar).
- Nền Mica (Win11), fallback Acrylic/màu đặc ở Win10.
- NavigationView trái, tự thu gọn khi hẹp. Ghi nhớ kích thước/vị trí cửa sổ theo màn hình.
- Đóng cửa sổ = thu về tray (như v1), có tùy chọn "Thoát hẳn khi đóng".

## 5. Các trang (theo 02-UX-SCREENS)
| Trang | ViewModel | Ghi chú kỹ thuật |
|---|---|---|
| Onboarding | `OnboardingViewModel` | Cửa sổ riêng, lưu tiến độ để mở lại giữa chừng |
| Hôm nay | `TodayViewModel` | Nhóm theo `TimeOfDay`; danh sách ảo hóa (`ItemsRepeater`) |
| Thói quen | `HabitsViewModel` | Kéo-thả cập nhật `sort_key` bằng FractionalIndex |
| Chi tiết | `HabitDetailViewModel` | Heatmap Win2D; biểu đồ bằng thư viện chart nhẹ (LiveCharts2 hoặc vẽ tay Win2D) |
| Tạo/sửa | `HabitEditorViewModel` | `ContentDialog` toàn chiều cao; validation từ Core |
| Insights | `InsightsViewModel` | Tính nền, cache theo khoảng |
| Buddy | `BuddyViewModel` | Animation dừng khi trang không hiển thị |
| Cài đặt | `Settings*ViewModel` | Mỗi nhóm một trang con |
| Bạn bè [v2.2] | `FriendsViewModel` | |

## 6. Đa ngôn ngữ & trợ năng
- Tài nguyên `.resw` (VI, EN ở v2.0; cấu trúc sẵn cho thêm ngôn ngữ). Đổi ngôn ngữ áp dụng ngay không khởi động lại (dùng lại ý tưởng `LocalizationManager` v1, nhưng **không** trả về `Binding` thô từ markup extension: đây chính là lỗi làm v1 sập).
- Điều hướng hoàn toàn bằng bàn phím, thứ tự Tab hợp lý, focus visual rõ.
- Narrator đọc được heatmap: mỗi ô có tên kiểu "Thứ Ba 4 tháng 11, đã xong, 3 trên 3 ly".
- High contrast: dùng `ThemeResource` hệ thống khi bật.

## 7. Hiệu năng (ngân sách)
| Chỉ số | Mục tiêu |
|---|---|
| Khởi động nguội tới trang Hôm nay | < 1,5 s (máy tầm trung) |
| Check-in → phản hồi hình ảnh | < 50 ms |
| RAM app (cửa sổ mở) | < 150 MB |
| RAM khi thu về tray | < 60 MB |
| Cuộn danh sách 50 thói quen | 60 fps |

## 8. Tiêu chí nghiệm thu
1. Mọi trang trong §5 dựng được trong smoke test UI (FlaUI) và trong một lần chạy thật có ảnh chụp đính kèm báo cáo.
2. Không còn tham chiếu WPF trong solution chính.
3. Khởi động lần hai chuyển về instance đầu, không mở DB lần hai.
4. Ném exception giả lập ở mọi ViewModel → app không thoát, hiện lỗi thân thiện, có log.
5. Ngân sách hiệu năng §7 đo bằng script trong CI (máy runner Windows) hoặc thủ công có ghi số.
6. Accessibility Insights for Windows: 0 lỗi nghiêm trọng trên các trang chính.

## 9. Tiến độ triển khai (cập nhật 05/11/2026)

**Đã làm (lát cắt dọc đầu tiên):**
- `HabitGrid.Presentation` (net10, không tham chiếu UI): `TodayViewModel`, `HabitsViewModel`, `ShellViewModel`, `HabitCreationService`, `Localizer` (VI/EN `.resx`), `IUiDispatcher`. 30 test chạy trên SQLite v3 thật.
- `HabitGrid.WinUI` (WinUI 3, Windows App SDK 2.6.5, unpackaged): shell Mica + NavigationView, trang **Hôm nay** (nhóm theo khung giờ, check-in một chạm, hoàn tác, tiến độ ngày, mini heatmap 7 ngày), trang **Thói quen** (lưu trữ, xóa có hoàn tác, kéo-thả đổi thứ tự), hộp thoại thêm thói quen, các trang giữ chỗ (Insights, Buddy, Cài đặt).
- Khởi động: single instance (`AppInstance`) kiểm tra **trước** khi mở CSDL; import v1→v3 ở lần chạy đầu; log file xoay vòng; mọi handler async có try/catch; bắt lỗi toàn cục.
- Chế độ `--smoke`: dựng mọi trang, bấm nút check-in qua cây XAML, kiểm tra DB, hoàn tác, mở hộp thoại; thoát 0/2. Từ chối chạy (mã 3) nếu không đặt `HABITGRID_DATA_DIR`. Có bước CI (chưa bắt buộc).

**Khác với spec:**
- Project WinUI tên `HabitGrid.WinUI` (không đè `HabitGrid.App` của v1); WPF cũ chưa chuyển vào `legacy/`.
- DI dùng `Microsoft.Extensions.DependencyInjection` thuần, chưa dùng Generic Host (chưa cần cấu hình/hosted service).
- Smoke test tự viết trong app thay vì FlaUI để chạy được ngay không cần driver; FlaUI vẫn là hướng cho kiểm tra từ bên ngoài.

**Cập nhật 2 (05/11/2026): trình tạo/sửa đầy đủ + onboarding.**
- `HabitEditorViewModel` + `HabitEditorView` (hộp thoại): tên, emoji (24 gợi ý hoặc gõ tự do), 12 màu preset kèm dải xem trước thang 5 mức (OKLCH), 4 loại, mục tiêu + đơn vị, 5 kiểu tần suất, khung giờ, tối đa 5 giờ nhắc (thông minh / nhắc lại), mood/ghi chú/freeze, mô tả, thẻ, ngày bắt đầu. Dùng chung cho tạo mới và sửa; lỗi hiển thị bằng ngôn ngữ người dùng và giữ hộp thoại mở. Sửa thao tác trên bản sao nên lưu lỗi không làm bẩn danh sách đang hiển thị.
- `OnboardingViewModel` + `OnboardingPage`: chào → chủ đề (≤3) → thói quen (≤3, sửa tên/mục tiêu) → đặt tên Buddy → nhắc nhở. Tiến độ được lưu để mở lại giữa chừng; tự bỏ qua nếu đã có dữ liệu (ví dụ nhập từ v1). 64 mẫu thói quen, 8 chủ đề, VI/EN.
- Giới hạn gói Free (5 thói quen đang chạy) áp ở editor và onboarding; lưu trữ để nhường chỗ; sửa thói quen có sẵn không bị chặn. Màn paywall thật thuộc SPEC-V2-10.
- Ngôn ngữ mặc định theo ngôn ngữ giao diện của Windows (vi → Việt, còn lại → English) cho tới khi có trang Cài đặt.
- Smoke test chạy trọn hành trình người dùng mới và xuất ảnh từng bước vào `<dữ liệu>\shots\`.
- Chưa làm trong onboarding: bước "ghim widget" (chờ SPEC-V2-05) và hướng dẫn check-in đầu tiên.

**Chưa làm:** trang chi tiết thói quen, Insights, Buddy, Cài đặt (đổi ngôn ngữ/theme), command palette, tray, title bar tìm kiếm, MSIX, đo ngân sách hiệu năng (§7), kiểm tra Accessibility Insights, Narrator cho heatmap. Chưa kiểm tra hiển thị ở DPI khác và Windows 10.

