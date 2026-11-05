# Gói Công Việc 03: Widget Lưới & Hiển Thị Desktop (SPEC-03)

> **Mã công việc**: `SPEC-03-WIDGET`  
> **Thư viện mục tiêu**: `HabitGrid.UI` (WPF, WPF-UI, .NET 8)  
> **Mục tiêu**: Thiết kế custom control lưới Heatmap vẽ trực tiếp bằng `DrawingVisual` siêu nhẹ (không dùng 371 control WPF con), xử lý hit-test toán học chính xác, cửa sổ widget trong suốt, hỗ trợ Per-Monitor DPI v2, kéo thả lưu vị trí tương đối và cơ chế ghim Desktop (`DesktopHost`).

---

## 1. THIẾT KẾ CONTROL LƯỚI TỰ VẼ (`HeatmapGridControl`)

### 1.1 Nguyên lý hiệu năng cao
Một lưới thói quen cỡ L (52 tuần $\times$ 7 ngày) có tới 364 đến 371 ô vuông. Nếu dùng `ItemsControl` hay sinh ra 371 `Border` / `Button`, WPF sẽ tốn nhiều bộ nhớ RAM và làm chậm quá trình render.
- **Giải pháp bắt buộc**: Tạo một custom control kế thừa `FrameworkElement`, ghi đè `OnRender(DrawingContext dc)` hoặc quản lý 1 `DrawingVisual`. Toàn bộ các ô vuông, viền hôm nay, nhãn tháng và nhãn thứ được vẽ trực tiếp bằng lệnh đồ họa nguyên thủy (`DrawRoundedRectangle`, `DrawGlyphRun` / `DrawText`).
- **RAM mục tiêu**: < 5MB cho mỗi instance widget.

### 1.2 Bảng thông số kích thước (Widget Sizes)

| Cỡ Widget | Số tuần hiển thị | Kích thước ô | Khoảng cách (Gap) | Kích thước xấp xỉ (100% DPI) | Nhãn thứ (T2, T4, T6) |
|:---:|:---:|:---:|:---:|:---:|:---:|
| **S (Nhỏ)** | 13 tuần (~3 tháng) | 14px | 3px | $\sim 260 \times 200$ px | Ẩn để tiết kiệm diện tích |
| **M (Vừa)** | 26 tuần (~6 tháng) | 14px | 3px | $\sim 480 \times 220$ px | Hiển thị |
| **L (Lớn)** | 52 tuần (~1 năm) | 12px | 3px | $\sim 820 \times 240$ px | Hiển thị |

### 1.3 Hit-Test toán học (O(1) Complexity)
Không gắn event listener vào từng ô. Khi người dùng click hoặc di chuột trên `HeatmapGridControl`, vị trí ô được suy luận bằng công thức toán học từ tọa độ chuột $(X, Y)$:

```csharp
public record GridCellInfo(
    int WeekColumnIndex, 
    int DayRowIndex, 
    DateOnly Date, 
    Rect CellBounds,
    bool IsToday,
    bool IsFuture,
    bool IsScheduled
);

public GridCellInfo? HitTest(Point mousePoint)
{
    // 1. Trừ đi lề Offset của nhãn tháng (Top) và nhãn thứ (Left)
    double relativeX = mousePoint.X - GridOriginX;
    double relativeY = mousePoint.Y - GridOriginY;

    if (relativeX < 0 || relativeY < 0) return null;

    int col = (int)(relativeX / (CellSize + CellGap));
    int row = (int)(relativeY / (CellSize + CellGap));

    if (col < 0 || col >= TotalWeeks || row < 0 || row >= 7) return null;

    // Kiểm tra xem chuột có nằm đúng trong lòng ô (không tính khoảng gap)
    double insideX = relativeX % (CellSize + CellGap);
    double insideY = relativeY % (CellSize + CellGap);
    if (insideX > CellSize || insideY > CellSize) return null;

    DateOnly cellDate = CalculateDateFromColRow(col, row);
    return new GridCellInfo(...);
}
```

---

## 2. TRẠNG THÁI Ô & HIỆU ỨNG TƯƠNG TÁC (MICRO-INTERACTIONS)

1. **Ô tương lai**: Mờ 30% (`Opacity = 0.3`), con trỏ chuột giữ nguyên dạng `Arrow`, không nhận tương tác click.
2. **Hover ô quá khứ / hôm nay**: Tăng độ sáng (Glow nhẹ), hiển thị WPF Tooltip dạng Fluent:
   - Thói quen Boolean: `"Thứ Ba, 21/10/2026 • Đã xong"` hoặc `"Thứ Ba, 21/10/2026 • Chưa hoàn thành"`.
   - Thói quen Count: `"5/8 ly nước (62%) • Nhấp để thêm"`.
3. **Thao tác Click**:
   - **Boolean**: Click trái $\rightarrow$ Toggle giá trị (1 $\leftrightarrow$ 0). Kích hoạt hiệu ứng "Pop" mở rộng ô 110% rồi co về 100% trong 150ms (`CubicEaseOut`).
   - **Count**: Click trái $\rightarrow$ Tăng giá trị $+1$.
   - **Count (Tùy chọn nâng cao)**: Click chuột phải hoặc `Shift + Click` mở một **Mini Popup** nổi sát vị trí ô:
     - Ô nhập số nhanh (`NumberBox`).
     - Nút "Đặt về 0".
     - Nút "Hoàn thành mục tiêu (Max)".
