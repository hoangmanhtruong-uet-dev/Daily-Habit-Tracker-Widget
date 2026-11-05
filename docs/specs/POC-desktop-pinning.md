# Giai Đoạn 0: POC Ghim Widget Lên Desktop (Desktop Pinning Proof of Concept)

> **Mục tiêu**: Xây dựng một ứng dụng thử nghiệm (Spike/POC) bằng WPF trên .NET 8 để thẩm định tính khả thi của kỹ thuật "ghim cửa sổ mức desktop" trên Windows 10 (22H2) và Windows 11 (23H2/24H2) trước khi phát triển toàn bộ hệ thống.

---

## 1. MỤC TIÊU KỸ THUẬT & RỦI RO LỚN NHẤT

Widget theo dõi thói quen cần thỏa mãn trải nghiệm cốt lõi:
- Nằm thường trực trên hình nền máy tính, phía dưới mọi cửa sổ ứng dụng làm việc.
- **Không bị biến mất hay thu nhỏ khi người dùng nhấn `Win + D` (Show Desktop)** hoặc `Win + M`.
- Nhận được sự kiện chuột (Click vào ô vuông để đánh dấu hôm nay, hover hiện tooltip).
- Không xuất hiện trong thanh Taskbar và danh sách `Alt + Tab`.
- Tự phục hồi vị trí khi khởi động lại `explorer.exe` hoặc khi thay đổi độ phân giải màn hình / tỷ lệ DPI.

---

## 2. BA CHẾ ĐỘ GHIM CỬA SỔ (PINNING STRATEGIES)

### 2.1 Chế độ 1: Desktop-Level (Kỹ thuật WorkerW / Progman)
Đây là kỹ thuật can thiệp sâu vào cây phân cấp cửa sổ của Windows Explorer:
1. **Dò tìm Progman**: Lấy handle của `Progman` (`FindWindow("Progman", null)`).
2. **Kích hoạt tạo WorkerW**: Gửi thông điệp `0x052C` (hoặc thông điệp không công khai tương đương) tới `Progman` bằng `SendMessageTimeout` để buộc Explorer sinh một cửa sổ trung gian mang lớp `WorkerW` nằm giữa biểu tượng desktop (`SHELLDLL_DefView`) và hình nền wallpaper.
3. **Dò tìm WorkerW đích**: Duyệt các cửa sổ con bằng `EnumWindows` để tìm `WorkerW` nằm ngay sau `SHELLDLL_DefView`.
4. **SetParent**: Gọi Win32 API `SetParent(widgetHwnd, targetWorkerW)`.
5. **Cấu hình Window Styles**:
   - `GWL_STYLE`: Bỏ `WS_POPUP`, thêm `WS_CHILD`.
   - `GWL_EXSTYLE`: Thêm `WS_EX_TOOLWINDOW`, loại bỏ hiển thị taskbar.

> [!WARNING]
> **Rủi ro tiếp nhận chuột (Input Trap)**: Nếu widget được đặt làm con của `WorkerW` nằm dưới lớp `SHELLDLL_DefView`, các cú click chuột có thể bị lớp icon Desktop nuốt chửng. POC cần kiểm tra xem liệu widget có nhận được sự kiện `MouseDown` trực tiếp không, hoặc liệu có cần đặt widget làm anh em (sibling) của `SHELLDLL_DefView` hay dùng Low-Level Mouse Hook chuyển tiếp sự kiện.

### 2.2 Chế độ 2: Bottom-Most (Cơ chế Dự phòng Ổn định)
Nếu Chế độ 1 gặp trục trặc trên các bản build Windows mới (đặc biệt là Windows 11 24H2):
1. Cửa sổ WPF độc lập không viền (`WindowStyle="None"`, `AllowsTransparency="True"`, `ShowInTaskbar="False"`).
2. Đặt `WS_EX_TOOLWINDOW` và `WS_EX_NOACTIVATE`.
3. Gọi `SetWindowPos(hwnd, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE)`.
4. Lắng nghe thông điệp hệ thống hoặc hook `EVENT_SYSTEM_FOREGROUND` để tự động đẩy cửa sổ về `HWND_BOTTOM` mỗi khi có cửa sổ khác kích hoạt.
5. **Nhược điểm đã biết**: Khi nhấn `Win + D`, cửa sổ có thể bị thu nhỏ hoặc che khuất tạm thời; cần xử lý sự kiện để phục hồi trạng thái hiển thị.

