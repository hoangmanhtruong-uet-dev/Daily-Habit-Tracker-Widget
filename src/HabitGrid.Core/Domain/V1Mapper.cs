using V1 = HabitGrid.Core.Models;

namespace HabitGrid.Core.Domain;

/// <summary>Ánh xạ dữ liệu v1 sang domain v2 (không mất thông tin). Dùng cho migration (SPEC-V2-02 A.3).</summary>
public static class V1Mapper
{
    /// <summary>Màu đại diện cho mỗi preset palette của v1 (tông giữa của thang màu).</summary>
    private static readonly Dictionary<string, string> PresetColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["default"] = "#40C463",
        ["blue"] = "#5AB0F5",
        ["purple"] = "#B07CF0",
        ["orange"] = "#FFA94D",
        ["pink"] = "#FF8FB8",
        ["grayscale"] = "#A0A0A0"
    };

    public static Habit ToV2(V1.Habit v1, string sortKey)
    {
        ArgumentNullException.ThrowIfNull(v1);

        var kind = v1.Kind == V1.HabitKind.Count ? HabitKind.Count : HabitKind.Boolean;

        HabitFrequency frequency = v1.ScheduleMask == V1.DayOfWeekMask.AllWeek || v1.ScheduleMask == V1.DayOfWeekMask.None
            ? new HabitFrequency.Daily()
            : new HabitFrequency.WeekDays(v1.ScheduleMask);

        var reminders = new List<Reminder>();
        if (v1.ReminderTime is { } time)
        {
            reminders.Add(new Reminder(time, v1.ReminderMask));
        }

        return new Habit
        {
            Id = v1.Id,
            Name = v1.Name,
            Icon = new HabitIcon(v1.Icon),
            ColorHue = ResolveColor(v1),
            Kind = kind,
            Target = new HabitTarget(kind == HabitKind.Count ? Math.Max(1, v1.Target) : 1),
            Frequency = frequency,
            StartDate = v1.StartDate,
            Reminders = reminders,
            SortKey = sortKey,
            ArchivedAt = v1.ArchivedAt,
            DeletedAt = v1.DeletedAt,
            CreatedAt = v1.CreatedAt,
            UpdatedAt = v1.CreatedAt
        };
    }

    /// <summary>Ánh xạ cả danh sách, gán sort key mới theo đúng thứ tự SortOrder cũ.</summary>
    public static IReadOnlyList<Habit> ToV2(IReadOnlyList<V1.Habit> habits)
    {
        ArgumentNullException.ThrowIfNull(habits);

        var ordered = habits.OrderBy(h => h.SortOrder).ThenBy(h => h.CreatedAt).ToList();
        var keys = FractionalIndex.Generate(null, null, ordered.Count);

        return ordered.Select((h, i) => ToV2(h, keys[i])).ToList();
    }

    public static DayLog ToV2(V1.Entry entry, Habit habit)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(habit);

        var log = new DayLog
        {
            HabitId = entry.HabitId,
            Day = entry.Day,
            Value = entry.Value,
            UpdatedAt = entry.UpdatedAt
        };

        return DayLogRules.Normalize(habit, log);
    }

    private static string ResolveColor(V1.Habit v1)
    {
        if (!string.IsNullOrWhiteSpace(v1.CustomColorHex))
        {
            return v1.CustomColorHex!.ToUpperInvariant();
        }

        return PresetColors.TryGetValue(v1.PaletteId ?? "default", out var color) ? color : PresetColors["default"];
    }
}
