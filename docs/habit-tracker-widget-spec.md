# ĐẶC TẢ A-Z: DAILY HABIT TRACKER WIDGET (tên tạm: **HabitGrid**)

> Widget theo dõi thói quen dạng lưới kiểu GitHub, ghim thẳng lên desktop Windows 11, phát hành trên Microsoft Store.
> Phiên bản tài liệu: 1.0 • Ngày: 21/10/2026 • Ngôn ngữ: Tiếng Việt
> Tài liệu này được viết để giao cho AI coding (Cline / Claude Code / Antigravity) theo từng gói công việc SPEC-01…SPEC-08 ở Phần 13.

---

## MỤC LỤC
0. Tóm tắt điều hành
1. Tầm nhìn, đối tượng, vấn đề
2. Thị trường & định vị
3. Phạm vi sản phẩm (MVP / v1.1 / v2 / không làm)
4. Khái niệm & quy tắc nghiệp vụ
5. Đặc tả chức năng
6. UX/UI
7. Kiến trúc kỹ thuật
8. Dữ liệu & sao lưu
9. Mô hình thu phí & license
10. Microsoft Store
11. Yêu cầu phi chức năng
12. Kế hoạch kiểm thử
13. Roadmap & gói công việc cho AI coding
14. Rủi ro & phương án giảm thiểu
15. Quyết định còn mở
16. Phụ lục

---

## 0. TÓM TẮT ĐIỀU HÀNH

| Mục | Nội dung |
|---|---|
| Sản phẩm | Widget lưới thói quen (mỗi ô = 1 ngày, đậm nhạt theo mức hoàn thành), nằm trên desktop, bấm vào ô để đánh dấu |
| Khác biệt | Nằm ngay trên màn hình nền, tối giản, đẹp kiểu Fluent, 100% offline, thao tác 1 chạm |
| Đối tượng | Sinh viên, dân văn phòng/lập trình viên, người tự học muốn theo dõi thói quen mà không mở app |
| Thu phí | Free: 2 thói quen. Pro (mua đứt 3,99–4,99 USD): không giới hạn thói quen, tùy biến màu heatmap, xuất/nhập sao lưu |
| Nền tảng | Windows 10 (22H2) / Windows 11, x64 + ARM64, phân phối qua Microsoft Store (MSIX) |
| Công nghệ đề xuất | .NET 8 + WPF + WPF-UI (Fluent), SQLite, Windows.Services.Store cho license |
| Rủi ro lớn nhất | Kỹ thuật "ghim ở mức desktop" (nằm trên hình nền, dưới mọi cửa sổ) có thể thay đổi theo bản cập nhật Windows → cần **POC ở Giai đoạn 0** và cơ chế dự phòng |
| Thời gian ước tính | ~6–8 tuần làm một mình (MVP phát hành Store ở tuần ~6) |

---

## 1. TẦM NHÌN, ĐỐI TƯỢNG, VẤN ĐỀ

### 1.1 Tầm nhìn
"Nhìn lướt desktop là biết hôm nay mình đã làm gì." Một ô vuông nhỏ mỗi ngày, mỗi thói quen một lưới, tạo động lực giữ chuỗi liên tục (streak) mà không cần mở app, đăng nhập hay tạo tài khoản.

### 1.2 Vấn đề người dùng
- App thói quen trên điện thoại nhiều, nhưng khi ngồi máy tính (học, làm việc) thì phải rút điện thoại ra → quên đánh dấu.
- App checklist trên Windows thường nặng, giao diện cũ, cần tài khoản/đồng bộ cloud.
- Widget bảng tin của Windows 11 nằm trong khay trượt, không "hiện diện" thường trực.

### 1.3 Persona
| Persona | Mô tả | Nhu cầu |
|---|---|---|
| Minh (20, sinh viên IT) | Học code, tập gym, đọc sách | Thấy streak ngay khi mở máy, đánh dấu nhanh sau mỗi buổi học |
| Lan (27, dân văn phòng) | Uống nước, thiền, đọc tiếng Anh | Nhắc nhở nhẹ, giao diện đẹp để chụp màn hình chia sẻ |
| Huy (32, freelancer) | Theo dõi 8–10 thói quen | Không giới hạn số lượng, tùy màu sắc, sao lưu dữ liệu |

### 1.4 Nguyên tắc sản phẩm
1. **Một chạm**: đánh dấu hôm nay không quá 1 click.
2. **Im lặng**: không popup quảng cáo, không thông báo trừ khi người dùng bật.
3. **Riêng tư**: dữ liệu nằm trên máy, không tài khoản, không telemetry mặc định.
4. **Nhẹ**: tốn RAM/CPU gần như bằng 0 khi không tương tác.
5. **Đẹp**: nhìn như một phần của Windows 11.

---

## 2. THỊ TRƯỜNG & ĐỊNH VỊ

### 2.1 Nhận định ban đầu (từ ý tưởng)
- Trên điện thoại, app lưới thói quen (ví dụ HabitKit) có doanh thu tốt; trên Windows còn thiếu widget ghim desktop có thiết kế tối giản.
- Đánh giá tiềm năng thu nhập: ⭐⭐⭐ (trung bình khá). Tiềm năng phụ thuộc chủ yếu vào khả năng được tìm thấy trên Store (ASO) chứ không phải vào tính năng.

### 2.2 Việc cần khảo sát TRƯỚC khi code (1–2 giờ)
Tìm trên Microsoft Store và web với các từ khóa: `habit tracker`, `habit grid`, `streak`, `heatmap widget`, `desktop widget`, `thói quen`, `daily tracker`. Ghi lại: số app cạnh tranh, giá, đánh giá, số lượt xếp hạng, điểm yếu hay bị phàn nàn. Kết quả điền vào Phụ lục 16.4. Mục đích: xác nhận khoảng trống thị trường trước khi đầu tư.

### 2.3 Định vị
> "Lưới thói quen đẹp nhất cho desktop Windows. Không tài khoản, không đồng bộ cloud, không quảng cáo."

### 2.4 Về widget gốc của Windows 11
Windows 11 có Widgets Board (khay trượt) cho phép widget bên thứ ba từ Windows App SDK. Các tin tức từ 2023 nói Microsoft dự định cho ghim widget lên desktop nhưng **mình chưa xác nhận được tính năng này đã phát hành chính thức hay chưa**. Vì vậy sản phẩm **không phụ thuộc** vào tính năng đó: v1.0 dùng cửa sổ widget riêng của app; hỗ trợ Widgets Board để dành cho v2 (xem Phần 3.3) sau khi kiểm tra lại tình trạng nền tảng.

---

## 3. PHẠM VI SẢN PHẨM

