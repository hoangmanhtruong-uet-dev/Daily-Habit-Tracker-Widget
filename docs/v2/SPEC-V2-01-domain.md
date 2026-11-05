# SPEC-V2-01 — Domain v2 (`HabitGrid.Core`)

> Phát hành: [v2.0] • Phụ thuộc: không • Dự án: `src/HabitGrid.Core`, `tests/HabitGrid.Core.Tests`
> Nguyên tắc: Core thuần .NET 10, không tham chiếu UI/SQLite/HTTP, chạy được trên cả client và server.

## 1. Mục tiêu
Mở rộng domain v1 để hỗ trợ loại thói quen, tần suất, trạng thái ngày, ghi chú/mood, freeze và các trường cần cho sync. Giữ tương thích dữ liệu v1 (mọi habit v1 phải ánh xạ được, không mất thông tin).

## 2. Mô hình

### 2.1 `Habit`
| Trường | Kiểu | Ghi chú |
|---|---|---|
| `Id` | `Guid` (v7) | Giữ Id v1 khi migrate. Habit mới dùng UUIDv7 (sắp theo thời gian) |
| `Name` | string ≤ 40 | Bắt buộc, trim |
| `Description` | string? ≤ 200 | |
| `Icon` | `HabitIcon` | `fluent:<key>` hoặc `emoji:<grapheme>`; v1 `Checkmark24Regular` → `fluent:Checkmark24Regular` |
| `ColorHue` | `string` hex | Thay `PaletteId`/`CustomColorHex` v1; preset → hex tương ứng |
| `Kind` | `HabitKind` | `Boolean`, `Count`, `Duration`, `Quit` |
| `Target` | `HabitTarget` | `Value` (int > 0), `Unit` (string? ≤ 12). Boolean = 1. Duration tính bằng phút. Quit: không dùng |
| `Frequency` | `HabitFrequency` | Xem 2.3 |
| `TimeOfDay` | `TimeOfDay` | `Anytime`, `Morning`, `Afternoon`, `Evening` |
| `StartDate` | `DateOnly` | |
| `Reminders` | `List<Reminder>` | `Time` (TimeOnly), `Days` (mask), `Smart` (bool), `FollowUp` (bool) |
| `AskMood` / `AllowNotes` / `AllowFreeze` | bool | Mặc định false / true / true |
| `RoutineId` + `RoutineOrder` | `Guid?`, int | Thuộc routine nào |
| `Tags` | `List<string>` | ≤ 5 tag, mỗi tag ≤ 20 ký tự |
| `SortOrder` | `string` | **Fractional index** (ví dụ `"a0"`, `"a0V"`) để kéo-thả không cần đánh lại số và không xung đột khi sync |
| `ArchivedAt`, `DeletedAt` | `DateTime?` UTC | Xóa = tombstone, dọn hẳn sau 30 ngày đã sync |
| `CreatedAt`, `UpdatedAt` | `DateTime` UTC | |
| `Version` | `Hlc` | Đồng hồ logic lai cho sync (SPEC-V2-02) |
| `SharedChallengeId` | `Guid?` | [v2.2] |

### 2.2 `DayLog` (thay `Entry` v1)
Khóa `(HabitId, Day)`.
| Trường | Kiểu | Ghi chú |
|---|---|---|
| `Value` | int | Boolean 0/1; Count số lần; Duration số phút; Quit: số lần lỡ (0 = giữ được) |
| `Status` | `DayStatus` | `None`, `Done`, `Partial`, `Skipped`, `Frozen`, `Failed` (chỉ Quit) |
| `Note` | string? ≤ 500 | |
| `Mood` | `int?` 1–5 | |
| `CheckedAt` | `DateTime?` UTC | Lần check gần nhất (cho insight theo giờ) |
| `UpdatedAt`, `Version` | | Sync |

`Status` là **giá trị suy ra** từ `Value` + `Target` trừ `Skipped`/`Frozen` (đặt tay). Lưu cả hai để truy vấn nhanh, nhưng `DayLogRules.Normalize()` MUST được gọi trước khi lưu.

### 2.3 `HabitFrequency`
```csharp
public abstract record HabitFrequency
{
    public sealed record Daily : HabitFrequency;
    public sealed record WeekDays(DayOfWeekMask Days) : HabitFrequency;      // v1 ScheduleMask
    public sealed record TimesPerWeek(int Times) : HabitFrequency;           // 1–6
    public sealed record TimesPerMonth(int Times) : HabitFrequency;          // 1–27
    public sealed record EveryNDays(int N, DateOnly Anchor) : HabitFrequency;// 2–30
}
```
Lưu dạng JSON có discriminator `type`.

### 2.4 Các thực thể khác
- `Routine`: Id, Name, Icon, TimeOfDay, danh sách HabitId theo thứ tự.
- `UserProfile` (cục bộ): DisplayName, BuddyName, BuddyColor, Xp, Level, FreezeTokens, Timezone.
- `Achievement`: Id (key cố định), UnlockedAt.
- `XpEvent`: Id, Kind, Amount, OccurredAt, HabitId? (nhật ký để tính lại được, không chỉ cộng dồn).

