namespace HabitGrid.Core.Domain.Routines;

public enum RoutineError
{
    None,
    NameRequired,
    NameTooLong,
    TooFewHabits,
    TooManyHabits,
    LimitReached,
    UnknownHabit,
    NotFound
}

/// <summary>Quy tắc routine (F1): Free tối đa 2 routine, Plus không giới hạn; mỗi routine 2–12 thói quen; một thói quen thuộc tối đa một routine.</summary>
public static class RoutineRules
{
    public const int FreeMaxRoutines = 2;
    public const int MaxNameLength = 30;
    public const int MinHabits = 2;
    public const int MaxHabits = 12;

    public static int MaxRoutines(bool isPlus) => isPlus ? int.MaxValue : FreeMaxRoutines;

    /// <param name="existingRoutines">Số routine đang có (không tính routine đang sửa).</param>
    public static RoutineError Validate(string? name, int habitCount, int existingRoutines, bool isPlus, bool isNew)
    {
        var trimmed = name?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            return RoutineError.NameRequired;
        }

        if (trimmed.Length > MaxNameLength)
        {
            return RoutineError.NameTooLong;
        }

        if (habitCount < MinHabits)
        {
            return RoutineError.TooFewHabits;
        }

        if (habitCount > MaxHabits)
        {
            return RoutineError.TooManyHabits;
        }

        if (isNew && existingRoutines >= MaxRoutines(isPlus))
        {
            return RoutineError.LimitReached;
        }

        return RoutineError.None;
    }

    /// <summary>Thói quen có thể vào routine: đang theo dõi, không phải loại cần bỏ (không có "bước làm").</summary>
    public static bool CanJoin(Habit habit) => habit.IsActive && habit.Kind != HabitKind.Quit;
}