### 3.1 MVP (v1.0 – phát hành Store)
| ID | Tính năng | Free | Pro |
|---|---|---|---|
| F-01 | Tạo/sửa/xóa/lưu trữ thói quen | ≤ 2 | không giới hạn |
| F-02 | Widget lưới trên desktop (1 widget/thói quen) | ✔ | ✔ |
| F-03 | Click ô để đánh dấu, sửa ngày quá khứ | ✔ | ✔ |
| F-04 | Thói quen dạng Có/Không và dạng Đếm (có mục tiêu/ngày) | ✔ | ✔ |
| F-05 | Lịch lặp theo thứ trong tuần | ✔ | ✔ |
| F-06 | Streak hiện tại, streak dài nhất, tỷ lệ hoàn thành | ✔ | ✔ |
| F-07 | Cỡ widget S/M/L, kéo thả vị trí, nhớ vị trí | ✔ | ✔ |
| F-08 | Chế độ ghim: Desktop-level / Dưới cùng / Luôn trên cùng | ✔ | ✔ |
| F-09 | Giao diện Sáng/Tối/Theo hệ thống, màu nhấn mặc định | ✔ | ✔ |
| F-10 | Bảng màu heatmap tùy biến (preset + màu tự chọn) | ✘ (1 bảng mặc định) | ✔ |
| F-11 | Xuất/nhập sao lưu (JSON, CSV) | ✘ | ✔ |
| F-12 | Nhắc nhở bằng toast hằng ngày | ✔ | ✔ |
| F-13 | Icon khay hệ thống, mở nhanh bảng quản lý | ✔ | ✔ |
| F-14 | Tự chạy cùng Windows | ✔ | ✔ |
| F-15 | Đa ngôn ngữ Việt/Anh | ✔ | ✔ |
| F-16 | Mua Pro trong app (Store), khôi phục giao dịch | ✔ | ✔ |

### 3.2 v1.1 (sau 4–6 tuần từ khi ra mắt, dựa trên phản hồi)
- Bảng gộp nhiều thói quen trong một widget (mỗi thói quen 1 hàng nhỏ).
- Ngày nghỉ/"skip day" không làm gãy streak.
- Ghi chú theo ngày.
- Tự sao lưu định kỳ ra thư mục (Pro).
- Phím tắt toàn cục mở popup đánh dấu nhanh.
- Thêm preset màu (Pro) và bảng màu thân thiện người mù màu.

### 3.3 v2 (nếu có đà)
- Nhà cung cấp widget cho Windows 11 Widgets Board (nếu kiểm tra thấy ổn định và có lợi).
- Chia sẻ ảnh lưới (xuất PNG đẹp để đăng mạng xã hội).
- Đồng bộ qua thư mục OneDrive/Dropbox (không tự dựng server).
- Thống kê nâng cao (theo tuần/tháng, khung giờ).

### 3.4 KHÔNG làm (giữ app nhẹ & riêng tư)
Không tài khoản, không cloud riêng, không quảng cáo, không mạng xã hội, không AI, không telemetry mặc định.

---

## 4. KHÁI NIỆM & QUY TẮC NGHIỆP VỤ

### 4.1 Thuật ngữ
| Thuật ngữ | Ý nghĩa |
|---|---|
| Habit | Một thói quen được theo dõi |
| Entry | Giá trị ghi nhận của 1 habit trong 1 ngày |
| Widget | Cửa sổ hiển thị lưới của 1 habit trên desktop |
| Ngày logic | Ngày tính theo giờ địa phương, có thể dời mốc sang ngày mới (mặc định 00:00; cho phép 00:00–05:00) |
| Scheduled day | Ngày thuộc lịch lặp của habit |

### 4.2 Loại thói quen
- **Boolean (Có/Không)**: ô có 2 trạng thái: xong / chưa.
- **Count (Đếm)**: có `target` mỗi ngày (ví dụ 8 ly nước). Ô có 5 mức đậm nhạt theo tỷ lệ `value/target`: 0 • (0,25%) • [25,50%) • [50,100%) • ≥100%.

### 4.3 Quy tắc tính streak
1. Chỉ xét các **scheduled day**; ngày không thuộc lịch được bỏ qua (không làm gãy chuỗi).
2. **Streak hiện tại** = số scheduled day liên tiếp hoàn thành, tính lùi từ hôm nay.
3. Hôm nay chưa đánh dấu **chưa làm gãy** streak; chỉ gãy khi qua hết ngày logic.
4. Habit Count được tính là hoàn thành khi `value ≥ target`.
5. **Streak dài nhất** = chuỗi liên tiếp dài nhất trong toàn bộ lịch sử.
6. **Tỷ lệ hoàn thành 30 ngày** = số scheduled day hoàn thành / số scheduled day trong 30 ngày gần nhất.
7. Sửa dữ liệu quá khứ → tính lại streak ngay.

### 4.4 Quy tắc ngày
- Không cho đánh dấu ngày tương lai (ô bị mờ, vô hiệu).
- Cho sửa mọi ngày quá khứ có trong lưới.
- Thay đổi múi giờ/đồng hồ: dùng giờ địa phương hiện tại; entry lưu theo **ngày** (không lưu giờ) nên không lệch khi đổi múi giờ.
- Xử lý chuyển ngày: dùng một timer duy nhất đặt tại mốc chuyển ngày kế tiếp; khi máy thức dậy từ chế độ ngủ cũng kiểm tra lại (sự kiện `PowerModeChanged`/`SystemEvents.TimeChanged`).

### 4.5 Quy tắc giới hạn Free
- Free tạo tối đa **2 thói quen**, tính cả thói quen đã lưu trữ để tránh lách (xem Quyết định mở Q-03).
- Hạ cấp (hoàn tiền/mất Pro) khi có > 2 habit: **giữ nguyên dữ liệu**, 2 habit đầu (theo thứ tự tạo) vẫn sửa được, các habit còn lại chuyển **chỉ đọc** và widget hiển thị mờ kèm nút "Mở khóa Pro". Tuyệt đối không xóa dữ liệu người dùng.

---

## 5. ĐẶC TẢ CHỨC NĂNG

### F-01 Quản lý thói quen
- Tạo: tên (bắt buộc, ≤ 40 ký tự), icon (bộ icon Fluent ~60 cái), loại (Boolean/Count), mục tiêu/ngày (Count), lịch lặp (7 checkbox thứ, mặc định cả tuần), màu (Pro: tùy biến), ngày bắt đầu (mặc định hôm nay), nhắc nhở (giờ, tùy chọn).
- Sửa: mọi trường, đổi tên/icon/màu không ảnh hưởng dữ liệu cũ. Đổi loại Boolean↔Count: cảnh báo vì giá trị cũ được chuyển (Boolean: 1; Count: giữ).
- Lưu trữ (archive): ẩn widget, giữ dữ liệu, có thể khôi phục.
- Xóa: xác nhận 2 bước (gõ lại tên hoặc giữ nút), có hoàn tác trong 10 giây (soft delete tới khi đóng toast).
- Sắp xếp thứ tự bằng kéo thả trong bảng quản lý.

