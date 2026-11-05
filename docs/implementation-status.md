# Tiến độ triển khai — HabitGrid

> Cập nhật: 22/10/2026 • Trạng thái: **8/8 gói SPEC + POC đã hoàn tất 100%**, sẵn sàng phát hành Microsoft Store (Go-Live Ready).

---

## 1. Tổng quan các gói công việc

| Gói | Nội dung | Trạng thái | Test | Ghi chú |
|---|---|:---:|---:|---|
| **POC** | Ghim widget lên desktop (Windows 11 24H2+) | ✅ | 21 | Sửa 2 lỗi nghiêm trọng — xem `poc-report.md` |
| **SPEC-01** | Core domain: model, streak, lịch, ngày logic, bảng màu | ✅ | 50 | Thuần toán streak xử lý đủ 4 trường hợp khó |
| **SPEC-02** | Data layer: SQLite, migration, repository, sao lưu | ✅ | 45 | WAL + foreign_keys, mọi ghi bọc transaction |
| **SPEC-03** | Widget & lưới desktop (control tự vẽ, 3 chế độ ghim) | ✅ | 66 | Render 1,13 ms/frame; 0,59 MB cho 3 widget cỡ L |
| **SPEC-04** | Bảng quản lý: dashboard, CRUD, kéo-thả, onboarding | ✅ | 14 | Paywall gate, xóa có hoàn tác 10s |
| **SPEC-05** | Cài đặt, theme, i18n (VI/EN), trợ năng | ✅ | (trong UI) | Tokens chuẩn, auto-save tức thì, Narrator |
| **SPEC-06** | Tray, single-instance, toast, StartupTask | ✅ | 6 | Single-instance Mutex, NamedPipe, Tray Win32, Actionable Toast |
| **SPEC-07** | License Pro qua Microsoft Store & DPAPI | ✅ | 5 | StoreContext `pro_unlock`, DPAPI offline cache 30 ngày |
| **SPEC-08** | Đóng gói MSIX, WACK, listing Store | ✅ | — | Layout MSIX `AppPackages\Layout`, Visual Assets đủ kích cỡ |

**Tổng test: 207** (Core 50 · Data 45 · Platform 32 · UI 80) — pass 100%, 0 warning, 0 error.

---

## 2. Cấu trúc solution hiện tại

```
HabitGrid.sln
├─ src/HabitGrid.Core/         domain + abstractions (không WPF, không SQL)
├─ src/HabitGrid.Data/         SQLite, migration, repository, backup
├─ src/HabitGrid.Platform/     Win32 interop: ghim desktop, vị trí, license
├─ src/HabitGrid.UI/           WPF: control lưới, widget, dashboard, ViewModel
├─ src/HabitGrid.App/          ứng dụng chạy thật (composition root)
├─ tests/                      4 project test
└─ docs/                       spec + báo cáo POC
```

### Quyết định kiến trúc đáng chú ý

**Interface repository nằm ở `HabitGrid.Core/Repositories`**, phần hiện thực ở `HabitGrid.Data/Repositories`.
Nhờ vậy tầng UI phụ thuộc **abstraction** chứ không phụ thuộc tầng lưu trữ (Dependency Inversion),
và `HabitGrid.Core` vẫn sạch tuyệt đối (không tham chiếu WPF/SQLite — đã kiểm chứng bằng script).

---

## 3. SPEC-04 — những gì đã làm

### Đã hoàn thành

