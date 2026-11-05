namespace HabitGrid.Core.Domain.Gamification;

/// <summary>Kế hoạch dùng freeze cho các ngày vừa lỡ.</summary>
/// <param name="Days">Các ngày sẽ được đóng băng (rỗng nếu không cần hoặc không cứu được).</param>
/// <param name="TokensNeeded">Số token cần (một token mỗi ngày, áp cho mọi thói quen ngày đó).</param>
/// <param name="NeedsConfirmation">True khi người dùng tắt "Tự dùng freeze": cần hỏi khi mở app.</param>
public sealed record FreezePlan(IReadOnlyList<DateOnly> Days, int TokensNeeded, bool NeedsConfirmation)
{
    public static FreezePlan None { get; } = new([], 0, false);

    public bool HasPlan => Days.Count > 0;
}

/// <summary>
/// Quy tắc streak freeze (SPEC-V2-06 §4). Freeze chỉ dùng khi nó thật sự cứu được chuỗi tổng: số ngày lỡ liên tiếp
/// (tính tới hôm qua) không vượt số token đang có. Không đủ token thì không tiêu token nào (chuỗi vẫn đứt như bình thường).
/// </summary>
public static class FreezeEngine
{
    public const int FreeCap = 3;
    public const int PlusCap = 5;
    public const int StreakDaysPerToken = 7;
    public const int MaxLookbackDays = 14;

    public static int Cap(bool isPlus) => isPlus ? PlusCap : FreeCap;

    /// <summary>Token kiếm được từ chuỗi tổng: 1 token cho mỗi 7 ngày.</summary>
    public static int EarnedFromStreak(int overallStreak) => Math.Max(0, overallStreak) / StreakDaysPerToken;

    /// <summary>Cộng token nhưng không vượt trần.</summary>
    public static int Add(int current, int amount, bool isPlus) => Math.Clamp(current + amount, 0, Cap(isPlus));

    /// <summary>
    /// Lập kế hoạch từ tập ngày có hoạt động (≥ 1 thói quen xong) và tập ngày đã đóng băng.
    /// Ngày hôm nay còn đang diễn ra nên không bao giờ bị coi là lỡ.
    /// </summary>
    public static FreezePlan Plan(
        DateOnly today,
        IReadOnlySet<DateOnly> activeDays,
        IReadOnlySet<DateOnly> frozenDays,
        int tokens,
        bool autoUse)
    {
        var gap = new List<DateOnly>();
        var day = today.AddDays(-1);

        while (!activeDays.Contains(day) && !frozenDays.Contains(day) && gap.Count <= MaxLookbackDays)
        {
            gap.Add(day);
            day = day.AddDays(-1);
        }

        // Không có ngày lỡ, hoặc trước chỗ lỡ không có chuỗi nào để cứu (người dùng mới/đã nghỉ lâu).
        if (gap.Count == 0 || gap.Count > MaxLookbackDays || (!activeDays.Contains(day) && !frozenDays.Contains(day)))
        {
            return FreezePlan.None;
        }

        if (gap.Count > tokens)
        {
            return FreezePlan.None;
        }

        gap.Reverse();
        return new FreezePlan(gap, gap.Count, !autoUse);
    }
}
