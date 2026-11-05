# 00 — Kế hoạch tổng HabitGrid v2

## 0. Tóm tắt một trang

| Mục | Nội dung |
|---|---|
| Một câu | Habit tracker **sống trên desktop**: lưới thói quen đẹp ghim ngay màn hình nền, một chạm là xong, có linh vật lớn lên theo chuỗi ngày, bạn bè cùng giữ streak, đồng bộ sang điện thoại. |
| Người dùng chính | GenZ 17–26 tuổi: sinh viên, người mới đi làm, dân code/thiết kế, người "học cùng trên TikTok/Discord", ngồi máy tính 4–10 giờ/ngày. |
| Khác biệt cốt lõi | (1) Widget ghim desktop không app nào trên Windows làm tốt; (2) thẩm mỹ để khoe (theme, share card, Wrapped); (3) động lực kiểu game nhưng không trẻ con; (4) local-first: không bắt đăng ký, dữ liệu vẫn của người dùng. |
| Nền tảng | Windows 10 22H2+/11, x64 + ARM64, Microsoft Store. Mobile (iOS/Android) ở giai đoạn 4. |
| Công nghệ | .NET 10 LTS, WinUI 3 (Windows App SDK), SQLite, ASP.NET Core + PostgreSQL, Microsoft Store subscriptions. |
| Thu phí | Free (đủ dùng thật) + **HabitGrid Plus**: thuê bao tháng/năm hoặc trọn đời. Người đã mua Pro v1 được nâng lên trọn đời miễn phí. |
| Thời gian | ≈17 tuần tới bản v2.2 đầy đủ (1 dev + AI coding), mobile thêm ≈10 tuần. |
| Rủi ro lớn nhất | Ghim cửa sổ WinUI 3 xuống lớp desktop (phải POC lại); chi phí và bảo mật khi có server; GenZ bỏ app sau tuần đầu. |

> Lưu ý công nghệ: .NET 8 hết hỗ trợ ngày 10/11/2026, nên v2 MUST dùng **.NET 10 LTS**. Core/Data của v1 nâng target là chạy, không cần viết lại.

---

## 1. Đánh giá v1: đang ở đâu

### 1.1 Cái v1 làm được (giữ lại)
- Domain sạch: Habit, Entry, streak có xử lý lịch tuần và mốc chuyển ngày, bảng màu sinh tự động. 207 unit test.
- Data: SQLite WAL, migration có phiên bản, backup JSON/CSV.
- Platform: kỹ thuật ghim desktop (WorkerW/Progman) đã POC, single-instance, tray, toast, StartupTask, license Store + cache DPAPI.

### 1.2 Khoảng trống so với app thói quen hiện nay

| Hạng mục | v1 | Mặt bằng thị trường 2026 | Mức thiếu |
|---|---|---|---|
| Loại thói quen | Có/không, đếm số | + thời lượng (timer), thói quen cần bỏ, "X lần/tuần", theo khung giờ trong ngày, routine nhiều bước | Lớn |
| Ghi nhận | Một giá trị/ngày | + ghi chú, mood, ảnh, bỏ qua có lý do, ngày nghỉ | Lớn |
| Động lực | Streak | + XP/level, huy hiệu, linh vật, streak freeze, ăn mừng, recap cuối năm | Rất lớn |
| Xã hội | Không | + bạn đồng hành, thử thách nhóm, nudge, share card | Rất lớn |
| Thống kê | 4 thẻ + biểu đồ tháng | + xu hướng, theo thứ trong tuần/giờ, tương quan, insight tự viết | Trung bình |
| Đồng bộ | Không | Hầu hết có tài khoản + sync đa thiết bị | Rất lớn |
| Nhắc nhở | Một giờ cố định | + nhắc thông minh theo thói quen thực tế, nhắc lần 2, im lặng khi đang tập trung | Trung bình |
| UI/UX | WPF cơ bản, ít chuyển động, theme sáng/tối | Fluent/Mica, chuyển động có ý nghĩa, nhiều theme thẩm mỹ, âm thanh nhẹ | Rất lớn |
| Hệ sinh thái Windows | Widget ghim, tray | + Widgets Board của Win11, phím tắt toàn cục, command palette, Focus session | Trung bình |
| Chất lượng | Unit test tốt, **không có test dựng UI**, bản build sập khi mở | Crash-free ≥ 99,5%, CI, crash report | Lớn |