4. **Viền ô Hôm nay**: Viền bo góc $1.5\text{px}$ bằng màu Accent (màu nhấn) của hệ thống hoặc màu chủ đạo của thói quen, giúp người dùng nhận ra ngày hiện tại ngay lập tức.
5. **Chế độ Khóa Pro (Read-only)**: Nếu thói quen vượt hạn mức Free do người dùng hạ cấp, phủ một lớp mờ (`Opacity = 0.5`) trên toàn bộ lưới kèm biểu tượng ổ khóa nhỏ ở góc phải và tooltip thông báo "Mở khóa Pro để tiếp tục ghi nhận thói quen này".

---

## 3. CỬA SỔ WIDGET & CẤU TRÚC GIAO DIỆN (`WidgetWindow.xaml`)

```
┌────────────────────────────────────────────────────────┐
│ [Icon] Đọc sách (30 phút)            [Streak: 12 ngày] [✓] │ <-- Header (Kéo thả, Nút Check hôm nay)
├────────────────────────────────────────────────────────┤
│       Th8       Th9       Th10      Th11       Th12    │ <-- Month Labels
│ T2  ▢ ▢ ▣ ▣ ▢ ▣ ▣ ▢ ▢ ▣ ▣ ▢ ▣ ...                      │
│ T4  ▣ ▢ ▣ ▣ ▣ ▢ ▣ ▣ ▢ ▣ ▣ ▣ ▢ ...                      │ <-- 7 hàng x N cột
│ T6  ▢ ▣ ▣ ▢ ▣ ▣ ▢ ▣ ▣ ▢ ▣ ▣ ▣ ...                      │
└────────────────────────────────────────────────────────┘
```

- **Header Window**:
  - Icon thói quen (Fluent Symbol) + Tên thói quen.
  - Chip thống kê Streak: Nền màu mờ, hiển thị số ngày liên tiếp (ví dụ: `🔥 12`).
  - Nút nhanh `[✓]` (Hôm nay): Đánh dấu hoàn thành hôm nay ngay từ header mà không cần tìm ô trong lưới.
- **Kéo thả vị trí**: Người dùng có thể bấm giữ chuột trái ở vùng Header để kéo di chuyển widget, hoặc giữ phím `Alt` và kéo tại bất kỳ điểm nào trên widget.
- **Chế độ Chỉnh Sửa (Edit Mode)**:
  - Bật/tắt thông qua menu khay hệ thống (Tray Icon).
  - Khi bật: Widget hiện viền nét đứt (Dashed Border) màu Accent, 4 tay nắm kéo góc, và nút `✕` ở góc trên để ẩn widget. Trong chế độ này, mọi thao tác click ô thói quen tạm khóa để tránh bấm nhầm khi đang căn chỉnh giao diện.
  - Khi tắt: Widget hoàn toàn cố định, người dùng thao tác thoải mái mà không lo bị xê dịch.

---

## 4. DỊCH VỤ GHIM DESKTOP & QUẢN LÝ TỌA ĐỘ (`DesktopHostService`)

### 4.1 Quản lý 3 chế độ ghim
Cửa sổ `WidgetWindow` được điều khiển thông qua `DesktopHostService`:
1. **Chế độ 1 (Desktop-Level)**: Can thiệp `WorkerW` của Shell (kết quả từ Giai đoạn 0 POC).
2. **Chế độ 2 (Bottom-Most Fallback)**:
   - Áp dụng Windows Hook lắng nghe sự kiện chuyển đổi cửa sổ (`EVENT_SYSTEM_FOREGROUND`).
   - Gọi `SetWindowPos(hwnd, HWND_BOTTOM, ...)` để đảm bảo widget luôn chìm xuống dưới cùng.
3. **Chế độ 3 (Always-On-Top)**: `Topmost = true`.

### 4.2 Lưu trữ tọa độ tương đối (Multi-Monitor & DPI Safe)
Để tránh lỗi widget bị văng ra ngoài màn hình khi người dùng đổi độ phân giải từ 4K sang 1080p hoặc rút cáp màn hình phụ:
- **Tọa độ lưu trong DB**: Lưu dưới dạng tỷ lệ thực $0.0 \le X, Y \le 1.0$ theo màn hình (`MonitorDevicePath`).
  $$X_{\text{rel}} = \frac{Left - Screen.Left}{Screen.Width - Widget.Width}$$
  $$Y_{\text{rel}} = \frac{Top - Screen.Top}{Screen.Height - Widget.Height}$$
- **Khi khôi phục**: Lấy kích thước làm việc hiện tại của màn hình (`WorkArea`), nhân với tỷ lệ $(X_{\text{rel}}, Y_{\text{rel}})$.
- **Snap to Grid**: Khi kéo thả, tự động bắt dính (snap) vào lưới bước nhảy $8\text{px}$ và cách mép màn hình $16\text{px}$.

---

## 5. TIÊU CHUẨN NGHIỆM THU (ACCEPTANCE CRITERIA)
- [ ] Widget render mượt mà 60 FPS, không có hiện tượng giật cục (stutter) khi hover chuột qua các ô.
- [ ] Tỷ lệ RAM tiêu thụ khi hiển thị đồng thời 3 widget cỡ L không vượt quá 80MB.
- [ ] Sự kiện click đổi màu ô phản hồi tức thì (< 30ms) và phát âm thanh / hiệu ứng nhẹ.
- [ ] Đổi chế độ ghim giữa 3 chế độ ngay trong Runtime mà không cần khởi động lại ứng dụng.
- [ ] Kéo widget từ màn hình DPI 100% sang màn hình DPI 150% tự động co giãn sắc nét mà không bị mờ (DPI-aware Per-Monitor v2).
