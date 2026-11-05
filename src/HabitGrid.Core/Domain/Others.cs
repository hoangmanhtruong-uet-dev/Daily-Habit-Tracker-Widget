namespace HabitGrid.Core.Domain;

/// <summary>Nhóm thói quen theo thứ tự.</summary>
public sealed record Routine(
    Guid Id,
    string Name,
    HabitIcon Icon,
    TimeOfDay TimeOfDay,
    string SortKey,
    IReadOnlyList<Guid> HabitIds);

/// <summary>Hồ sơ cục bộ của người dùng.</summary>
public sealed record UserProfile
{
    public string DisplayName { get; init; } = string.Empty;

    public string BuddyName { get; init; } = "Buddy";

    public string BuddyColor { get; init; } = "#8B5CF6";

    public long Xp { get; init; }

    public int Level { get; init; } = 1;

    public int FreezeTokens { get; init; }

    /// <summary>Id múi giờ dùng để tính ngày logic.</summary>
    public string TimeZoneId { get; init; } = TimeZoneInfo.Local.Id;
}

/// <summary>Huy hiệu đã mở khóa.</summary>
public sealed record Achievement(string Id, DateTime UnlockedAt);

/// <summary>Nhật ký XP để tính lại được khi đổi quy tắc.</summary>
public sealed record XpEvent(Guid Id, string Kind, int Amount, DateTime OccurredAt, Guid? HabitId);

/// <summary>Giới hạn theo gói; Core chỉ hỏi interface, Platform cung cấp hiện thực.</summary>
public interface IEntitlements
{
    int MaxActiveHabits { get; }

    int MaxRemindersPerHabit { get; }

    bool CanUseTheme(string themeId);

    bool CanUseAiCoach { get; }
}
