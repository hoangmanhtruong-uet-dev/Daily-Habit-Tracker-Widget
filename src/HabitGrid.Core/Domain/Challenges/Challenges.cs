namespace HabitGrid.Core.Domain.Challenges;

/// <summary>Thử thách mẫu (F2). Tên hiển thị tra theo <see cref="Id"/> trong resource (<c>challenge.{Id}</c>).</summary>
public sealed record ChallengeTemplate(
    string Id,
    string Emoji,
    HabitKind Kind,
    int DefaultDays,
    int Target = 1,
    TimeOfDay TimeOfDay = TimeOfDay.Anytime);

/// <summary>Một thử thách đang theo đuổi: gắn với một thói quen, có ngày bắt đầu và độ dài cố định.</summary>
public sealed record Challenge(
    Guid Id,
    Guid HabitId,
    string Title,
    string TemplateId,
    DateOnly Start,
    int Days,
    DateOnly? CompletedOn = null,
    DateOnly? DismissedOn = null)
{
    public DateOnly End => Start.AddDays(Days - 1);
}

public enum ChallengeState
{
    Active,
    Completed,
    Failed
}

/// <param name="DayNumber">Ngày thứ mấy của thử thách (1..Days), dừng ở Days khi đã qua ngày cuối.</param>
/// <param name="GoodDays">Số ngày đạt (hoặc được miễn do tạm nghỉ/bỏ qua).</param>
/// <param name="FailedOn">Ngày đầu tiên lỡ, nếu có.</param>
public sealed record ChallengeProgress(Challenge Challenge, ChallengeState State, int DayNumber, int GoodDays, DateOnly? FailedOn)
{
    public double Fraction => Challenge.Days == 0 ? 0 : Math.Min(1.0, GoodDays / (double)Challenge.Days);

    public int DaysLeft => Math.Max(0, Challenge.Days - DayNumber);
}

public static class ChallengeCatalog
{
    public const int MinDays = 3;
    public const int MaxDays = 100;
    public static readonly IReadOnlyList<int> DayChoices = [7, 14, 21, 30, 66, 100];

    public static readonly IReadOnlyList<ChallengeTemplate> Templates =
    [
        new("noSugarDrinks", "🧋", HabitKind.Quit, 30),
        new("noSocialMorning", "📵", HabitKind.Quit, 14, TimeOfDay: TimeOfDay.Morning),
        new("noLateSnacks", "🌙", HabitKind.Quit, 21, TimeOfDay: TimeOfDay.Evening),
        new("read10", "📖", HabitKind.Count, 30, Target: 10),
        new("walk", "🚶", HabitKind.Count, 30, Target: 6000),
        new("water", "💧", HabitKind.Count, 21, Target: 8),
        new("meditate", "🧘", HabitKind.Duration, 21, Target: 10, TimeOfDay: TimeOfDay.Morning),
        new("earlyBed", "😴", HabitKind.Boolean, 14, TimeOfDay: TimeOfDay.Evening),
        new("journal", "✍️", HabitKind.Boolean, 30, TimeOfDay: TimeOfDay.Evening),
        new("stretch", "🤸", HabitKind.Boolean, 21, TimeOfDay: TimeOfDay.Morning)
    ];

    public static ChallengeTemplate? Find(string id) => Templates.FirstOrDefault(t => t.Id == id);
}

/// <summary>
/// Đánh giá thử thách theo nhật ký của thói quen gắn kèm. Quy tắc "liền mạch": mọi ngày từ lúc bắt đầu phải đạt; ngày tạm nghỉ/bỏ qua được miễn;
/// hôm nay chưa xong chưa tính là lỡ; thói quen Quit mặc định "sạch" nếu không ghi lỡ (hôm nay chỉ tính khi ngày đã trôi qua).
/// </summary>
public static class ChallengeEngine
{
    public static ChallengeProgress Evaluate(Challenge challenge, Habit habit, IReadOnlyList<DayLog> logs, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(challenge);
        ArgumentNullException.ThrowIfNull(habit);

        var latest = new Dictionary<DateOnly, DayLog>();

        foreach (var log in logs.Where(l => l.HabitId == habit.Id))
        {
            if (!latest.TryGetValue(log.Day, out var existing) || log.UpdatedAt >= existing.UpdatedAt)
            {
                latest[log.Day] = log;
            }
        }

        var good = 0;
        DateOnly? failedOn = null;
        var last = today < challenge.End ? today : challenge.End;

        for (var day = challenge.Start; day <= last; day = day.AddDays(1))
        {
            latest.TryGetValue(day, out var log);
            var status = log?.Status ?? DayStatus.None;

            if (status is DayStatus.Frozen or DayStatus.Skipped)
            {
                good++;
                continue;
            }

            var isToday = day == today;

            if (habit.Kind == HabitKind.Quit)
            {
                if (status == DayStatus.Failed || log?.Value > 0)
                {
                    failedOn ??= day;
                }
                else if (!isToday)
                {
                    good++;
                }

                continue;
            }

            if (status == DayStatus.Done)
            {
                good++;
            }
            else if (!isToday)
            {
                failedOn ??= day;
            }
        }

        var dayNumber = Math.Clamp(today.DayNumber - challenge.Start.DayNumber + 1, 0, challenge.Days);
        var state = failedOn is not null
            ? ChallengeState.Failed
            : good >= challenge.Days ? ChallengeState.Completed : ChallengeState.Active;

        return new ChallengeProgress(challenge, state, dayNumber, good, failedOn);
    }
}