Kết luận: đánh giá "khoảng 20%" của bạn là hợp lý nếu tính theo mặt bằng thị trường. Phần nền (domain, data, ghim desktop) dùng lại được, còn UI, backend, động lực và xã hội phải làm mới gần hết.

---

## 2. Đối thủ và bài học (cần xác minh lại số liệu trước khi dùng cho marketing)

| App | Điểm mạnh nên học | Điểm yếu để khai thác |
|---|---|---|
| HabitKit | Lưới heatmap đẹp, đơn giản | Gần như chỉ có mobile; không có widget desktop |
| Finch | Linh vật dễ thương, GenZ rất thích, tone nhẹ nhàng | Thiên về self-care, ít số liệu |
| Habitica | Game hóa sâu, nhóm/party | Giao diện cũ, phức tạp, pixel art không hợp mọi người |
| Streaks | Thiết kế gọn, ít chạm | Chỉ hệ Apple |
| Loop Habit Tracker | Thống kê tốt, miễn phí, riêng tư | Giao diện kỹ thuật, chỉ Android |
| Habitify | Đa nền tảng, có web/desktop | Desktop là app thường, không sống trên màn hình nền |
| TickTick | Gộp todo + habit + pomodoro | Habit chỉ là tính năng phụ |

**Định vị:** "HabitKit + Finch, nhưng sống trên desktop Windows". Thẩm mỹ và cảm giác "cute nhưng không trẻ con", số liệu đủ sâu cho người thích tối ưu, và lợi thế độc quyền là hiện diện thường trực trên màn hình.

---

## 3. Người dùng mục tiêu

| Persona | Bối cảnh | Muốn gì | Tính năng then chốt |
|---|---|---|---|
| **Linh, 19, sinh viên** | Học online, xem TikTok "study with me", dùng Notion | Thấy tiến bộ, khoe được, có bạn cùng giữ streak | Theme thẩm mỹ, share card, buddy, Focus timer |
| **Khoa, 23, dev junior** | Ngồi máy 10 giờ/ngày, thích GitHub contribution graph | Ít chạm, phím tắt, số liệu thật | Widget desktop, Ctrl+K, phím tắt toàn cục, thống kê |
| **Mai, 25, designer** | Đang bỏ thói quen xấu (lướt điện thoại khuya), lo âu nhẹ | Nhẹ nhàng, không bị phạt khi lỡ một ngày | Thói quen cần bỏ, streak freeze, mood, linh vật |
| **Huy, 21, gym bro** | Theo dõi tập, nước, ngủ | Mục tiêu theo tuần, xem xu hướng | "3 lần/tuần", đếm số, biểu đồ |

### Nguyên tắc sản phẩm v2
1. **Một chạm vẫn là luật số 1**: check-in hôm nay ≤ 1 click từ desktop, ≤ 2 phím từ bất kỳ đâu.
2. **Thưởng, không phạt**: lỡ một ngày không xóa công sức; dùng ngôn ngữ tích cực; có freeze và "ngày nghỉ".
3. **Đẹp để khoe**: mọi màn hình quan trọng phải chụp/chia sẻ được mà vẫn đẹp.
4. **Local-first, riêng tư mặc định**: dùng đầy đủ không cần tài khoản; tài khoản chỉ để sync/xã hội; ghi chú và mood không bao giờ vào telemetry.
5. **Nhẹ**: widget nhàn rỗi < 1% CPU, toàn app < 150 MB RAM.

---

## 4. Phạm vi theo bản phát hành

