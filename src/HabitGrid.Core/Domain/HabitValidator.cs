using System.Text.RegularExpressions;

namespace HabitGrid.Core.Domain;

/// <summary>Một lỗi nhập liệu với mã ổn định để UI/server dịch.</summary>
public sealed record ValidationError(string Code, string Field);

/// <summary>Kiểm tra dữ liệu thói quen. Trả về danh sách lỗi, không ném exception cho lỗi nhập liệu.</summary>
public static partial class HabitValidator
{
    public const int MaxNameLength = 40;
    public const int MaxDescriptionLength = 200;
    public const int MaxTags = 5;
    public const int MaxTagLength = 20;
    public const int MaxUnitLength = 12;
    public const int MaxTarget = 100_000;
    public const int MaxReminders = 5;

    public static IReadOnlyList<ValidationError> Validate(Habit habit)
    {
        ArgumentNullException.ThrowIfNull(habit);

        var errors = new List<ValidationError>();

        var name = habit.Name?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            errors.Add(new("name.required", nameof(Habit.Name)));
        }
        else if (name.Length > MaxNameLength)
        {
            errors.Add(new("name.too_long", nameof(Habit.Name)));
        }

        if (habit.Description is { Length: > MaxDescriptionLength })
        {
            errors.Add(new("description.too_long", nameof(Habit.Description)));
        }

        if (!HexColor().IsMatch(habit.ColorHue ?? string.Empty))
        {
            errors.Add(new("color.invalid", nameof(Habit.ColorHue)));
        }

        ValidateTarget(habit, errors);
        ValidateFrequency(habit, errors);

        if (habit.Tags.Count > MaxTags)
        {
            errors.Add(new("tags.too_many", nameof(Habit.Tags)));
        }

        if (habit.Tags.Any(t => string.IsNullOrWhiteSpace(t) || t.Length > MaxTagLength))
        {
            errors.Add(new("tags.invalid", nameof(Habit.Tags)));
        }

        if (habit.Reminders.Count > MaxReminders)
        {
            errors.Add(new("reminders.too_many", nameof(Habit.Reminders)));
        }

        if (!FractionalIndex.IsValid(habit.SortKey))
        {
            errors.Add(new("sortkey.invalid", nameof(Habit.SortKey)));
        }

        return errors;
    }

    private static void ValidateTarget(Habit habit, List<ValidationError> errors)
    {
        if (habit.Kind is HabitKind.Count or HabitKind.Duration)
        {
            if (habit.Target.Value is < 1 or > MaxTarget)
            {
                errors.Add(new("target.range", nameof(Habit.Target)));
            }
        }

        if (habit.Target.Unit is { Length: > MaxUnitLength })
        {
            errors.Add(new("target.unit_too_long", nameof(Habit.Target)));
        }
    }

    private static void ValidateFrequency(Habit habit, List<ValidationError> errors)
    {
        switch (habit.Frequency)
        {
            case HabitFrequency.WeekDays { Days: 0 }:
                errors.Add(new("frequency.days.empty", nameof(Habit.Frequency)));
                break;
            case HabitFrequency.TimesPerWeek { Times: < 1 or > 6 }:
            case HabitFrequency.TimesPerMonth { Times: < 1 or > 27 }:
                errors.Add(new("frequency.times.range", nameof(Habit.Frequency)));
                break;
            case HabitFrequency.EveryNDays { N: < 2 or > 30 }:
                errors.Add(new("frequency.interval.range", nameof(Habit.Frequency)));
                break;
        }
    }

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColor();
}