## 3. Quy tắc nghiệp vụ

### 3.1 Ngày logic
Giữ `DayProvider` v1 (mốc chuyển ngày 00:00–05:00). Bổ sung: ngày logic tính theo **múi giờ của người dùng lưu trong profile**, không phải múi giờ máy tại thời điểm xem, để du lịch đổi múi giờ không làm sai streak. Toàn bộ app MUST lấy "hôm nay" từ một `IDayProvider` duy nhất (lỗi v1 đã sửa một phần ở `SettingsDayProvider`).

### 3.2 Streak theo loại tần suất
| Tần suất | Đơn vị streak | Điều kiện giữ |
|---|---|---|
| Daily | ngày | Mọi ngày đều Done/Frozen/Skipped |
| WeekDays | ngày có lịch | Ngày có lịch Done/Frozen/Skipped; ngày không lịch bỏ qua |
| TimesPerWeek | **tuần** | Số ngày Done trong tuần (theo ngày đầu tuần cài đặt) ≥ Times. Tuần hiện tại chưa kết thúc không làm đứt streak |
| TimesPerMonth | **tháng** | Tương tự theo tháng |
| EveryNDays | lượt | Có Done trong mỗi cửa sổ N ngày kể từ Anchor |
| Quit | ngày | Số ngày liên tục `Value == 0` kể từ lần lỡ gần nhất (hoặc StartDate) |

- `Skipped`: không tính hoàn thành, **không đứt** streak, không cộng vào tỷ lệ (loại khỏi mẫu số). Giới hạn: tối đa 2 skip/7 ngày/thói quen (chống lạm dụng), cấu hình được.
- `Frozen`: như Skipped nhưng tiêu 1 freeze token (SPEC-V2-06).
- Hôm nay chưa xong **không làm đứt** streak cho tới khi hết ngày logic (giữ quy tắc v1).

API:
```csharp
public interface IStreakCalculator
{
    StreakResult Calculate(Habit habit, IReadOnlyList<DayLog> logs, DateOnly today);
}
public sealed record StreakResult(int Current, int Best, StreakUnit Unit, bool AtRiskToday, DateOnly? LastCompleted);
```

### 3.3 Tỷ lệ hoàn thành
`rate = done / (scheduled − skipped − frozen)` trong khoảng, với `scheduled` tính theo tần suất (TimesPerWeek: mẫu số = Times mỗi tuần đầy đủ, tuần dở dang tính tỷ lệ theo số ngày đã qua). Trả về `null` khi mẫu số = 0.

### 3.4 Validation
`HabitValidator` trả về danh sách lỗi có mã (`name.required`, `target.range`, `frequency.times.range`…) để UI dịch, không ném exception cho lỗi nhập liệu.

### 3.5 Giới hạn gói (đọc từ `IEntitlements`, không hard-code)
`MaxActiveHabits`, `MaxReminderPerHabit`, `CanUseTheme(id)`, `CanUseAiCoach`… Core chỉ hỏi interface; Platform cung cấp hiện thực.

## 4. Dịch vụ Core mới
| Service | Trách nhiệm |
|---|---|
| `IScheduleCalculator` | Ngày nào "cần làm" theo Frequency (mở rộng v1) |
| `IStreakCalculator` | Như 3.2 |
| `IStatsCalculator` | Tỷ lệ, tổng, trung bình, theo thứ, theo giờ, theo tháng |
| `IXpEngine` | Tính XP từ sự kiện (SPEC-V2-06) |
| `IAchievementEngine` | Kiểm tra điều kiện huy hiệu |
| `IInsightEngine` | Sinh insight tự động (SPEC-V2-07) |
| `IPaletteGenerator` | Thang 5 mức OKLCH từ hue + theme |
| `FractionalIndex` | Sinh khóa sắp xếp giữa hai khóa |
| `Hlc` | Hybrid Logical Clock (dùng chung client/server) |

## 5. Tiêu chí nghiệm thu
1. Mọi quy tắc 3.2 có test, gồm biên: đổi ngày đầu tuần, tháng 2 năm nhuận, đổi giờ mùa hè, đổi múi giờ, mốc chuyển ngày 03:00, skip quá giới hạn.
2. Test thuộc tính (property-based, FsCheck): streak hiện tại ≤ kỷ lục; tỷ lệ ∈ [0,1]; Normalize idempotent.
3. Mọi habit v1 (fixture lấy từ test v1) ánh xạ sang v2 rồi tính streak cho kết quả **bằng** v1 với cùng dữ liệu.
4. Core không tham chiếu gói nào ngoài BCL (+ test). Script kiểm tra trong CI.
5. `IStreakCalculator.Calculate` cho 1 thói quen × 3 năm dữ liệu < 1 ms (benchmark).
