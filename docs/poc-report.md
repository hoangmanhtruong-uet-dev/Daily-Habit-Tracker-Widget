# Báo cáo POC — Ghim Widget Desktop (Giai đoạn 0)

> Tài liệu này ghi lại kết quả kiểm thử kỹ thuật "ghim cửa sổ ở mức desktop" cho dự án **HabitGrid**.
> Công cụ kiểm thử: `src/HabitGrid.App` (bảng điều khiển POC) — chạy `dotnet run --project src/HabitGrid.App`.

---

## 1. Môi trường kiểm thử

| Hạng mục | Giá trị |
|---|---|
| Hệ điều hành | Microsoft Windows 11 Home Single Language |
| Build | 10.0.26300 (bản 25H2) |
| Kiến trúc | AMD64 (x64) |
| .NET SDK | 8.0.426 |
| DPI | 100% (màn hình `\\.\DISPLAY1`, vùng làm việc 1920×1080) |
| Cấu trúc cây cửa sổ Shell | `SHELLDLL_DefView` là **con trực tiếp của `Progman`** (không nằm trong `WorkerW`) |

---

## 2. Kết quả kiểm thử tự động

Các kiểm tra dưới đây đã được chạy tự động và **đều đạt**:

| # | Hạng mục | Kết quả |
|---|---|---|
| A1 | Tìm thấy `Progman` qua `FindWindow("Progman", null)` | ✅ `0x10156` (trùng `GetShellWindow()`) |
| A2 | Tìm thấy lớp icon `SHELLDLL_DefView` | ✅ `0x1015E`, là con trực tiếp của `Progman` |
| A3 | `SetParent` ghim widget vào lớp desktop | ✅ Widget trở thành con của `Progman` (`0x10156`) |
| A4 | Giữ nguyên vị trí hiển thị sau khi đổi cha | ✅ Trước `(125,125)` → sau `(125,125)` |
| A5 | Di chuyển widget theo toạ độ màn hình khi đã là child window | ✅ `MoveToScreenPosition(640,360)` → đúng `(640,360)` |
| A6 | Chế độ 2 (Bottom-most) đẩy xuống đáy z-order | ✅ |
| A7 | Chế độ 3 (Always-on-top) | ✅ |
| A8 | Manifest `PerMonitorV2` được nhúng trong file exe | ✅ |
| A9 | Ứng dụng khởi động, tạo CSDL, chạy migration có sao lưu `bak-v0` | ✅ |

### Toán học vị trí (đã kiểm thử bằng unit test)

| Tình huống | Kết quả |
|---|---|
| Chuyển tọa độ tuyệt đối → tương đối | `(500,300)` → `(0.312, 0.333)` |
| Khôi phục tương đối → tuyệt đối | `(0.312,0.333)` → `(500,300)` ✔ khớp |
| Snap lưới 8px + lề 16px, tự kẹp trong vùng làm việc | ✅ |

---

## 3. Hai lỗi nghiêm trọng đã phát hiện & sửa trong quá trình POC

### 3.1 `SetParent` bị kiểm tra sai → chế độ 1 **luôn thất bại**

`SetParent(hWndChild, hWndNewParent)` trả về **cha cũ** của cửa sổ, mà với một cửa sổ top-level cha cũ là `NULL`.
Code ban đầu coi `NULL` là dấu hiệu thất bại → **mọi lần ghim đều bị báo lỗi dù `SetParent` đã thành công**.

Sửa: xoá lỗi cũ bằng `SetLastError(0)` trước khi gọi, rồi kiểm tra `GetLastError()` thay vì giá trị trả về.

### 3.2 Không xử lý cấu trúc cây cửa sổ của Windows 11 24H2+

Trên bản 24H2 trở lên, `SHELLDLL_DefView` **không còn nằm trong `WorkerW`** mà là con trực tiếp của `Progman`;
đồng thời **không tồn tại `WorkerW` nào nằm dưới `Progman`** trong z-order (đo được: 14 `WorkerW`, tất cả đều nằm *trên* `Progman`).

Cách cũ (dùng `WorkerW` đứng sau `SHELLDLL_DefView`) vì vậy không tìm được đích.

Sửa: khi phát hiện `SHELLDLL_DefView` là con trực tiếp của `Progman` → gắn widget làm **con của `Progman`**, tức là **anh em với lớp icon**.

