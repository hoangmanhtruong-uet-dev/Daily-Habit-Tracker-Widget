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
