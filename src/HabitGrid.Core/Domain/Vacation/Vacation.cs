namespace HabitGrid.Core.Domain.Vacation;

/// <summary>Một kỳ tạm nghỉ đang chạy. <see cref="HabitIds"/> rỗng = mọi thói quen.</summary>
public sealed record VacationInfo(DateOnly From, DateOnly To, IReadOnlyList<Guid> HabitIds)
{
    public bool IsAll => HabitIds.Count == 0;

    public int Days => To.DayNumber - From.DayNumber + 1;

    public bool Covers(DateOnly day) => day >= From && day <= To;

    public bool Covers(Guid habitId) => IsAll || HabitIds.Contains(habitId);
}

public enum VacationError
{
    None,
    InPast,
    EndBeforeStart,
    TooLong,
    AlreadyActive,
    NothingToPause
}

public sealed record VacationCheck(VacationError Error, int MaxDays)
{
    public bool IsValid => Error == VacationError.None;
}

/// <summary>
/// Quy tắc tạm nghỉ (F3). Ngày nghỉ được ghi thành "đóng băng" (<see cref="DayStatus.Frozen"/>) nên chuỗi, nhắc nhở,
/// XP và Insights vốn đã coi đó là ngày trung tính. Khác freeze (cứu ngày đã lỡ, tốn token), tạm nghỉ đặt trước, miễn phí.
/// Free: tối đa 14 ngày mỗi lần; Plus: 60 ngày.
/// </summary>
public static class VacationRules
{
    public const int FreeMaxDays = 14;
    public const int PlusMaxDays = 60;

    public static int MaxDays(bool isPlus) => isPlus ? PlusMaxDays : FreeMaxDays;

    public static VacationCheck Validate(DateOnly from, DateOnly to, DateOnly today, bool isPlus, VacationInfo? active)
    {
        var max = MaxDays(isPlus);

        if (active is not null && active.To >= today)
        {
            return new VacationCheck(VacationError.AlreadyActive, max);
        }

        if (from < today)
        {
            return new VacationCheck(VacationError.InPast, max);
        }

        if (to < from)
        {
            return new VacationCheck(VacationError.EndBeforeStart, max);
        }

        if (to.DayNumber - from.DayNumber + 1 > max)
        {
            return new VacationCheck(VacationError.TooLong, max);
        }

        return new VacationCheck(VacationError.None, max);
    }

    /// <summary>Các ngày cần đánh dấu nghỉ cho một thói quen: ngày có lịch, từ ngày bắt đầu, chưa có bản ghi nào (không đè lên "Xong").</summary>
    public static IReadOnlyList<DateOnly> DaysToMark(
        Habit habit,
        VacationInfo vacation,
        IReadOnlyCollection<DateOnly> daysWithLogs,
        IScheduleCalculator? schedule = null)
    {
        ArgumentNullException.ThrowIfNull(habit);
        ArgumentNullException.ThrowIfNull(vacation);

        schedule ??= new ScheduleCalculator();

        if (!habit.IsActive || habit.Kind == HabitKind.Quit || !vacation.Covers(habit.Id))
        {
            return [];
        }

        var days = new List<DateOnly>();

        for (var day = vacation.From < habit.StartDate ? habit.StartDate : vacation.From; day <= vacation.To; day = day.AddDays(1))
        {
            if (schedule.IsScheduled(habit, day) && !daysWithLogs.Contains(day))
            {
                days.Add(day);
            }
        }

        return days;
    }

    /// <summary>Kết thúc sớm: chỉ gỡ đóng băng từ hôm nay trở đi (quá khứ giữ nguyên). Ngày đóng băng do freeze token luôn ở quá khứ nên không bị ảnh hưởng.</summary>
    public static bool ShouldClear(DayLog log, VacationInfo vacation, DateOnly today) =>
        log.Status == DayStatus.Frozen && log.Value == 0 && log.Day >= today && vacation.Covers(log.Day) && vacation.Covers(log.HabitId);
}