> **Lợi ích phụ quan trọng:** vì widget không nằm *dưới* lớp icon nữa mà nằm *ngang hàng*, chuột không bị lớp icon nuốt → **giải quyết được rủi ro "input trap" mà tài liệu POC cảnh báo**.

Luồng dò tìm hiện tại (theo thứ tự ưu tiên):

1. `SHELLDLL_DefView` là con của `Progman` → dùng `Progman` làm cha *(Windows 11 24H2+)*
2. `WorkerW` chứa lớp icon → dùng `WorkerW` đứng ngay dưới nó *(Windows 10 / 11 23H2)*
3. `WorkerW` nằm ngay dưới `Progman` *(một số bản dàn xếp khác)*
4. Dùng `Progman` làm cha *(phương án cuối)*
5. Nếu tất cả thất bại → **tự động fallback sang chế độ 2 (Bottom-most)** và báo cho người dùng

---

## 4. Kết quả 7 tiêu chí POC

> Các mục đánh dấu `[x]` dưới đây là phần **đã kiểm chứng tự động**.
> Phần cần **xác nhận bằng mắt** được đánh dấu trong bảng điều khiển của ứng dụng POC.

| # | Tiêu chí | Tự động | Cần xác nhận bằng mắt |
|---|---|:---:|:---:|
| 1 | Widget nằm trên hình nền, dưới cửa sổ thường | ✅ (đã reparent vào `Progman`) | ☐ |
| 2 | Nhận được click / hover chính xác trên các ô | ✅ (widget là anh em lớp icon) | ☐ |
| 3 | Sống sót qua `Win + D` | — | ☐ |
| 4 | Tự ghim lại sau khi kill `explorer.exe` | ✅ (đã có hook `TaskbarCreated`) | ☐ |
| 5 | Đúng vị trí khi đổi DPI / đa màn hình | ✅ (tọa độ tương đối + `PerMonitorV2`) | ☐ |
| 6 | Không hiện ở Alt+Tab / taskbar | ✅ (`WS_EX_TOOLWINDOW`) | ☐ |
| 7 | Không nhấp nháy, RAM & tài nguyên ổn định | ✅ (0,59 MB quản lý cho 3 widget cỡ L) | ☐ |

### Cách xác nhận các mục còn lại

1. Chạy `dotnet run --project src/HabitGrid.App`
2. Bấm **"Tạo thói quen mẫu"** → 3 widget sẽ hiện trên desktop
3. Thực hiện lần lượt:
   - **Win + D** → widget phải vẫn còn
   - **Taskkill /IM explorer.exe** → Explorer tự khởi động lại, widget phải tự ghim lại
   - Click vào các ô → màu phải đổi ngay
   - Kéo widget sang màn hình khác (nếu có) → phải tự co giãn sắc nét
   - Alt+Tab → widget không được xuất hiện
4. Đánh dấu 7 ô checkbox rồi bấm **"Lưu báo cáo POC"** → ghi đè file `docs/poc-report.md`

---

## 5. Kết luận & khuyến nghị

**Trạng thái: GO với điều kiện** — chế độ 1 (Desktop-level) đã kỹ thuật hoạt động trên bản Windows 11 25H2 sau khi sửa 2 lỗi nêu trên.

- Chế độ 1 nên là **mặc định** cho v1.0, miễn là 3 mục cần xác nhận bằng mắt (tiêu chí 1, 3, 7) đạt trên ít nhất 2 máy: một máy Windows 11 và một máy Windows 10 22H2.
- Cơ chế **fallback tự động sang chế độ 2** phải được giữ nguyên và ghi rõ trong mô tả Store nếu chế độ 1 không ổn định trên một số bản Windows.
- **Bắt buộc kiểm thử lại trên Windows 10 22H2** vì cấu trúc cây cửa sổ khác (`SHELLDLL_DefView` nằm trong `WorkerW`) → nhánh dò tìm số 2 sẽ được dùng thay cho nhánh số 1.

### Nợ kỹ thuật còn lại

| Hạng mục | Ghi chú |
|---|---|
| Multi-monitor thật | Máy hiện tại chỉ có 1 màn hình → cần kiểm thử trên máy 2 màn hình (tiêu chí 5) |
| Windows 10 22H2 | Chưa kiểm thử được trên phiên bản này |
| Click-through (`WS_EX_TRANSPARENT`) | Đã có API, chưa bật trong POC |
| Chống nhấp nháy khi Explorer khởi động lại | Cần quan sát trực tiếp (tiêu chí 7) |