### F-02 Widget lưới
- Lưới 7 hàng (T2→CN hoặc CN→T7 tùy cài đặt "tuần bắt đầu từ") × N cột (tuần), cột mới nhất ở bên phải, ô hôm nay có viền nhấn.
- Nhãn tháng phía trên, nhãn thứ T2/T4/T6 bên trái (ẩn ở cỡ S).
- Tiêu đề: icon + tên + chip streak (dùng icon Fluent, không dùng emoji) + nút "Hôm nay" nhanh.
- Hover ô: tooltip "Thứ Ba, 21/10/2026 • Đã xong" hoặc "5/8 ly".
- Cỡ widget (tham khảo ở 100% DPI):
  | Cỡ | Số tuần | Ô | Kích thước ~ |
  |---|---|---|---|
  | S | 13 | 14px, gap 3px | 260×200 |
  | M | 26 | 14px, gap 3px | 480×220 |
  | L | 52 | 12px, gap 3px | 820×240 |
- Co giãn theo DPI từng màn hình (per-monitor DPI v2).

### F-03 Đánh dấu
- Boolean: click ô → bật/tắt. Có animation "pop" 150ms.
- Count: click = +1; Shift+click hoặc chuột phải mở mini popup: nhập số, "Đặt về 0", "Hoàn thành mục tiêu".
- Nút "Hôm nay" trên header thao tác y như click ô hôm nay.
- Mọi thay đổi có toast nhỏ "Đã cập nhật • Hoàn tác" trong 5 giây (Ctrl+Z cũng hoạt động khi widget đang focus).
- Ghi DB tức thì (không cần nút Lưu).

### F-04/F-05 Loại thói quen & lịch lặp
Xem 4.2 và 4.3. Ô thuộc ngày không nằm trong lịch hiển thị dạng chấm nhỏ mờ, vẫn cho đánh dấu thêm (tính là hoàn thành vượt lịch, không ảnh hưởng streak xấu).

### F-06 Thống kê
Hiện trong chip header và trang chi tiết thói quen: streak hiện tại, streak dài nhất, tổng số lần hoàn thành, tỷ lệ 30 ngày, mini biểu đồ cột theo tháng (12 tháng).

### F-07 Vị trí & kích thước
- Kéo bằng cách giữ vùng header (hoặc giữ phím Alt + kéo ở bất kỳ đâu).
- Có "Chế độ chỉnh sửa" bật/tắt (từ menu khay): khi bật hiện viền + tay kéo; khi tắt widget không bị kéo nhầm.
- Snap vào lưới 8px và mép màn hình; lưu vị trí theo **màn hình + tọa độ tương đối**; nếu màn hình biến mất thì dời vào màn hình chính.
- Khi độ phân giải/DPI đổi: tính lại vị trí theo tỷ lệ, không bay mất ra ngoài vùng hiển thị.

### F-08 Chế độ ghim (xem chi tiết 7.3)
1. **Desktop-level** (mặc định nếu POC ổn): nằm trên hình nền (và trên/dưới icon desktop tùy kết quả POC), dưới mọi cửa sổ, **không biến mất khi nhấn Win+D**.
2. **Dưới cùng (bottom-most)**: cửa sổ thường bị đẩy xuống đáy z-order; có thể bị ẩn khi Win+D.
3. **Luôn trên cùng**: lơ lửng trên mọi cửa sổ (dùng để tập trung, hoặc khi 2 chế độ trên lỗi).
- Tự động fallback sang chế độ 2 nếu chế độ 1 thất bại khi khởi động, kèm thông báo một lần trong cài đặt.

### F-09 Giao diện
Sáng / Tối / Theo hệ thống. Nền widget: 3 kiểu: Trong suốt mờ (acrylic), Đặc, Không nền (chỉ các ô). Độ mờ chỉnh bằng thanh trượt 0–100%. Bo góc 12px. Tương thích chế độ High Contrast của Windows.

### F-10 Màu heatmap (Pro)
- Mỗi habit chọn 1 bảng 5 mức (empty + 4 mức) từ preset hoặc tự chọn màu gốc (hệ thống tự sinh 4 mức đậm dần, đảm bảo tương phản ≥ 3:1 với nền ở cả Sáng/Tối).
- Preset gợi ý ở Phụ lục 16.1 (Xanh lá GitHub, Xanh dương, Tím, Cam, Hồng, Xám đơn sắc, Cầu vồng theo mức, Thân thiện người mù màu).
- Free chỉ dùng bảng mặc định (xanh lá); nút đổi màu hiện khóa Pro, bấm vào mở màn hình nâng cấp.

### F-11 Sao lưu (Pro)
- **Xuất JSON** (đầy đủ, dùng để khôi phục) và **Xuất CSV** (mỗi habit một file: `date,value`).
- **Nhập JSON**: chọn "Gộp" (giữ dữ liệu cũ, thêm mới, trùng thì lấy giá trị lớn hơn) hoặc "Thay thế" (cảnh báo rõ, tự tạo bản sao lưu tạm trước khi thay).
- Kiểm tra `schemaVersion`; file quá mới → thông báo cần cập nhật app; file hỏng → báo lỗi, không sửa DB.
- Đề xuất hiển thị rõ cho người dùng Free: "Dữ liệu của bạn nằm tại: %LocalAppData%\HabitGrid" (xem Quyết định mở Q-04).

### F-12 Nhắc nhở
- Mỗi habit đặt giờ nhắc (tùy chọn) + các thứ trong tuần.
- Chỉ nhắc nếu hôm nay **chưa hoàn thành**.
- Toast có nút "Đã xong" (đánh dấu ngay không mở app) và "Để sau 1 giờ".
- Tôn trọng Focus Assist / Không làm phiền của Windows.
- Không có nhắc thì app không tạo bất kỳ thông báo nào.

### F-13 Khay hệ thống
Menu: Mở bảng quản lý • Chế độ chỉnh sửa • Hiện/ẩn tất cả widget • Cài đặt • Thoát. Click trái icon khay = mở bảng quản lý. Đóng cửa sổ quản lý chỉ thu vào khay.

### F-14 Khởi động cùng Windows
Dùng `StartupTask` trong manifest MSIX. Công tắc trong Cài đặt; mặc định **tắt** ở lần chạy đầu và hỏi một lần ở hướng dẫn làm quen. Khi khởi động cùng Windows: chạy ẩn vào khay và hiện widget, không bật cửa sổ quản lý.

### F-15 Đa ngôn ngữ
Việt (mặc định nếu Windows là Việt) và Anh. Toàn bộ chuỗi trong file resource (`.resx`), định dạng ngày theo culture, không hardcode.

### F-16 Mua Pro
Xem Phần 9.

### 5.1 Onboarding lần đầu (≤ 30 giây)
1. Màn hình chào + nút "Bắt đầu" (1 trang).
2. Tạo thói quen đầu tiên (gợi ý nhanh: Uống nước, Đọc sách, Tập thể dục, Học tiếng Anh, Thiền).
3. Hiện widget ngay trên desktop kèm bong bóng chỉ dẫn "Bấm vào ô hôm nay để đánh dấu".
4. Hỏi bật nhắc nhở và chạy cùng Windows (cả hai đều tùy chọn, bỏ qua được).

