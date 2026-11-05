namespace HabitGrid.Core.Domain;

/// <summary>Ghi nhận của một thói quen trong một ngày logic (thay Entry v1). Khóa (HabitId, Day).</summary>
public sealed record DayLog
{
    public const int MaxNoteLength = 500;

    public Guid HabitId { get; init; }

    public DateOnly Day { get; init; }

    /// <summary>Boolean 0/1; Count số lần; Duration số phút; Quit số lần lỡ.</summary>
    public int Value { get; init; }

    public DayStatus Status { get; init; }

    public string? Note { get; init; }

    /// <summary>Mood 1–5.</summary>
    public int? Mood { get; init; }

    /// <summary>Thời điểm check gần nhất (UTC), dùng cho thống kê theo giờ.</summary>
    public DateTime? CheckedAt { get; init; }

    public DateTime UpdatedAt { get; init; } = DateTime.UtcNow;

    public Hlc Version { get; init; }
}