| Nhóm | v2.0 "Glow-up" (offline) | v2.1 "Sync" | v2.2 "Squad" | v3 / sau |
|---|---|---|---|---|
| App shell | WinUI 3 mới hoàn toàn, Mica, onboarding mới | Đăng nhập, quản lý thiết bị | Hồ sơ cá nhân | — |
| Thói quen | Có/không, đếm, thời lượng, cần bỏ, X/tuần, khung giờ, routine | — | Thói quen dùng chung trong nhóm | Thói quen tự đo qua tích hợp |
| Ghi nhận | Ghi chú, mood, bỏ qua, ngày nghỉ | Đồng bộ ghi chú | — | Ảnh |
| Widget | Ghim desktop v2 (4 cỡ, nhiều kiểu), Widgets Board, quick check-in | Widget cập nhật khi máy khác check-in | Widget "squad" | — |
| Động lực | XP, level, 30 huy hiệu, Grid Buddy, freeze, ăn mừng | — | Wrapped tháng/năm | Mùa (season) |
| Thống kê | Trang Insights + insight tự động (không AI) | — | AI coach (Plus) | — |
| Xã hội | Share card ảnh | — | Bạn bè, buddy, thử thách nhóm, nudge | Bảng xếp hạng công khai |
| Tích hợp | Phím tắt toàn cục, Ctrl+K, Focus timer, tray mới, nhắc thông minh | Thông báo đẩy (WNS) | — | Lịch, Spotify/Strava |
| Dữ liệu | Migration v1→v3, backup tự động | Sync, backup cloud, xóa tài khoản | — | Mã hóa đầu-cuối |
| Thu phí | Free/Plus (Store), chuyển Pro v1 → trọn đời | Xác minh entitlement phía server | — | Mobile IAP dùng chung entitlement |
| Mobile | — | API sẵn sàng | — | App iOS/Android (giai đoạn 4) |

**Không làm** (ít nhất tới v3): todo/project management, mạng xã hội công khai có feed vô hạn, quảng cáo, bán dữ liệu.

---

## 5. Kiến trúc tổng thể

```
┌─────────────────────── Windows (MSIX) ───────────────────────┐
│ HabitGrid.App (WinUI 3)        HabitGrid.WidgetHost          │
│  - Shell, trang, ViewModel      - Widget ghim desktop        │
│  - Tray, hotkey, Ctrl+K         HabitGrid.WidgetsBoard       │
│                                  - Provider cho Win11 Board  │
│        │              │                     │                │
│  HabitGrid.Core (domain, streak, XP, insight — thuần .NET)   │
│  HabitGrid.Data (SQLite v3, repository, outbox)              │
│  HabitGrid.Sync (engine đồng bộ, HLC, merge)                 │
│  HabitGrid.Platform (Win32, Store, notification, hotkey)     │
│  HabitGrid.Contracts (DTO + phiên bản API, dùng chung)       │
└──────────────────────────┬───────────────────────────────────┘
                           │ HTTPS (REST + SignalR)
┌──────────────────────────▼───────────────────────────────────┐
│ HabitGrid.Server (ASP.NET Core, container)                   │
│  Auth • Sync • Social • Entitlement • AI proxy • Push (WNS)  │
│  PostgreSQL • Object storage (avatar, share card) • Queue    │
└──────────────────────────────────────────────────────────────┘
```

Quyết định quan trọng:
- **Local-first:** mọi thao tác ghi vào SQLite trước, UI phản hồi ngay; sync chạy nền. App MUST dùng đầy đủ khi offline vĩnh viễn.
- **Một nguồn sự thật cho logic:** streak, XP, insight tính ở Core, chạy được cả trên server (cho social) và client.
- **WinUI 3 cho app chính.** Widget ghim desktop chạy trong tiến trình/cửa sổ riêng; công nghệ render của widget do POC-V2 quyết định (xem SPEC-V2-05 §1).

---

## 6. Roadmap

| Giai đoạn | Tuần | Nội dung | Kết quả |
|---|---|---|---|
| **0. Nền móng** | 1–2 | POC-V2 ghim desktop với WinUI 3; nâng .NET 10; design system + Figma; dựng solution mới; CI | Quyết định công nghệ widget, design token chạy được |
| **1. Glow-up** | 3–8 | SPEC-V2-01, 02 (phần local), 04, 05, 06, 09, 10 (Store), 11 | **v2.0** lên Store (offline, đủ tính năng cốt lõi) |
| **2. Sync** | 9–13 | SPEC-V2-03, 02 (phần sync), 10 (server), WNS | **v2.1**: tài khoản + sync + backup cloud |
| **3. Squad** | 14–17 | SPEC-V2-07, 08, Wrapped | **v2.2**: bạn bè, thử thách, AI coach |
| **4. Mobile** | 18–27 | SPEC-V2-13 | App iOS/Android beta |