---

## 6. UX/UI

### 6.1 Danh sách màn hình
1. **Widget** trên desktop (mục 6.2).
2. **Bảng quản lý** (cửa sổ chính): danh sách habit, thêm/sửa, kéo thả thứ tự, bật/tắt widget từng habit.
3. **Trang chi tiết habit**: lưới lớn 52 tuần, thống kê, biểu đồ tháng, nút sửa/lưu trữ/xóa.
4. **Hộp thoại tạo/sửa habit**.
5. **Cài đặt** (mục 6.5).
6. **Màn hình nâng cấp Pro** (mục 9.4).
7. **Toast** (nhắc nhở) và **mini popup** (nhập số cho habit Count).

### 6.2 Giải phẫu widget
```
┌──────────────────────────────────────────────┐
│ [icon] Đọc sách            chip: 12 ngày  [✓]│  ← header (kéo được), [✓] = đánh dấu hôm nay
│       Th10   Th11   Th12 ...                 │  ← nhãn tháng
│ T2  ▢ ▢ ▣ ▣ ▢ ▣ ...                          │
│ T4  ▣ ▢ ▣ ▣ ▣ ▢ ...                          │
│ T6  ▢ ▣ ▣ ▢ ▣ ▣ ...                          │
│     ...  7 hàng × N tuần                     │
└──────────────────────────────────────────────┘
```
- Ô vuông bo góc 3px. Ô hôm nay có viền 1.5px màu nhấn. Ô tương lai mờ 30%.
- Trạng thái: bình thường • hover (sáng lên nhẹ) • nhấn (thu nhỏ 92%) • đã xong (pop 150ms) • chỉ đọc (khóa Pro: phủ mờ + biểu tượng khóa).
- Khi chế độ chỉnh sửa bật: viền nét đứt màu nhấn, tay nắm kéo 4 góc, nút ✕ ẩn widget.

### 6.3 Bảng quản lý
- Bố cục 2 vùng: danh sách habit bên trái (thẻ có icon, tên, streak, nút bật/tắt widget), chi tiết/trang cài bên phải.
- Nút nổi bật "+ Thêm thói quen". Khi đạt giới hạn Free và bấm thêm → mở màn hình nâng cấp (không phải hộp thoại hệ thống thô).
- Empty state: minh họa lưới mẫu + nút "Tạo thói quen đầu tiên".

### 6.4 Nguyên tắc thị giác
- Tái dùng **design token** của các app khác trong cùng bộ (màu, spacing 4/8/12/16/24, radius 8–12, Segoe UI Variable, icon Fluent).
- Hiển thị tốt ở Sáng/Tối; tương phản chữ đạt WCAG AA; ô đã xong phân biệt với ô trống không chỉ bằng màu (hover có tooltip chữ; màn người mù màu có kiểu viền/dấu chấm tùy chọn).
- Animation 120–200ms, tôn trọng "Hiệu ứng động" của Windows (tắt thì tắt hết).

### 6.5 Cài đặt (nhóm)
- **Chung**: ngôn ngữ, tuần bắt đầu từ T2/CN, mốc chuyển ngày, khởi động cùng Windows.
- **Giao diện**: Sáng/Tối/Hệ thống, kiểu nền widget, độ mờ, bo góc.
- **Widget**: chế độ ghim, cỡ mặc định, bật/tắt chế độ chỉnh sửa, đặt lại vị trí.
- **Nhắc nhở**: bật/tắt toàn cục, giờ yên tĩnh.
- **Dữ liệu** (Pro): xuất/nhập, mở thư mục dữ liệu.
- **Giấy phép**: trạng thái Free/Pro, nút Nâng cấp / Khôi phục giao dịch.
- **Giới thiệu**: phiên bản, chính sách riêng tư, liên hệ hỗ trợ, nút "Sao chép thông tin phiên bản".
- Thay đổi áp dụng ngay, không cần nút Lưu (dùng cùng kiểu trang Cài đặt đã thiết kế cho Batch File Renamer để đồng bộ trải nghiệm).

### 6.6 Phím tắt (khi bảng quản lý/widget đang focus)
`Ctrl+N` thêm habit • `Space/Enter` đánh dấu ô đang focus • `←↑↓→` di chuyển giữa các ô • `Ctrl+Z` hoàn tác • `Delete` lưu trữ habit đang chọn • `Esc` đóng popup.

### 6.7 Trợ năng
Mỗi ô có tên truy cập: "21 tháng 10, đã xong". Điều hướng bằng bàn phím trong lưới. Hỗ trợ Narrator. Hỗ trợ High Contrast, cỡ chữ hệ thống (Text scaling).

---

## 7. KIẾN TRÚC KỸ THUẬT

### 7.1 Lựa chọn công nghệ
| Phương án | Ưu | Nhược | Khuyến nghị |
|---|---|---|---|
| **.NET 8 + WPF + WPF-UI** | Cửa sổ trong suốt dễ (`AllowsTransparency`), interop Win32 thành thục (SetParent, WS_EX_*), nhẹ, đóng gói MSIX tốt | Cần thư viện để có kiểu Fluent | **Chọn mặc định** |
| WinUI 3 (Windows App SDK) | Fluent gốc | Cửa sổ trong suốt hoàn toàn không đơn giản (theo hiểu biết hiện tại), tốn RAM hơn, cấu hình phức tạp hơn | Phương án thay thế |
| Electron/Tauri | Dễ làm UI | Nặng/khó ghim desktop, không phù hợp widget thường trực | Không dùng |

> Lưu ý: mình không biết stack hiện tại của các app khác của bạn (QAS, WPD, Batch File Renamer). **Nên chọn cùng stack** để tái dùng code đóng gói MSIX, khay hệ thống, license, localization và design token.

- Ngôn ngữ: C# 12, .NET 8, MVVM (CommunityToolkit.Mvvm).
- Lưu trữ: SQLite (`Microsoft.Data.Sqlite`), truy cập qua repository mỏng.
- Vẽ lưới: một control tự vẽ (`DrawingVisual`/`OnRender`) cho ≤ 371 ô, hit-test bằng toán (tính ô từ tọa độ), không tạo 371 control con.
- Thông báo: `Microsoft.Toolkit.Uwp.Notifications` / `AppNotificationManager` cho toast có nút hành động.
- License: `Windows.Services.Store.StoreContext` (khởi tạo với HWND của cửa sổ chính).
- Đóng gói: MSIX (dự án Windows Application Packaging), x64 + ARM64.