### 2.3 Chế độ 3: Always-On-Top (Nổi trên cùng)
- Đặt thuộc tính `Topmost = true`.
- Tùy chọn Click-Through khi bật chế độ xem tĩnh (`WS_EX_TRANSPARENT`).

---

## 3. TIÊU CHÍ NGHIỆM THU POC (GO / NO-GO CRITERIA)

Dự án chỉ được phép dùng **Chế độ 1 làm mặc định** nếu vượt qua đủ 7 tiêu chí trong bảng sau trên máy thật Windows 11:

| STT | Tiêu chí đánh giá | Điều kiện đạt (Pass) | Mức độ |
|:---:|---|---|:---:|
| 1 | **Vị trí hiển thị** | Widget nằm trên ảnh nền, dưới mọi cửa sổ Word/Chrome/Explorer | Bắt buộc |
| 2 | **Tương tác chuột** | Nhận Click, Double Click, Mouse Enter/Leave chính xác trên các ô | Bắt buộc |
| 3 | **Sống sót qua `Win + D`** | Nhấn `Win + D` nhiều lần, widget vẫn hiển thị nguyên vẹn trên màn hình nền | Bắt buộc |
| 4 | **Phục hồi sau Crash Explorer** | Kill process `explorer.exe` trong Task Manager và mở lại, widget tự động ghim lại | Bắt buộc |
| 5 | **Đa màn hình & DPI** | Đổi scale từ 100% lên 150%, kéo widget qua màn hình thứ 2 không bị vỡ hoặc lệch tọa độ | Bắt buộc |
| 6 | **Ẩn Taskbar / Alt+Tab** | Không xuất hiện icon trên Taskbar, không có trong danh sách Alt+Tab / Win+Tab | Bắt buộc |
| 7 | **Tài nguyên & Hiệu năng** | Không gây nhấp nháy màn hình, CPU idle < 0.1%, RAM < 35MB cho app POC | Bắt buộc |

> [!IMPORTANT]
> **Quy tắc quyết định**: Nếu Tiêu chí 1, 2 hoặc 3 không đạt trên Windows 11 23H2/24H2, **hủy bỏ ghim Chế độ 1**, chuyển sang phát hành MVP với **Chế độ 2 (Bottom-Most) làm mặc định**, kèm ghi chú rõ ràng trong phần Giới thiệu app trên Store.

---

## 4. BẢNG MÃ NGUỒN MẪU KIỂM THỬ WIN32 INTEROP (C#)

Dưới đây là khung mã nguồn kiểm thử nhanh cần được đưa vào dự án spike:

```csharp
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace HabitGrid.Spike
{
    public static class DesktopPinningService
    {
        private const int WM_SPAWN_WORKER = 0x052C;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string className, string windowTitle);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessageTimeout(
            IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam, 
            uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        public static bool PinToDesktopLevel(Window window)
        {
            var helper = new WindowInteropHelper(window);
            IntPtr hWnd = helper.Handle;

            IntPtr progman = FindWindow("Progman", null);
            if (progman == IntPtr.Zero) return false;

            IntPtr result = IntPtr.Zero;
            SendMessageTimeout(progman, (uint)WM_SPAWN_WORKER, new IntPtr(0xD), new IntPtr(0x1), 0, 1000, out result);

            IntPtr workerW = IntPtr.Zero;
            EnumWindows((topHandle, param) =>
            {
                IntPtr shellView = FindWindowEx(topHandle, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (shellView != IntPtr.Zero)
                {
                    // Lấy WorkerW đứng liền kề sau SHELLDLL_DefView
                    workerW = FindWindowEx(IntPtr.Zero, topHandle, "WorkerW", null);
                }
                return true;
            }, IntPtr.Zero);

            if (workerW != IntPtr.Zero)
            {
                SetParent(hWnd, workerW);
                return true;
            }

            return false;
        }
    }
}
```

---

## 5. KẾT QUẢ CẦN ĐẠT ĐƯỢC CỦA GIAI ĐOẠN 0

1. Tạo một project console / WPF độc lập `HabitGrid.Spike.DesktopPinning`.
2. Chạy thử nghiệm trên tối thiểu 2 máy tính:
   - 1 máy Windows 10 (bản 22H2).
   - 1 máy Windows 11 (bản 23H2 hoặc 24H2).
3. Ghi chép kết quả 7 tiêu chí vào báo cáo ngắn gọn.
4. Xác nhận kiến trúc DesktopHost trong `SPEC-03` sẽ kích hoạt Chế độ 1 hay chuyển hướng sang Chế độ 2.
