using System.Globalization;
using HabitGrid.Core.Models;

namespace HabitGrid.Data.Repositories;

/// <summary>
/// Chuyển đổi giữa giá trị SQL và các kiểu dữ liệu của Domain.
/// Định dạng lưu trữ: ngày = yyyy-MM-dd, giờ = HH:mm, mốc thời gian = ISO-8601 UTC.
/// </summary>
internal static class SqlMapping
{
    public const string DateFormat = "yyyy-MM-dd";
    public const string TimeFormat = "HH:mm";

    public static string ToSqlDate(DateOnly value) =>
        value.ToString(DateFormat, CultureInfo.InvariantCulture);

    public static DateOnly ToDateOnly(string value) =>
        DateOnly.ParseExact(value, DateFormat, CultureInfo.InvariantCulture);

    public static string? ToSqlTime(TimeOnly? value) =>
        value?.ToString(TimeFormat, CultureInfo.InvariantCulture);

    public static TimeOnly? ToTimeOnly(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : TimeOnly.ParseExact(value, TimeFormat, CultureInfo.InvariantCulture);

    public static string ToSqlTimestamp(DateTime value) =>
        value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    public static string? ToSqlTimestamp(DateTime? value) =>
        value.HasValue ? ToSqlTimestamp(value.Value) : null;

    public static DateTime ToTimestamp(string value) =>
        DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToUniversalTime();

    public static DateTime? ToTimestampOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : ToTimestamp(value);

    /// <summary>Chuyển một hàng kết quả thành thực thể <see cref="Habit"/>.</summary>
    public static Habit ToHabit(Microsoft.Data.Sqlite.SqliteDataReader reader)
    {
        return new Habit
        {
            Id = Guid.Parse(reader.GetString(0)),
            Name = reader.GetString(1),
            Icon = reader.GetString(2),
            Kind = (HabitKind)reader.GetInt32(3),
            Target = reader.GetInt32(4),
            ScheduleMask = (DayOfWeekMask)reader.GetInt32(5),
            PaletteId = reader.GetString(6),
            CustomColorHex = reader.IsDBNull(7) ? null : reader.GetString(7),
            StartDate = ToDateOnly(reader.GetString(8)),
            ReminderTime = ToTimeOnly(reader.IsDBNull(9) ? null : reader.GetString(9)),
            ReminderMask = (DayOfWeekMask)reader.GetInt32(10),
            SortOrder = reader.GetInt32(11),
            ArchivedAt = ToTimestampOrNull(reader.IsDBNull(12) ? null : reader.GetString(12)),
            DeletedAt = ToTimestampOrNull(reader.IsDBNull(13) ? null : reader.GetString(13)),
            CreatedAt = ToTimestamp(reader.GetString(14)),
            IsReadOnly = !reader.IsDBNull(15) && reader.GetInt32(15) != 0
        };
    }

    /// <summary>Chuyển một hàng kết quả thành thực thể <see cref="Entry"/>.</summary>
    public static Entry ToEntry(Microsoft.Data.Sqlite.SqliteDataReader reader)
    {
        return new Entry
        {
            HabitId = Guid.Parse(reader.GetString(0)),
            Day = ToDateOnly(reader.GetString(1)),
            Value = reader.GetInt32(2),
            UpdatedAt = ToTimestamp(reader.GetString(3))
        };
    }
}
