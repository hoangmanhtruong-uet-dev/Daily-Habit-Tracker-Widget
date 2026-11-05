# SPEC-V2-10 — Thu phí: Free / HabitGrid Plus

> Phát hành: Store purchase [v2.0], xác minh server + entitlement đa nền tảng [v2.1] • Phụ thuộc: SPEC-V2-01 §3.5, 03

## 1. Gói
| | Free | Plus |
|---|---|---|
| Thói quen đang chạy | 5 | Không giới hạn |
| Loại thói quen, tần suất, ghi chú, mood | Đủ | Đủ |
| Widget desktop | Không giới hạn số widget, 2 theme | Mọi theme, nền ảnh, tắt watermark share card |
| Theme thẩm mỹ | Classic Glow, Midnight + theme mở bằng level | Toàn bộ, + theme mùa |
| Buddy cosmetics | Mở bằng level/huy hiệu | + bộ Plus mỗi mùa |
| Freeze tối đa giữ | 3 | 5 |
| Insights | 30 ngày | Toàn bộ lịch sử, xuất CSV |
| Sao lưu | Tự động cục bộ | + chọn thư mục (OneDrive…) |
| Sync & backup cloud [v2.1] | 2 thiết bị | Không giới hạn thiết bị |
| Bạn bè [v2.2] | 10, tham gia thử thách | 50, tạo thử thách |
| AI coach [v2.2] | 1 tóm tắt tuần/tháng | Tóm tắt hằng tuần + 30 lượt chat/tháng |
| Âm thanh Focus nền, bộ âm thanh | — | Có |

Lý do nâng giới hạn Free từ 2 (v1) lên 5: GenZ thường thử 3–5 thói quen ngay từ đầu; giới hạn 2 làm họ bỏ app trước khi kịp gắn bó. Doanh thu đến từ thẩm mỹ, xã hội, AI, không phải từ việc chặn tính năng cơ bản.

## 2. Giá (đề xuất, chốt ở quyết định mở #3)
| Gói | Quốc tế (USD) | Việt Nam (VND, theo bậc giá Store) |
|---|---|---|
| Plus tháng | 2,99 | ≈ 39.000 |
| Plus năm (dùng thử 7 ngày) | 19,99 | ≈ 249.000 |
| Plus trọn đời | 39,99 | ≈ 499.000 |
- Học sinh/sinh viên: giảm 40% gói năm qua mã khuyến mãi phát ở cộng đồng (Store promo code).
- Ra mắt v2.0: trọn đời −30% trong 14 ngày.
- **Người mua Pro v1**: tự động được Plus trọn đời (kiểm tra add-on `pro_unlock` khi khởi động v2).

## 3. Kỹ thuật Store [v2.0]
- Add-on trên Partner Center: `plus_monthly`, `plus_yearly` (subscription), `plus_lifetime` (durable), giữ `pro_unlock` (durable cũ).
- `Windows.Services.Store.StoreContext`: `GetAppLicenseAsync` + `GetUserCollectionAsync` để đọc quyền; `RequestPurchaseAsync` với HWND của cửa sổ (giữ cách khởi tạo `IInitializeWithWindow` của v1).
- `IEntitlements` hiện thực bởi `StoreEntitlementService` (v2.0) → `CompositeEntitlementService` (v2.1: Store cục bộ + server).
- Cache quyền offline: giữ cơ chế DPAPI v1, hạn 7 ngày với subscription, 30 ngày với durable.
- Khôi phục mua hàng: nút trong Cài đặt → đọc lại collection.

## 4. Entitlement phía server [v2.1]
- Khi người dùng đăng nhập và có quyền Store: client lấy **Store ID key** (`GetCustomerPurchaseIdAsync`) với token do server cấp → server gọi Microsoft Store Purchase/Collections API xác minh → ghi `entitlements`.
- Server là nguồn sự thật cho quyền dùng tính năng server (sync không giới hạn thiết bị, AI, tạo thử thách). Client là nguồn sự thật cho tính năng thuần cục bộ khi offline.
- Kiểm tra lại hằng ngày (job) và khi nhận thông báo thay đổi subscription; hết hạn → hạ quyền.
- Giai đoạn mobile: thêm nguồn App Store/Google Play, cùng bảng `entitlements` để mua ở đâu dùng ở đó.

## 5. Hạ cấp (Plus hết hạn) — không bao giờ xóa dữ liệu
- Thói quen vượt 5: giữ nguyên, đánh dấu "chỉ đọc" (xem, không check-in mới), người dùng tự chọn 5 thói quen tiếp tục chạy (thay cách "2 cái đầu tiên" của v1, vốn không cho người dùng chọn).
- Theme Plus đang dùng: chuyển về Classic Glow, giữ cấu hình để bật lại khi nâng cấp.
- Thiết bị sync vượt 2: thiết bị đăng nhập lâu nhất không dùng bị tạm dừng sync (vẫn dùng offline), người dùng chọn lại được.

