# POC-V2 — Ghim cửa sổ WinUI 3 xuống desktop

> Ngày: 05/11/2026 • Mã nguồn: `poc/HabitGrid.Poc.WinUI` • Môi trường: Windows 11 (build 26100), .NET 10.0.403, Windows App SDK 2.6.5, unpackaged + self-contained, x64.

## Phương pháp
Cửa sổ WinUI 3 (`AppWindow`, bỏ khung/tiêu đề, `IsShownInSwitchers=false`) được ghim bằng đúng `DesktopHostService` của v1 (WorkerW/Progman). Chạy thật, chụp màn hình, đọc log.

## Kết quả theo 7 tiêu chí POC

| # | Tiêu chí | Kết quả | Bằng chứng |
|---|---|---|---|
| 1 | Nằm trên hình nền, dưới mọi cửa sổ | **Đạt** | Chế độ DesktopLevel áp dụng thành công, không phải fallback. Khi VS Code che desktop, widget bị che đúng; hiện lại khi tạm hiện desktop |
| 2 | Nhận chuột | **Chưa kết luận** | `WindowFromPoint` tại vị trí nút trả về cửa sổ con WinUI (`DesktopChildSiteBridge`, cha là cửa sổ POC), nên input được định tuyến tới đúng nơi. Chưa quan sát được sự kiện click thật (lần thử bấm tự động bị lệch thời điểm). **Cần thử tay** |
| 3 | Sống sót Win+D | **Đạt** | Widget vẫn hiển thị nguyên vẹn khi hiện desktop (ảnh chụp) |
| 4 | Phục hồi sau khi Explorer chết | **Chưa thử** | Cần kill `explorer.exe`, sẽ làm gián đoạn máy; thử khi có máy test |
| 5 | Đa màn hình & DPI | **Chưa thử** | Máy chỉ có 1 màn hình; DPI 125% hiển thị đúng, không lệch |
| 6 | Ẩn Taskbar / Alt+Tab | **Đạt một phần** | `IsShownInSwitchers=false`; ảnh chụp không thấy nút taskbar riêng. Chưa kiểm Alt+Tab/Win+Tab bằng tay |
| 7 | Tài nguyên | **Không đạt ngân sách** | Xem dưới |

## Đo tài nguyên (một cửa sổ, nội dung rất đơn giản)
| Cấu hình | Working set | Private | CPU nhàn rỗi |
|---|---|---|---|
| Debug | 155 MB | — | 0% (10 s) |
| Release | 89 MB | 47 MB | ≈3,4% trong 10 s đo đầu (chưa rõ do khởi động hay do hiệu ứng nền) |

Ngân sách của SPEC-V2-05: 3 widget L + Today List < 60 MB, CPU nhàn rỗi < 0,5%. Mỗi tiến trình WinUI 3 self-contained đã gần 90 MB WS chỉ với 1 cửa sổ trống, nên **phương án A không đạt ngân sách** dù ghim chạy được.

## Kết luận tạm thời
- **Về chức năng**: WinUI 3 + `DesktopHostService` ghim được xuống desktop. Rủi ro kỹ thuật lớn nhất (có ghim được hay không) **đã được gỡ**.
- **Về tài nguyên**: không nên chạy widget hosting bằng WinUI 3 như một tiến trình riêng.
- **Đề xuất**: WidgetHost dùng **phương án C (WPF, tái dùng code v1 đã được kiểm chứng) cho v2.0**; app chính vẫn WinUI 3. Cân nhắc phương án B (Win32 + Direct2D, nhẹ nhất) ở v2.1 nếu số liệu RAM của WPF cũng không đạt. Cần **đo RAM/CPU của WidgetHost WPF với 3 widget** trước khi chốt.
- Cần bạn xác nhận quyết định này trước khi viết WidgetHost (ảnh hưởng SPEC-V2-05 §2.4).

## Việc còn lại của POC
1. Thử tay click, Alt+Tab, kéo qua màn hình thứ hai, đổi DPI.
2. Kill Explorer trên máy test (tiêu chí 4).
3. Đo CPU nhàn rỗi bản Release trong 2 phút sau khi ổn định.
4. Đo RAM WidgetHost WPF với 3 widget.