### 7.2 Cấu trúc dự án
```
HabitGrid/
├─ src/
│  ├─ HabitGrid.App/            # entry, DI, tray, vòng đời
│  ├─ HabitGrid.Core/           # domain: Habit, Entry, StreakCalculator, Schedule, Palette
│  ├─ HabitGrid.Data/           # SQLite, migrations, repository, BackupService
│  ├─ HabitGrid.UI/             # control lưới, window widget, trang quản lý, theme tokens
│  ├─ HabitGrid.Platform/       # DesktopHost (ghim), ToastService, StartupService, StoreLicense
│  └─ HabitGrid.Package/        # MSIX, manifest, assets
├─ tests/
│  ├─ HabitGrid.Core.Tests/     # streak, lịch, chuyển ngày
│  └─ HabitGrid.Data.Tests/     # migration, backup/import
├─ docs/  (spec, privacy policy, store listing)
└─ build/ (script đóng gói, WACK)
```

### 7.3 Cơ chế "ghim ở mức desktop" (hạng mục rủi ro cao nhất)
Mục tiêu: widget nằm trên hình nền, dưới mọi cửa sổ, không biến mất khi Win+D, nhưng vẫn nhận click.

**Chế độ 1 – Desktop-level (kỹ thuật thường dùng bởi các app tùy biến desktop):**
1. Tìm cửa sổ `Progman`, gửi thông điệp `0x052C` để Explorer tạo `WorkerW` phía sau icon.
2. Tìm `WorkerW` thích hợp (cấu trúc cây cửa sổ **đã thay đổi ở một số bản Windows 11**, ví dụ 24H2 → phải dò theo từng bản).
3. `SetParent(widgetHwnd, workerW)`, đặt style `WS_CHILD`, bỏ `WS_POPUP`, thêm `WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE` để không hiện ở Alt+Tab/taskbar.
4. **Vấn đề đầu vào**: nếu widget nằm *sau* lớp icon desktop (`SHELLDLL_DefView`), chuột bị lớp icon chặn. Cần xác minh trong POC cách nào để widget nhận được click (đặt trên lớp icon nhưng vẫn dưới cửa sổ thường, hoặc chuyển tiếp chuột bằng hook mức thấp trên vùng widget).
5. Xử lý: đổi độ phân giải/DPI/số màn hình (`WM_DISPLAYCHANGE`), Explorer khởi động lại (`TaskbarCreated` → ghim lại), khóa/mở khóa máy, chuyển Virtual Desktop.

**Chế độ 2 – Bottom-most (dự phòng, ổn định hơn):**
- Cửa sổ top-level không viền, `WS_EX_TOOLWINDOW`, `SetWindowPos(HWND_BOTTOM)`; gọi lại khi mất focus/khi z-order đổi (hook `EVENT_SYSTEM_FOREGROUND`).
- Nhược điểm: có thể bị ẩn khi Win+D.

**Chế độ 3 – Always-on-top:** `Topmost = true`, kèm tùy chọn click-through (`WS_EX_TRANSPARENT`) khi bật "Chế độ xem".

**Giai đoạn 0 (POC, ~2–3 ngày) — phải đạt các tiêu chí sau trước khi làm tiếp:**
| # | Tiêu chí | Đạt? |
|---|---|---|
| 1 | Widget hiển thị trên hình nền, dưới cửa sổ thường (Windows 10 22H2, Windows 11 23H2 và bản mới nhất) | |
| 2 | Nhận được click/hover | |
| 3 | Sống sót sau Win+D | |
| 4 | Sống sót sau khởi động lại Explorer | |
| 5 | Đúng vị trí khi đổi DPI/đa màn hình | |
| 6 | Không hiện ở Alt+Tab/taskbar | |
| 7 | Không có hiện tượng nhấp nháy/tụt hiệu năng | |
Nếu tiêu chí 1–3 không đạt ổn định → v1.0 ra mắt với **Chế độ 2 làm mặc định** và ghi rõ trong mô tả Store.

### 7.4 Vòng đời ứng dụng
- Một instance duy nhất (mutex + named pipe để instance thứ hai báo instance đầu mở bảng quản lý).
- Khởi động: đọc DB → dựng widget cho các habit đang bật → đặt timer chuyển ngày → kiểm tra license nền (không chặn UI).
- Thoát: lưu vị trí widget, đóng DB sạch.
- Khôi phục sau crash: DB dùng WAL + giao dịch; ghi log cục bộ xoay vòng (tối đa 5 file × 1 MB) tại `%LocalAppData%\HabitGrid\logs`, không gửi đi đâu.

### 7.5 Hiệu năng (mục tiêu)
| Chỉ số | Mục tiêu |
|---|---|
| RAM khi idle (1–3 widget) | < 100 MB |
| CPU khi idle | ~0% (chỉ 1 timer chuyển ngày) |
| Thời gian khởi động tới khi widget hiện | < 1,5 giây |
| Phản hồi click → đổi màu ô | < 50 ms |
| Kích thước gói cài đặt | < 60 MB |

---

## 8. DỮ LIỆU & SAO LƯU

### 8.1 Lược đồ SQLite (migration có phiên bản)
```sql
CREATE TABLE habits (
  id            TEXT PRIMARY KEY,           -- GUID
  name          TEXT NOT NULL,
  icon          TEXT NOT NULL,
  kind          INTEGER NOT NULL,           -- 0 = boolean, 1 = count
  target        INTEGER NOT NULL DEFAULT 1, -- mục tiêu/ngày (count)
  schedule_mask INTEGER NOT NULL DEFAULT 127, -- bit 0..6 = T2..CN
  palette_id    TEXT NOT NULL DEFAULT 'default',
  custom_color  TEXT NULL,                  -- #RRGGBB (Pro)
  start_date    TEXT NOT NULL,              -- yyyy-MM-dd
  reminder_time TEXT NULL,                  -- HH:mm
  reminder_mask INTEGER NOT NULL DEFAULT 127,
  sort_order    INTEGER NOT NULL,
  archived_at   TEXT NULL,
  deleted_at    TEXT NULL,                  -- soft delete
  created_at    TEXT NOT NULL
);

CREATE TABLE entries (
  habit_id   TEXT NOT NULL REFERENCES habits(id) ON DELETE CASCADE,
  day        TEXT NOT NULL,                 -- yyyy-MM-dd (ngày logic)
  value      INTEGER NOT NULL,
  updated_at TEXT NOT NULL,
  PRIMARY KEY (habit_id, day)
);

CREATE TABLE widget_state (
  habit_id  TEXT PRIMARY KEY REFERENCES habits(id) ON DELETE CASCADE,
  monitor   TEXT NOT NULL,                  -- định danh màn hình
  x REAL NOT NULL, y REAL NOT NULL,         -- tọa độ tương đối 0..1
  size      INTEGER NOT NULL,               -- 0=S,1=M,2=L
  visible   INTEGER NOT NULL DEFAULT 1
);

CREATE TABLE settings (key TEXT PRIMARY KEY, value TEXT NOT NULL);
CREATE TABLE schema_info (version INTEGER NOT NULL);
```
- Index: `entries(habit_id, day)` (đã là khóa chính), `habits(sort_order)`.
- Vị trí DB: `%LocalAppData%\HabitGrid\habitgrid.db` (gói MSIX sẽ ảo hóa vào thư mục gói của app; kiểm tra khi viết chức năng "mở thư mục dữ liệu").

