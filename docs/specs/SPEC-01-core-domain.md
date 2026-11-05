# Gói Công Việc 01: Core Domain & Thuật Toán Tính Toán (SPEC-01)

> **Mã công việc**: `SPEC-01-CORE`  
> **Thư viện mục tiêu**: `HabitGrid.Core` (.NET 8 Class Library, không chứa UI, không chứa SQL)  
> **Dự án kiểm thử**: `HabitGrid.Core.Tests` (xUnit + FluentAssertions)  
> **Mục tiêu**: Định nghĩa toàn bộ thực thể nghiệp vụ (Domain Entities), hợp đồng logic (Interfaces), thuật toán tính chuỗi thói quen (Streak Calculation), xử lý lịch lặp (Schedule Bitmask), mốc chuyển ngày (Logical Day), và bộ sinh bảng màu trực quan (Palette Generator).

---

## 1. MÔ HÌNH THỰC THỂ (DOMAIN MODELS)

### 1.1 Enum & Value Objects

```csharp
namespace HabitGrid.Core.Models;

public enum HabitKind
{
    Boolean = 0, // Thói quen Đúng/Sai (Đã làm / Chưa làm)
    Count = 1    // Thói quen Đếm số lượng (mục tiêu >= Target/ngày)
}

[Flags]
public enum DayOfWeekMask
{
    None      = 0,
    Monday    = 1 << 0, // 1
    Tuesday   = 1 << 1, // 2
    Wednesday = 1 << 2, // 4
    Thursday  = 1 << 3, // 8
    Friday    = 1 << 4, // 16
    Saturday  = 1 << 5, // 32
    Sunday    = 1 << 6, // 64
    AllWeek   = Monday | Tuesday | Wednesday | Thursday | Friday | Saturday | Sunday // 127
}
```

### 1.2 Thực thể `Habit`

```csharp
namespace HabitGrid.Core.Models;

public class Habit
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty; // Độ dài 1..40 ký tự
    public string Icon { get; set; } = "Checkmark24Regular"; // Fluent Icon Key
    public HabitKind Kind { get; set; } = HabitKind.Boolean;
    public int Target { get; set; } = 1; // Áp dụng cho Count (mặc định 1)
    public DayOfWeekMask ScheduleMask { get; set; } = DayOfWeekMask.AllWeek;
    public string PaletteId { get; set; } = "default";
    public string? CustomColorHex { get; set; } // Mã Hex #RRGGBB (Dành cho Pro)
    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Now);
    public TimeOnly? ReminderTime { get; set; }
    public DayOfWeekMask ReminderMask { get; set; } = DayOfWeekMask.AllWeek;
    public int SortOrder { get; set; } = 0;
    public DateTime? ArchivedAt { get; set; }
    public DateTime? DeletedAt { get; set; } // Hỗ trợ Soft Delete
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    public bool IsActive => DeletedAt == null && ArchivedAt == null;
    public bool IsArchived => ArchivedAt != null && DeletedAt == null;
}
```

### 1.3 Thực thể `Entry` (Nhật ký thực hiện ngày)

```csharp
namespace HabitGrid.Core.Models;

public class Entry
{
    public Guid HabitId { get; init; }
    public DateOnly Day { get; init; } // Ngày logic (yyyy-MM-dd)
    public int Value { get; set; } // Boolean: 0 hoặc 1. Count: số nguyên >= 0
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public bool IsCompleted(Habit habit)
    {
        return habit.Kind == HabitKind.Boolean ? Value >= 1 : Value >= habit.Target;
    }
}
```

---

## 2. QUY TẮC NGHIỆP VỤ & DỊCH VỤ CỐT LÕI (DOMAIN SERVICES)

### 2.1 Xử lý ngày logic (`IDayProvider`)
Người thức khuya thường coi lúc 01:00 AM hay 02:00 AM vẫn thuộc "hôm qua". Hệ thống hỗ trợ cấu hình giờ chuyển ngày `DayStartHour` (mặc định 00:00, cho phép cấu hình từ 00:00 đến 05:00).

```csharp
namespace HabitGrid.Core.Services;

public interface IDayProvider
{
    TimeSpan DayStartOffset { get; }
    DateOnly GetToday();
    DateOnly ToLogicalDate(DateTime localDateTime);
    DateTime GetNextDayTransitionUtc();
}
```

- **Quy tắc**: Nếu `now.TimeOfDay < DayStartOffset`, `ToLogicalDate(now)` trả về `DateOnly.FromDateTime(now.AddDays(-1))`. Ngược lại, trả về `DateOnly.FromDateTime(now)`.

### 2.2 Xử lý lịch lặp (`IScheduleCalculator`)

```csharp
namespace HabitGrid.Core.Services;

public interface IScheduleCalculator
{
    bool IsScheduled(Habit habit, DateOnly date);
    int CountScheduledDays(Habit habit, DateOnly fromDate, DateOnly toDate);
}
```

- **Quy tắc chuyển đổi `DayOfWeek`**:
  - `DayOfWeek.Monday` tương ứng bit `Monday` (1 << 0).
  - `DayOfWeek.Sunday` tương ứng bit `Sunday` (1 << 6).
- Ngày không nằm trong lịch (`IsScheduled == false`) được gọi là **Off-Day**.

### 2.3 Thuật toán tính Streak (`IStreakCalculator`)

```csharp
namespace HabitGrid.Core.Services;

public record StreakResult(
    int CurrentStreak,
    int LongestStreak,
    double CompletionRate30Days,
    int TotalCompletedDays
);

public interface IStreakCalculator
{
    StreakResult Calculate(Habit habit, IReadOnlyList<Entry> entries, DateOnly today);
}
```

