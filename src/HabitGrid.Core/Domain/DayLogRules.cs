namespace HabitGrid.Core.Domain;

/// <summary>Quy tắc chuẩn hóa và giới hạn cho <see cref="DayLog"/> (SPEC-V2-01 §2.2, §3.2).</summary>
public static class DayLogRules
{
    /// <summary>Số lần bỏ qua tối đa trong 7 ngày cho mỗi thói quen (chống lạm dụng).</summary>
    public const int DefaultMaxSkipsPer7Days = 2;

    /// <summary>
    /// Chuẩn hóa một bản ghi: kẹp giá trị, suy ra <see cref="DayStatus"/>, cắt ghi chú, kiểm tra mood.
    /// Idempotent: Normalize(Normalize(x)) == Normalize(x).
    /// </summary>
    public static DayLog Normalize(Habit habit, DayLog log)
    {
        ArgumentNullException.ThrowIfNull(habit);
        ArgumentNullException.ThrowIfNull(log);

        var value = Math.Max(0, log.Value);
        var note = NormalizeNote(log.Note);
        var mood = log.Mood is >= 1 and <= 5 ? log.Mood : null;

        // Skipped/Frozen do người dùng đặt, được giữ nguyên.
        var status = log.Status is DayStatus.Skipped or DayStatus.Frozen
            ? log.Status
            : DeriveStatus(habit, value);

        return log with { Value = value, Status = status, Note = note, Mood = mood };
    }

    /// <summary>Suy ra trạng thái từ giá trị (không xét Skipped/Frozen).</summary>
    public static DayStatus DeriveStatus(Habit habit, int value)
    {
        ArgumentNullException.ThrowIfNull(habit);

        if (habit.Kind == HabitKind.Quit)
        {
            return value > 0 ? DayStatus.Failed : DayStatus.Done;
        }

        if (value <= 0)
        {
            return DayStatus.None;
        }

        if (habit.Kind == HabitKind.Boolean)
        {
            return DayStatus.Done;
        }

        var target = habit.EffectiveTarget;

        if (value >= target)
        {
            return DayStatus.Done;
        }

        // Từ 50% mục tiêu trở lên tính là Partial.
        return value * 2 >= target ? DayStatus.Partial : DayStatus.None;
    }

    /// <summary>Có được bỏ qua <paramref name="day"/> không (tối đa N lần trong cửa sổ 7 ngày chứa ngày đó).</summary>
    public static bool CanSkip(
        IEnumerable<DayLog> logs,
        DateOnly day,
        int maxSkipsPer7Days = DefaultMaxSkipsPer7Days)
    {
        ArgumentNullException.ThrowIfNull(logs);

        var skipDays = logs
            .Where(l => l.Status == DayStatus.Skipped && l.Day != day)
            .Select(l => l.Day)
            .ToList();

        // Mọi cửa sổ 7 ngày liên tiếp chứa 'day' đều không được vượt giới hạn sau khi thêm 'day'.
        for (var start = day.AddDays(-6); start <= day; start = start.AddDays(1))
        {
            var end = start.AddDays(6);
            var count = skipDays.Count(d => d >= start && d <= end) + 1;

            if (count > maxSkipsPer7Days)
            {
                return false;
            }
        }

        return true;
    }

    private static string? NormalizeNote(string? note)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            return null;
        }

        var trimmed = note.Trim();
        return trimmed.Length > DayLog.MaxNoteLength ? trimmed[..DayLog.MaxNoteLength] : trimmed;
    }
}
