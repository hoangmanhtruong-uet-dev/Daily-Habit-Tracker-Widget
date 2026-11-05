using HabitGrid.Core.Domain;
using V1 = HabitGrid.Core.Models;

namespace HabitGrid.WidgetHost;

/// <summary>Chuyển thói quen/nhật ký v3 sang mô hình v1 mà <c>WidgetViewModel</c> hiểu.</summary>
public static class WidgetMapper
{
    public static V1.Habit ToWidgetHabit(Habit habit) => new()
    {
        Id = habit.Id,
        Name = habit.Name,
        Kind = habit.Kind == HabitKind.Boolean ? V1.HabitKind.Boolean : V1.HabitKind.Count,
        Target = habit.Kind == HabitKind.Boolean ? 1 : habit.EffectiveTarget,
        ScheduleMask = V1.DayOfWeekMask.AllWeek,
        PaletteId = "custom",
        CustomColorHex = habit.ColorHue,
        StartDate = habit.StartDate
    };

    /// <summary>Giá trị mỗi ngày cho heatmap: có/không → 1 khi xong; đếm/thời lượng → giá trị thật; bỏ qua/đóng băng → bỏ.</summary>
    public static Dictionary<DateOnly, int> ToValues(Habit habit, IEnumerable<DayLog> logs)
    {
        var values = new Dictionary<DateOnly, int>();

        foreach (var log in logs)
        {
            if (log.Status is DayStatus.Skipped or DayStatus.Frozen || log.Value <= 0)
            {
                continue;
            }

            values[log.Day] = habit.Kind == HabitKind.Boolean ? 1 : log.Value;
        }

        return values;
    }
}