**Quy tắc bất biến (Invariants) khi tính Streak**:
1. **Lọc ngày hợp lệ**: Chỉ xét các ngày $\ge$ `habit.StartDate` và thuộc lịch (`IsScheduled(habit, d) == true`).
2. **Current Streak (Chuỗi hiện tại)**:
   - Nếu `today` là ngày có lịch và đã hoàn thành: tính từ `today` lùi dần về quá khứ qua từng ngày có lịch.
   - Nếu `today` là ngày có lịch nhưng **chưa hoàn thành**: **chưa bị gãy streak**! Điểm xuất phát tính từ ngày có lịch liền kề trước đó (`previousScheduledDay`). Nếu ngày liền kề trước đó đã hoàn thành, streak vẫn tiếp tục được giữ.
   - Nếu `today` không phải ngày có lịch: lùi về ngày có lịch gần nhất để kiểm tra.
   - Khi gặp một ngày có lịch mà `IsCompleted == false`: Chuỗi hiện tại dừng lại ngay lập tức.
3. **Longest Streak (Chuỗi kỷ lục)**: Là chuỗi dài nhất của các ngày có lịch liên tiếp được hoàn thành trong toàn bộ lịch sử ghi nhận.
4. **Completion Rate 30 Days (Tỷ lệ 30 ngày)**:
   $$\text{Rate} = \frac{\text{Số ngày có lịch hoàn thành trong khoảng } [today - 29, today]}{\text{Tổng số ngày có lịch trong khoảng } [today - 29, today]}$$
   Nếu tổng số ngày có lịch = 0, trả về 0.0.
5. **Đánh dấu ngoài lịch (Bonus completion)**: Nếu người dùng đánh dấu vào ngày off-day, ngày đó được tính vào `TotalCompletedDays`, nhưng không làm gãy chuỗi ngày lịch bình thường.

### 2.4 Bảng màu trực quan Heatmap (`IPaletteGenerator`)

Lưới hiển thị 5 mức trạng thái (0: Trống, 1: 1-24%, 2: 25-49%, 3: 50-99%, 4: >=100% hoặc Boolean Đã xong).

```csharp
namespace HabitGrid.Core.Services;

public record HeatmapShades(
    string Level0Empty, // Rỗng (thích ứng theme Sáng/Tối)
    string Level1,      // Nhạt nhất
    string Level2,      // Nhạt vừa
    string Level3,      // Đậm vừa
    string Level4       // Đậm nhất (hoặc màu gốc)
);

public interface IPaletteGenerator
{
    HeatmapShades GenerateFromHex(string baseHexColor, bool isDarkTheme);
    HeatmapShades GetPreset(string presetId, bool isDarkTheme);
}
```

- Bảng màu mặc định (GitHub Green):
  - Sáng: `Level1: #9BE9A8`, `Level2: #40C463`, `Level3: #30A14E`, `Level4: #216E39`.
  - Tối: `Level1: #0E4429`, `Level2: #006D32`, `Level3: #26A641`, `Level4: #39D353`.
- Đảm bảo độ tương phản tối thiểu giữa ô rỗng và ô cấp 1 để người dùng nhận biết rõ ngày đã check-in.

---

## 3. BỘ KIỂM THỬ ĐƠN VỊ CẦN THỰC THI (UNIT TEST REQUIREMENTS)

Dự án `HabitGrid.Core.Tests` phải cài đặt đầy đủ các ca kiểm thử sau:

### 3.1 Nhóm `StreakCalculatorTests`
- `Calculate_WhenNoEntries_ShouldReturnZeroStreak()`
- `Calculate_WhenCompletedToday_ShouldIncludeTodayInStreak()`
- `Calculate_WhenTodayNotCompletedYet_ShouldKeepStreakFromYesterday()`
- `Calculate_WhenYesterdayMissed_ShouldBreakCurrentStreakToZero()`
- `Calculate_WithWeekendOffSchedule_ShouldSkipWeekendsWithoutBreakingStreak()`
- `Calculate_ForCountHabit_WhenValueLessThanTarget_ShouldNotCountAsCompleted()`
- `Calculate_ForCountHabit_WhenValueReachesOrExceedsTarget_ShouldCountAsCompleted()`
- `Calculate_LongestStreak_WhenMultipleStreaksExist_ShouldReturnMaximum()`
- `Calculate_30DayRate_ShouldCalculateAccurateRatio()`

### 3.2 Nhóm `DayProviderTests`
- `ToLogicalDate_BeforeCutoffHour_ShouldBelongToPreviousCalendarDay()`
- `ToLogicalDate_AfterCutoffHour_ShouldBelongToCurrentCalendarDay()`

### 3.3 Nhóm `PaletteGeneratorTests`
- `GenerateFromHex_WithCustomColor_ShouldReturnValid5Shades()`
- `GetPreset_WithDefaultPreset_ShouldReturnStandardGitHubColors()`

---

## 4. TIÊU CHUẨN NGHIỆM THU (ACCEPTANCE CRITERIA)
- [ ] Chạy `dotnet test` vượt qua 100% test cases trong `HabitGrid.Core.Tests`.
- [ ] Không có bất kỳ phụ thuộc nào vào Windows Presentation Foundation (WPF) hay Microsoft.Data.Sqlite trong `HabitGrid.Core`.
- [ ] Code tuân thủ nullable reference types (`<Nullable>enable</Nullable>`) không sinh ra compiler warnings.