### 8.2 Định dạng sao lưu JSON
```json
{
  "app": "HabitGrid",
  "schemaVersion": 1,
  "exportedAt": "2026-10-21T14:00:00+07:00",
  "habits": [ { "id": "...", "name": "Đọc sách", "kind": 0, "scheduleMask": 127, "...": "..." } ],
  "entries": [ { "habitId": "...", "day": "2026-10-21", "value": 1 } ],
  "settings": { "weekStart": "monday" }
}
```
Quy tắc: số lượng habit nhập vào vượt giới hạn Free (khi không còn Pro) → nhập vẫn thành công nhưng habit vượt quá ở chế độ chỉ đọc.

### 8.3 Toàn vẹn dữ liệu
- Mọi ghi dùng giao dịch; bật `PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON`.
- Trước mỗi lần migration: sao lưu file DB sang `habitgrid.db.bak-<version>`.
- Nhập "Thay thế": luôn tạo bản sao lưu tạm trước khi ghi.

---

## 9. MÔ HÌNH THU PHÍ & LICENSE

### 9.1 Gói
| | Free | Pro (mua đứt) |
|---|---|---|
| Số thói quen | 2 | Không giới hạn |
| Bảng màu heatmap | 1 mặc định | Preset + màu tự chọn |
| Xuất/nhập sao lưu | Không | JSON + CSV |
| Mọi thứ còn lại | ✔ | ✔ |
| Giá | 0 | 3,99–4,99 USD (đề xuất **4,99**, khuyến mãi ra mắt 3,99 trong 2 tuần đầu) |

### 9.2 Kỹ thuật
- Sản phẩm: **Add-on bền vững (Durable)** trong Partner Center, mã `pro_unlock`.
- Kiểm tra quyền: `StoreContext.GetAppLicenseAsync()` → `AddOnLicenses` chứa `pro_unlock` và `IsActive`.
- Mua: `StoreContext.RequestPurchaseAsync("<StoreId>")` (khởi tạo `StoreContext` với HWND bằng `InitializeWithWindow`).
- Khôi phục giao dịch: chính là kiểm tra lại license; nút "Khôi phục giao dịch" trong Cài đặt.
- **Cache offline**: lưu trạng thái Pro + thời điểm xác minh cuối (mã hóa DPAPI); cho phép offline tối đa 30 ngày không kiểm tra lại; mỗi lần có mạng thì làm mới nền.
- Không cần máy chủ riêng, không lưu thông tin thanh toán.
- Tuyệt đối không xóa dữ liệu khi mất Pro (xem 4.5).

### 9.3 Cổng giới hạn (soft paywall)
- Thêm habit thứ 3 / đổi màu / xuất dữ liệu → mở **màn hình nâng cấp** (không dùng hộp thoại hệ thống thô).
- Không bao giờ chặn việc đánh dấu hằng ngày với 2 habit đầu; không nag, không popup ngẫu nhiên.

### 9.4 Màn hình nâng cấp Pro
Tiêu đề, 3 lợi ích có icon (Không giới hạn thói quen • Màu sắc theo ý bạn • Sao lưu dữ liệu), minh họa heatmap màu mới, giá + "Mua một lần, dùng mãi mãi", nút chính "Mở khóa Pro", nút phụ "Khôi phục giao dịch", dòng nhỏ "Thanh toán an toàn qua Microsoft Store". Trạng thái: chưa mua • đang xử lý • thành công (chúc mừng nhẹ, mở khóa ngay) • đã hủy • lỗi mạng.

### 9.5 Ước tính doanh thu (giả định, KHÔNG phải số liệu thực)
Thu nhập ròng/lượt bán ≈ 4,99 × (1 − 15%) ≈ **4,24 USD** (theo mức phí 15% cho app không phải game khi dùng hệ thống thanh toán của Microsoft; chưa tính thuế phát sinh theo vùng).

| Lượt cài/tháng | Tỷ lệ mua | Số lượt mua | Doanh thu ròng/tháng |
|---|---|---|---|
| 500 | 2% | 10 | ~42 USD |
| 500 | 5% | 25 | ~106 USD |
| 2.000 | 2% | 40 | ~170 USD |
| 2.000 | 5% | 100 | ~424 USD |
| 10.000 | 2% | 200 | ~848 USD |
| 10.000 | 5% | 500 | ~2.120 USD |

Kết luận: mức ⭐⭐⭐ là hợp lý; yếu tố quyết định là **lượt cài** (ASO, đánh giá, bài viết/video giới thiệu), không phải thêm tính năng.

---

## 10. MICROSOFT STORE

### 10.1 Đăng ký & phí (đã kiểm tra)
- Tài khoản nhà phát triển cá nhân hiện **miễn phí đăng ký** (từ 2025, gần 200 thị trường), chỉ cần tài khoản Microsoft cá nhân + xác minh danh tính. Tài khoản công ty vẫn có phí một lần.
- Dùng hệ thống thanh toán của Microsoft: phí 15% cho app không phải game. Với app không phải game, được phép dùng hệ thống thanh toán riêng và giữ 100% (không khuyến nghị cho app nhỏ vì mất sự tiện lợi và độ tin cậy).
- Bạn nên kiểm tra lại điều khoản mới nhất trong Partner Center trước khi chốt giá.

### 10.2 Đóng gói & chứng nhận
- MSIX, ký bởi Store (không cần mua chứng chỉ ký).
- Khai báo capability `runFullTrust` (cần cho .NET desktop) → chuẩn bị lời giải thích ngắn trong phần ghi chú chứng nhận: "App hiển thị widget trên desktop, dùng Win32 API để đặt vị trí cửa sổ; không thu thập dữ liệu".
- Chạy **Windows App Certification Kit (WACK)** trước khi nộp; sửa mọi lỗi.
- Ghi chú rõ cho người kiểm duyệt cách thấy widget: "Mở app → tạo thói quen → widget hiện trên desktop; có thể chuyển chế độ ghim ở Cài đặt → Widget".
- Rủi ro chứng nhận: kỹ thuật SetParent vào WorkerW có thể bị hỏi; có sẵn chế độ Bottom-most làm phương án nộp nếu cần.