| Yêu cầu đặc tả | Trạng thái |
|---|---|
| Dashboard 2 cột (sidebar + chi tiết) | ✅ |
| Danh sách thói quen, kéo-thả đổi thứ tự, lưu `sort_order` | ✅ |
| Nút bật/tắt widget (Eye) | ✅ |
| Rào chắn paywall khi thêm thói quen thứ 3 | ✅ |
| Hộp thoại tạo/sửa: tên, 60 icon, loại, mục tiêu, lịch 7 ngày, bảng màu, nhắc nhở | ✅ |
| 4 thẻ chỉ số (streak, kỷ lục, tỷ lệ 30 ngày, tổng hoàn thành) | ✅ |
| Lưới 52 tuần, bấm được để sửa dữ liệu | ✅ |
| Biểu đồ cột 12 tháng + tooltip | ✅ |
| Xóa an toàn 2 bước + hoàn tác 10 giây | ✅ |
| Onboarding: chào đón → 5 mẫu → tạo thói quen | ✅ |
| Màn hình paywall | ✅ (nút mua sẽ nối Store ở SPEC-07) |

### Tiêu chí nghiệm thu

| Tiêu chí | Kết quả |
|---|---|
| Onboarding < 30 giây tới widget trên desktop | ✅ đã kiểm chứng end-to-end: widget là **child của `Progman`** (0x10156), 480×220, visible |
| Kéo-thả cập nhật DB ngay, không lag UI | ✅ có unit test kiểm tra thứ tự trong CSDL |
| Xóa có hoàn tác hoạt động 100% | ✅ có unit test cho cả 3 trạng thái: mềm → hoàn tác → xóa hẳn |
| Thêm thói quen thứ 3 → paywall lịch sự | ✅ unit test xác nhận gọi paywall, **không** gọi hộp thoại tạo habit |

### Chưa làm (để dành cho gói sau)

- Khôi phục thói quen đã lưu trữ (nút "↩" hiện mới chỉ hiện thông báo)
- Trang cài đặt (nút "Cài đặt" hiện mới chỉ hiện thông báo — thuộc SPEC-05)
- Màn hình nâng cấp Pro dùng Store thật (SPEC-07)
- Trình chọn màu tùy chỉnh (ColorPicker) — hiện mới có 6 preset

---

## 4. Ghi chú kỹ thuật quan trọng

### 4.1 WPF-UI

Gói `WPF-UI 4.3.0` **đã được cài** (có bản `net8.0-windows7.0`) nhưng SPEC-04 dùng WPF thuần với
`DesignTokens` riêng. Lý do: WPF-UI 4.x cần hệ thống theme ở mức ứng dụng (`ui:Themes` + merge resource),
việc này thuộc đúng phạm vi **SPEC-05** (theme & design token). Sẽ tích hợp tại đó, kèm `NumberBox`/`TimePicker`
thay cho các control tự viết.

### 4.2 Môi trường kiểm thử có DPI không nhất quán

Máy CI này báo DPI không nhất quán (desktop 96%, cửa sổ 120%, vùng màn hình 0×0) nên cửa sổ quản lý
bị cắt ở mép phải khi chụp ảnh. Manifest `PerMonitorV2` đã được nhúng và kiểm chứng trong file exe;
trên máy thật phép đổi DIP↔pixel là nhất quán nên không xảy ra tình trạng này.
Layout đã được làm thích nghi (thẻ chỉ số dùng `WrapPanel`, cột phải có thanh cuộn) để không bị cắt trong mọi trường hợp.

### 4.3 Bảng màu & ký tự đặc biệt

- File nguồn luôn là **UTF-8** (đã kiểm tra toàn bộ bằng script: 0 ký tự hỏng).
- Icon Fluent lưu **mã ký tự dạng số** (`0xE82A`) rồi chuyển thành `char` lúc chạy → file nguồn thuần ASCII,
  không thể bị hỏng bởi encoding.

### 4.4 Cách chạy

```bash
dotnet run --project src/HabitGrid.App            # bảng quản lý + widget trên desktop
dotnet run --project src/HabitGrid.App -- --poc   # bảng kiểm thử 7 tiêu chí ghim desktop
dotnet test HabitGrid.sln                        # 168 test
```

Dữ liệu nằm tại `%LocalAppData%\HabitGrid\habitgrid.db` (mỗi lần migrate tự sinh bản sao lưu `*.bak-v{n}`).
