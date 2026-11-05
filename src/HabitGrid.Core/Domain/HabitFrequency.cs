using HabitGrid.Core.Models;

namespace HabitGrid.Core.Domain;

/// <summary>Tần suất của thói quen. Tuần tự hóa JSON có trường phân biệt <c>type</c>.</summary>
[System.Text.Json.Serialization.JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(Daily), "daily")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(WeekDays), "weekDays")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(TimesPerWeek), "timesPerWeek")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(TimesPerMonth), "timesPerMonth")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(EveryNDays), "everyNDays")]
public abstract record HabitFrequency
{
    private HabitFrequency()
    {
    }

    /// <summary>Mỗi ngày.</summary>
    public sealed record Daily : HabitFrequency;

    /// <summary>Các thứ cố định trong tuần (tương ứng ScheduleMask của v1).</summary>
    public sealed record WeekDays(DayOfWeekMask Days) : HabitFrequency;

    /// <summary>X lần mỗi tuần, 1–6.</summary>
    public sealed record TimesPerWeek(int Times) : HabitFrequency;

    /// <summary>X lần mỗi tháng, 1–27.</summary>
    public sealed record TimesPerMonth(int Times) : HabitFrequency;

    /// <summary>Mỗi N ngày kể từ mốc Anchor, N trong 2–30.</summary>
    public sealed record EveryNDays(int N, DateOnly Anchor) : HabitFrequency;

    /// <summary>Đơn vị streak tương ứng với tần suất.</summary>
    public StreakUnit Unit => this switch
    {
        TimesPerWeek => StreakUnit.Week,
        TimesPerMonth => StreakUnit.Month,
        EveryNDays => StreakUnit.Window,
        _ => StreakUnit.Day
    };

    /// <summary>True nếu mục tiêu gắn với một chu kỳ (tuần/tháng/cửa sổ) thay vì từng ngày cụ thể.</summary>
    public bool IsFlexible => this is TimesPerWeek or TimesPerMonth or EveryNDays;
}