### 10.3 Hồ sơ listing (tiếng Việt + tiếng Anh)
- **Tên**: kiểm tra tên còn trống trong Partner Center (và tránh trùng thương hiệu "HabitKit"). Phương án: HabitGrid, StreakDeck, DotHabit, GridHabit.
- **Danh mục**: Productivity (chính). 
- **Mô tả ngắn (≤ 100 ký tự)**: "Lưới thói quen kiểu GitHub ngay trên màn hình nền Windows. Đẹp, nhẹ, ngoại tuyến."
- **Mô tả dài**: vấn đề → giải pháp → 5 điểm nổi bật (widget desktop, 1 chạm, streak, nhắc nhở, riêng tư/offline) → bảng Free vs Pro → hỗ trợ.
- **Từ khóa (7 cụm)**: habit tracker, streak, heatmap, desktop widget, daily habits, productivity tracker, thói quen.
- **Ảnh chụp (tối thiểu 4, khuyến nghị 6)**: (1) widget trên desktop đẹp, nền ảnh đẹp • (2) Click đánh dấu • (3) bảng quản lý • (4) chi tiết/thống kê • (5) bảng màu Pro • (6) chế độ Sáng/Tối. Tỷ lệ 16:9, 1920×1080.
- **Icon**: 1:1, nhiều kích thước; mẫu logo là lưới 3×3 ô với một ô nhấn màu.
- **Video demo 20–30 giây** (tăng chuyển đổi).
- **Chính sách riêng tư**: trang web công khai (GitHub Pages cũng được). Nội dung: không thu thập dữ liệu cá nhân, dữ liệu lưu trên máy, kết nối mạng chỉ để Store kiểm tra license, liên hệ.
- **Phân loại độ tuổi**: hoàn thành bảng câu hỏi IARC (kết quả dự kiến: mọi lứa tuổi).
- **Giá**: Free + add-on `pro_unlock`; đặt giá khu vực (VND hợp lý) trong Partner Center.

### 10.4 Sau khi phát hành
- Xin đánh giá đúng thời điểm (sau khi người dùng giữ streak ≥ 7 ngày, một lần duy nhất, bấm "Để sau" được).
- Theo dõi Store Analytics (lượt cài, lượt mua, tỷ lệ gỡ), đánh giá, lỗi crash trong Partner Center.
- Phản hồi mọi đánh giá trong 48 giờ.

---

## 11. YÊU CẦU PHI CHỨC NĂNG

| Nhóm | Yêu cầu |
|---|---|
| Riêng tư | Không tài khoản, không telemetry, không gửi dữ liệu thói quen ra ngoài; kết nối mạng chỉ khi mua/kiểm tra license qua Store |
| Bảo mật | Không đọc/ghi ngoài thư mục dữ liệu app và đường dẫn do người dùng chọn khi xuất/nhập; kiểm tra schema khi nhập; DPAPI cho cache license |
| Tin cậy | Không mất dữ liệu khi crash/mất điện (WAL + giao dịch); migration có sao lưu |
| Tương thích | Windows 10 22H2 trở lên, Windows 11 các bản; x64 + ARM64; DPI 100–300%; đa màn hình |
| Trợ năng | WCAG AA, bàn phím đầy đủ, Narrator, High Contrast |
| Quốc tế hóa | Việt/Anh; định dạng ngày theo culture; không hardcode chuỗi |
| Hiệu năng | Theo 7.5 |
| Bảo trì | Mã có test cho Core/Data; cấu hình CI chạy test + build MSIX |

---

## 12. KẾ HOẠCH KIỂM THỬ

### 12.1 Unit test (Core)
- Streak: lịch cả tuần, lịch T2–T6, ngày hôm nay chưa đánh dấu, sửa quá khứ, ngày đầu tiên, habit Count đạt/không đạt mục tiêu, bỏ qua ngày ngoài lịch.
- Chuyển ngày: mốc 00:00 và mốc dời 03:00, đổi múi giờ, chuyển DST, máy ngủ dậy qua nửa đêm.
- Màu: sinh 4 mức từ màu gốc, tương phản tối thiểu.

### 12.2 Test dữ liệu
- Migration từ mỗi phiên bản cũ lên mới; sao lưu → nhập (Gộp/Thay thế) cho kết quả đúng; file hỏng/quá mới; DB bị khóa.

### 12.3 Test thủ công trên máy thật (bảng kiểm)
| Nhóm | Ca kiểm thử |
|---|---|
| Ghim | 3 chế độ × (Win+D, Alt+Tab, khởi động lại Explorer, khóa/mở khóa, Virtual Desktop) |
| Màn hình | Đổi DPI, 2 màn hình, rút màn hình phụ, đổi độ phân giải |
| Nhập liệu | Click, Shift+click, chuột phải, bàn phím, màn hình cảm ứng |
| Nhắc nhở | Đúng giờ, đã xong thì không nhắc, nút "Đã xong" từ toast, Focus Assist |
| License | Chưa mua / mua / hoàn tiền / offline 30 ngày / khôi phục trên máy khác |
| Giới hạn Free | Tạo habit thứ 3, hạ cấp có >2 habit (chỉ đọc), nhập sao lưu vượt giới hạn |
| Cài đặt/gỡ | Cài mới, nâng cấp từ bản cũ, gỡ cài đặt (dữ liệu xử lý đúng), chạy cùng Windows |
| Hiệu năng | RAM/CPU idle sau 24 giờ, rò rỉ bộ nhớ |

### 12.4 Cổng phát hành
Test Core/Data pass 100% • WACK pass • bảng kiểm thủ công không còn lỗi mức nghiêm trọng • không crash trong 48 giờ chạy liên tục • xem lại text Store/privacy.

---

## 13. ROADMAP & GÓI CÔNG VIỆC CHO AI CODING

| Giai đoạn | Nội dung | Ước tính |
|---|---|---|
| 0 | Khảo sát Store + POC ghim desktop (7.3) | 3–4 ngày |
| 1 | Core + Data + test (SPEC-01, 02) | 1 tuần |
| 2 | Control lưới + widget + đánh dấu (SPEC-03) | 1–1,5 tuần |
| 3 | Bảng quản lý + cài đặt + theme + i18n (SPEC-04, 05) | 1,5 tuần |
| 4 | Nhắc nhở + khay + khởi động cùng Windows (SPEC-06) | 3–4 ngày |
| 5 | Pro/license + sao lưu + màu (SPEC-07) | 1 tuần |
| 6 | Đóng gói MSIX + WACK + listing + nộp Store (SPEC-08) | 1 tuần (kể cả chờ duyệt) |

### Gói công việc (giao lần lượt, xong gói nào test gói đó)
- **SPEC-01 Core domain**: model Habit/Entry, `ScheduleCalculator`, `StreakCalculator`, `DayProvider` (mốc chuyển ngày), `PaletteGenerator`; test đầy đủ theo 12.1. Chưa có UI.
- **SPEC-02 Data layer**: SQLite, migration, repository, soft delete, `BackupService` (JSON/CSV, Gộp/Thay thế); test theo 12.2.
- **SPEC-03 Widget & lưới**: control tự vẽ, hit-test, cỡ S/M/L, tooltip, đánh dấu, animation, 3 chế độ ghim (`DesktopHost`) + lưu/khôi phục vị trí + chế độ chỉnh sửa + fallback.
- **SPEC-04 Bảng quản lý**: danh sách, tạo/sửa habit, kéo thả, chi tiết/thống kê, onboarding.
- **SPEC-05 Cài đặt & theme**: nhóm cài đặt 6.5, Sáng/Tối/Hệ thống, token thiết kế, `.resx` Việt/Anh, trợ năng.
- **SPEC-06 Nền hệ thống**: tray, single-instance, toast nhắc nhở có nút hành động, StartupTask, xử lý power/time change.
- **SPEC-07 Pro**: `StoreLicense`, cache offline, soft paywall, màn hình nâng cấp, nút khôi phục, khóa/mở khóa tính năng đúng 4.5.
- **SPEC-08 Phát hành**: manifest/MSIX x64+ARM64, asset icon, privacy policy, listing, WACK, ghi chú chứng nhận.