## Cập nhật 05/11/2026 — quyết định và đo WPF
- **Quyết định:** WidgetHost v2.0 dùng WPF (đã ghi vào SPEC-V2-05 §1).
- Core/Data/Platform/UI/App và 4 project test đã nâng lên **.NET 10**; 208 test pass; cảnh báo lỗ hổng SQLite (NU1903) đã xử lý bằng ghim `SQLitePCLRaw.lib.e_sqlite3` 2.1.14.
- **Đo sơ bộ app v1 WPF (Debug, `--background`, 1 widget, cửa sổ chính dựng ẩn):** 156 MB working set / 91 MB private, CPU nhàn rỗi 0% trong 20 s. Đây là toàn bộ app, **không** đại diện cho WidgetHost. Cần đo lại khi `HabitGrid.WidgetHost` tồn tại (Release, chỉ widget, 3 widget L + Today List). Nếu vẫn > 60 MB thì cân nhắc phương án B.

## Cập nhật 05/11/2026 (2) — đo WidgetHost WPF với 0/1/3/6 widget

**Cách đo:** `tools/WidgetHostSpike` (WPF, Release, runtime .NET 10.0.14) dựng N widget v1 cỡ Large (52 tuần, 380 ngày dữ liệu giả), ghim DesktopLevel, lấy mẫu 5 s/lần trong 70 s. Chạy bằng `.github/workflows/widget-spike.yml` trên runner `windows-latest` (Windows 10.0.26100, 4 nhân). **Đây là máy CI, không phải máy người dùng**; cửa sổ không hiển thị trên một desktop thật nên chi phí vẽ/DWM không nằm trong số đo.

| Cấu hình | Working set | Private | CPU nhàn rỗi (sau 15 s) |
|---|---|---|---|
| 0 widget (chỉ WPF + runtime) | 40,3 MB | 11,1 MB | 0,01% |
| 1 widget | 101,1 MB | 44,8 MB | 0,01% |
| 3 widget | 91,7 MB | 37,2 MB | 0,02% |
| 6 widget | 100,1 MB | 43,4 MB | 0,00% |
| 3 widget + thu gọn working set lúc nhàn rỗi | **14,4 MB** (đỉnh 91,5) | 34,3 MB | 0,07% |

**Đọc số liệu**
- Chi phí chủ yếu **cố định**: widget đầu tiên kéo thêm ~50–60 MB working set (nạp WPF/PresentationFramework, WPF-UI, render); thêm widget thứ 2–6 gần như không tốn thêm. Số widget không phải yếu tố quyết định.
- CPU nhàn rỗi ≈ 0% (khác 3,4% của WinUI 3, vốn là 10 s đầu gồm cả khởi động) → **đạt**.
- **Working set 92–101 MB không đạt** mức < 60 MB của SPEC-V2-05 §tiêu chí 3, và gần như bằng WinUI 3 (89 MB). Private 37–45 MB **đạt** nếu hiểu ngân sách theo bộ nhớ riêng.
- Gọi `EmptyWorkingSet` khi nhàn rỗi đưa working set xuống 14 MB và giữ ổn định (lên lại ~0,5 MB/5 s khi tiến trình chạm trang), nhưng private commit vẫn ~34 MB. Cột "Bộ nhớ" trong tab Processes của Task Manager là private working set, nên người dùng sẽ thấy khoảng 14 MB sau khi thu gọn.

**Giới hạn của phép đo (chưa biết)**
- Chưa đo trên máy thật có hiển thị (DWM, độ phân giải/DPI cao), chưa đo Windows 10 22H2, chưa đo 10 phút liên tục theo tiêu chí gốc (chỉ 70 s).
- Số của WidgetHost thật (có kho dữ liệu SQLite, đồng bộ, tray) sẽ cao hơn spike; spike chỉ gồm cửa sổ widget và dữ liệu giả.
- `EmptyWorkingSet` làm tiến trình nạp lại trang khi có sự kiện (đổi ngày, check-in) → độ trễ nhỏ chưa đo.

**Kết luận tạm:** giữ phương án C (WPF) cho v2.0 với hai điều kiện: (1) hiểu ngân sách RAM là **private working set** và thu gọn working set khi nhàn rỗi; (2) đo lại trên máy thật khi WidgetHost thật tồn tại. Nếu sau đó private vượt 60 MB thì mới cân nhắc phương án B (Win32 + Direct2D). Phương án B không được chọn vì số đo này, mà vì chi phí viết lại.