## 6. Paywall
- Chỉ hiện theo 02-UX-SCREENS §12. Không dùng mẹo tối (dark pattern): nút đóng rõ ràng, giá hiển thị đầy đủ, ghi rõ tự gia hạn và cách hủy.
- Đo: lượt xem paywall theo nguồn → bắt đầu dùng thử → mua (telemetry opt-in).

## 7. Tiêu chí nghiệm thu
1. Mua thử bằng tài khoản test Store cho cả 3 gói; khôi phục mua hàng hoạt động.
2. Người dùng có `pro_unlock` v1 → Plus trọn đời ngay lần mở đầu tiên v2.
3. Offline 6 ngày với subscription: vẫn Plus; ngày 8: hỏi kiểm tra lại, không khóa dữ liệu.
4. Hạ cấp với 9 thói quen: người dùng chọn được 5, 4 còn lại chỉ đọc, không mất dòng dữ liệu nào.
5. Không có đường nào trong UI gọi mua hàng khi người dùng chưa bấm nút mua (test).

## Tiến độ triển khai (06/11/2026)
**Đã có**
- Lõi thuần có test: `LicensePolicy` (thuê bao: ≤ 7 ngày offline vẫn Plus, 7–14 ngày vẫn Plus nhưng hỏi kiểm tra lại, quá 14 ngày về Free không khóa dữ liệu; hết hạn đã biết thì dừng ngay; gói vĩnh viễn và Pro v1 luôn Plus, chỉ nhắc kiểm tra sau 30 ngày), `LicensePolicy.FromOwned` (ưu tiên trọn đời > Pro v1 > năm > tháng), `LiveEntitlements` (đổi Free/Plus tức thì cho mọi dịch vụ), `ThemeAccess`, `HabitAllowance` (hạ cấp: người dùng chọn 5 thói quen chạy, còn lại chỉ đọc).
- `PlusService`: cache offline (DPAPI theo người dùng Windows, file `.plus.dat` trong thư mục dữ liệu), xác minh với Store khi mở app, khôi phục mua hàng, cập nhật danh sách chỉ đọc khi quyền hoặc thói quen đổi.
- `WindowsPlusStore`: `StoreContext` (`GetAssociatedStoreProductsAsync`, `GetAppLicenseAsync`, `RequestPurchaseAsync` với HWND qua `InitializeWithWindow`). Chỉ hoạt động khi app có danh tính gói MSIX; bản không đóng gói thì nút Mua bị vô hiệu kèm giải thích — **không bao giờ tự cấp quyền** (bản v1 `StoreLicenseService` có đường "giả lập" mở Pro miễn phí khi không đóng gói; v2 không giữ đường này). Store giả chỉ có trong bản Debug với `HABITGRID_FAKE_STORE=1`.
- Paywall: lợi ích, 3 gói với giá từ Store (không có Store thì giá tham khảo có nhãn), "Tiết kiệm 44%" và "Dùng thử 7 ngày" ở gói năm, điều khoản tự gia hạn và cách hủy, nút "Để sau" rõ ràng, khôi phục mua hàng. Chỉ mở khi người dùng chạm giới hạn 5 thói quen (sau khi bấm Lưu), chọn theme khóa, hoặc tự bấm Nâng cấp.
- Hạ cấp: thói quen chỉ đọc bị chặn ghi ở mọi đường (trang Hôm nay, widget, Quick check-in, toast nhắc, palette, Focus), không nhận nhắc nhở, hiện nhãn "🔒 Chỉ đọc"; hộp chọn thói quen đang chạy (hỏi một lần mỗi phiên và trong Cài đặt). Theme Plus đang dùng thì hiển thị Candy Bento nhưng giữ lựa chọn để nâng cấp lại là dùng tiếp.
- Quyết định theme: Candy Bento và Neon Night miễn phí; Soft Pastel thuộc Plus hoặc mở ở cấp 10 (thay cho "Classic Glow, Midnight" của bản spec cũ, vốn không còn tồn tại).

**Tiêu chí nghiệm thu:** 2 (Pro v1 → Plus), 3 (offline 6 / 8 ngày), 4 (9 thói quen → chọn 5, 4 chỉ đọc, không mất dữ liệu), 5 (test quét mã nguồn: chỉ nút Mua trên paywall gọi mua) có test tự động. **Tiêu chí 1 (mua thật bằng tài khoản test Store) chưa làm được**: cần tạo add-on trên Partner Center và đóng gói MSIX (SPEC-V2-12).

**Chưa làm:** add-on trên Partner Center, đóng gói MSIX cho app WinUI, mã khuyến mãi sinh viên / giảm giá ra mắt (cấu hình trên Partner Center), xác minh phía server [v2.1], đo phễu paywall (telemetry opt-in, SPEC-V2-11), các quyền Plus chưa có tính năng tương ứng (Insights toàn lịch sử, nền ảnh widget, theme mùa, âm thanh Focus, chọn thư mục sao lưu).