**Cách giao cho AI**: mỗi lần dán một SPEC + mục liên quan của tài liệu này; yêu cầu AI (1) đọc code hiện có, (2) đề xuất kế hoạch ngắn, (3) làm, (4) build + chạy test, (5) báo cáo thay đổi. Không gộp nhiều SPEC vào một lần.

---

## 14. RỦI RO & GIẢM THIỂU

| # | Rủi ro | Mức | Giảm thiểu |
|---|---|---|---|
| R-01 | Kỹ thuật ghim desktop hỏng sau bản cập nhật Windows | Cao | POC sớm; 3 chế độ + tự fallback; kiểm thử trên nhiều bản Windows; ghi rõ trong listing |
| R-02 | Widget chặn/nuốt click, hoặc click xuyên xuống icon desktop | Trung bình | Tiêu chí POC #2; chế độ chỉnh sửa; fallback Bottom-most |
| R-03 | Chứng nhận Store hỏi về `runFullTrust`/Win32 | Trung bình | Ghi chú giải thích; WACK trước khi nộp |
| R-04 | Khó được tìm thấy trên Store (ASO yếu) → doanh thu thấp | Cao | Listing tốt, video, từ khóa, bài giới thiệu (Reddit r/Windows11, r/desktops, r/getdisciplined; YouTube ngắn), xin đánh giá đúng lúc |
| R-05 | Cạnh tranh/clone | Trung bình | Đẹp + nhẹ + riêng tư; ra bản v1.1 nhanh theo phản hồi |
| R-06 | Người dùng Free phản ứng vì khóa xuất dữ liệu | Trung bình | Cân nhắc Q-04; minh bạch vị trí dữ liệu |
| R-07 | Mất/lỗi dữ liệu người dùng | Cao | WAL, giao dịch, sao lưu trước migration, test kỹ |
| R-08 | Chuyển ngày sai (múi giờ, ngủ máy) | Trung bình | Test DST/ngủ máy; lưu theo "ngày" chứ không theo giờ |
| R-09 | Trùng tên/thương hiệu | Thấp–TB | Kiểm tra tên trước khi đăng ký; tránh "HabitKit" |
| R-10 | Làm một mình quá tải | Trung bình | Giữ MVP gọn; bảng nhiều thói quen/skip day để v1.1 |

---

## 15. QUYẾT ĐỊNH CÒN MỞ (cần bạn chốt)

| # | Câu hỏi | Gợi ý |
|---|---|---|
| Q-01 | Chọn WPF hay WinUI 3? | WPF + WPF-UI, trừ khi các app khác của bạn đang dùng WinUI 3 (chọn đồng bộ) |
| Q-02 | Giá chính: 3,99 hay 4,99? | Đặt 4,99, khuyến mãi 3,99 hai tuần đầu |
| Q-03 | Habit đã lưu trữ có tính vào giới hạn 2 của Free không? | Tính (tránh lách), nhưng cho phép xóa hẳn để lấy chỗ |
| Q-04 | Free có được xuất dữ liệu không? | Bạn đã chọn khóa Pro. Phương án dung hòa: Free xem được thư mục dữ liệu để tự sao chép, Pro có nút xuất/nhập tiện lợi |
| Q-05 | Có thử Pro (trial) không? | Add-on bền vững của Store không có trial sẵn; dùng chính bản Free (2 habit) làm bản dùng thử |
| Q-06 | Tên app cuối cùng? | Chốt sau khi kiểm tra tên trống trong Partner Center |
| Q-07 | Có làm provider cho Widgets Board ở v2 không? | Quyết định sau khi kiểm tra lại trạng thái "ghim widget lên desktop" của Windows |
| Q-08 | Ngôn ngữ giai đoạn đầu | Việt + Anh |

---

## 16. PHỤ LỤC

### 16.1 Bảng màu preset (5 mức: trống → đậm nhất)
Màu trống tự động theo nền Sáng/Tối; giá trị tham khảo cho 4 mức còn lại ở nền sáng:
- **Xanh lá (mặc định)**: #9BE9A8, #40C463, #30A14E, #216E39
- **Xanh dương**: #A5D8FF, #5AB0F5, #2F80D0, #1B4F8F
- **Tím**: #D6BCFA, #B07CF0, #8B4FD0, #5B2C9B
- **Cam**: #FFD8A8, #FFA94D, #F77F00, #B85C00
- **Hồng**: #FFC9DE, #FF8FB8, #E5508A, #A02A5C
- **Xám đơn sắc**: #D0D0D0, #A0A0A0, #707070, #404040
- **Thân thiện người mù màu**: dùng dải sáng→tối của một màu xanh dương/cam kèm kiểu viền (tuyệt đối không dựa riêng vào đỏ/xanh lá)

### 16.2 Danh sách chuỗi cần dịch (khung)
`app.name`, `habit.add`, `habit.edit`, `habit.archive`, `habit.delete.confirm`, `habit.today`, `streak.current`, `streak.longest`, `rate.30days`, `pro.title`, `pro.benefit1/2/3`, `pro.buy`, `pro.restore`, `settings.*`, `toast.reminder.title/body/done/later`, `error.import.invalid/newer`.

### 16.3 Tiêu chí nghiệm thu v1.0
- Mở app lần đầu → có habit đầu tiên hiển thị trên desktop trong < 60 giây.
- Click ô hôm nay đánh dấu tức thì và còn nguyên sau khi khởi động lại máy.
- Streak đúng ở mọi ca test 12.1.
- Widget vẫn hiển thị đúng sau Win+D (chế độ 1) hoặc app báo rõ chế độ đang dùng.
- Free bị chặn đúng ở habit thứ 3, màu tùy biến, xuất dữ liệu; mua Pro mở khóa ngay không cần khởi động lại.
- Mất Pro không làm mất dữ liệu.
- WACK pass, nộp Store thành công.

### 16.4 Bảng khảo sát đối thủ (điền ở Giai đoạn 0)
| App | Giá | Đánh giá/số lượt | Có widget desktop? | Điểm mạnh | Điểm yếu/phàn nàn |
|---|---|---|---|---|---|
| (điền) | | | | | |

### 16.5 Nguồn đã kiểm tra
- Phí đăng ký miễn phí cho nhà phát triển cá nhân: Windows Developer Blog, 10/09/2025.
- Mức phí 15% cho app không phải game, quyền dùng thanh toán riêng giữ 100%: TechCrunch (Build 2025) và AlternativeTo.
- Widgets Board cho bên thứ ba & tin tức về ghim lên desktop (chưa xác nhận phát hành): Windows Central, Windows Latest, Thurrott, 2022–2023.