Mỗi giai đoạn kết thúc bằng: beta kín 1 tuần (20–50 người, ưu tiên GenZ) → sửa lỗi → phát hành.

---

## 7. KPI và mục tiêu

| Chỉ số | Mục tiêu 3 tháng sau v2.0 |
|---|---|
| Kích hoạt (tạo ≥ 1 thói quen + check-in lần đầu trong phiên đầu) | ≥ 70% |
| Tỷ lệ ghim widget lên desktop | ≥ 60% người dùng kích hoạt |
| Giữ chân D1 / D7 / D30 | ≥ 45% / 25% / 12% |
| Check-in trung bình / người dùng hoạt động / ngày | ≥ 2,0 |
| Chuyển đổi Free → Plus (sau 30 ngày) | 3–5% |
| Crash-free sessions | ≥ 99,5% |
| Điểm Store | ≥ 4,5 |
| Từ v2.2: % người dùng có ≥ 1 bạn | ≥ 20% |

Telemetry để đo các chỉ số này là **opt-in**, chỉ sự kiện ẩn danh (xem SPEC-V2-11).

---

## 8. Rủi ro và phương án

| Rủi ro | Khả năng | Ảnh hưởng | Giảm thiểu |
|---|---|---|---|
| Cửa sổ WinUI 3 không ghim ổn xuống WorkerW | Trung bình | Cao | POC-V2 tuần 1; phương án dự phòng: widget dùng lại WPF hoặc Win2D trong tiến trình riêng |
| Windows update đổi cấu trúc desktop | Trung bình | Cao | Giữ 3 chế độ ghim của v1 + tự phát hiện + fallback "luôn dưới cùng" |
| Chi phí server khi chưa có doanh thu | Trung bình | Trung bình | Sync gửi delta nhỏ; giới hạn free hợp lý; hạ tầng serverless/container co giãn về 0 |
| Lộ dữ liệu cá nhân (ghi chú, mood) | Thấp | Rất cao | Mã hóa khi lưu, phân quyền chặt, pen-test nhẹ trước v2.1, tùy chọn không sync ghi chú |
| Xung đột sync làm mất check-in | Trung bình | Cao | Merge theo trường + HLC; check-in dạng "lấy giá trị lớn hơn" khi xung đột cùng ngày; test fuzz |
| GenZ bỏ app sau tuần 1 | Cao | Cao | Linh vật + buddy + freeze + nhắc thông minh; đo D7 từ beta |
| Store từ chối do dùng API Win32 | Thấp | Trung bình | `runFullTrust` đã được chấp nhận ở v1; WACK trong CI |
| Một người làm, phạm vi quá lớn | Cao | Cao | Cắt theo bản phát hành; v2.0 không phụ thuộc server |

---

## 9. Quyết định còn mở (cần chốt trước giai đoạn tương ứng)

1. **Tên/thương hiệu:** ~~giữ "HabitGrid" hay đổi tên?~~ **Đã chốt (06/11/2026): giữ "HabitGrid"** để người dùng và người mua Pro v1 nhận ra, và nói đúng điểm khác biệt (lưới thói quen trên desktop). Cần giữ chỗ tên trên Partner Center.
2. **Hosting server:** Azure Container Apps + Azure PostgreSQL (khuyến nghị, hợp hệ sinh thái MS) hay Fly.io/Neon (rẻ hơn khi nhỏ)? (trước tuần 9)
3. **Giá Plus** cho thị trường VN và quốc tế (đề xuất trong SPEC-V2-10). (trước tuần 7)
4. **Linh vật:** tự vẽ hay thuê họa sĩ? Ngân sách? (trước tuần 4)
5. **AI coach:** có làm ở v2.2 không, giới hạn chi phí/người dùng. (trước tuần 14)
