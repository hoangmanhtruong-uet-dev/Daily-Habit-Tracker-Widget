using CommunityToolkit.Mvvm.ComponentModel;
using HabitGrid.Core.Domain;
using HabitGrid.Core.Domain.Gamification;
using HabitGrid.Presentation.Services;

namespace HabitGrid.Presentation.ViewModels;

public enum BuddyStage
{
    Egg,
    Baby,
    Teen,
    Adult,
    Legend
}

/// <summary>Một ô huy hiệu trên trang Buddy. Huy hiệu bí mật chưa đạt thì <see cref="Hidden"/> = true (UI không lộ điều kiện).</summary>
public sealed record BadgeItem(
    string Id,
    string Group,
    bool Unlocked,
    bool Hidden,
    int Tier,
    int TierCount,
    long Progress,
    long Target,
    DateTime? UnlockedAt);

/// <summary>Trang Buddy: linh vật theo level, XP, token freeze và lưới huy hiệu (SPEC-V2-06 §2–§5).</summary>
public sealed partial class BuddyViewModel : ObservableObject
{
    private readonly GamificationService _service;
    private readonly IProfileRepository _profile;
    private readonly IAchievementRepository _achievements;

    public BuddyViewModel(GamificationService service, IProfileRepository profile, IAchievementRepository achievements)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        _achievements = achievements ?? throw new ArgumentNullException(nameof(achievements));
    }

    [ObservableProperty]
    private string _name = "Buddy";

    [ObservableProperty]
    private string _color = "#8B5CF6";

    [ObservableProperty]
    private int _level = 1;

    [ObservableProperty]
    private long _xpIntoLevel;

    [ObservableProperty]
    private long _xpForNextLevel;

    [ObservableProperty]
    private double _levelProgress;

    [ObservableProperty]
    private long _totalXp;

    [ObservableProperty]
    private int _freezeTokens;

    [ObservableProperty]
    private int _overallStreak;

    [ObservableProperty]
    private BuddyStage _stage;

    [ObservableProperty]
    private int _unlockedCount;

    [ObservableProperty]
    private string? _errorMessage;

    public IReadOnlyList<BadgeItem> Badges { get; private set; } = [];

    public int TotalCount => Badges.Count;

    public static BuddyStage StageFor(int level) => level switch
    {
        >= 50 => BuddyStage.Legend,
        >= 30 => BuddyStage.Adult,
        >= 15 => BuddyStage.Teen,
        >= 5 => BuddyStage.Baby,
        _ => BuddyStage.Egg
    };

    public async Task LoadAsync()
    {
        try
        {
            ErrorMessage = null;
            var snapshot = await _service.RefreshAsync();
            var profile = await _profile.GetAsync();
            var unlockedAt = (await _achievements.GetAllAsync()).ToDictionary(a => a.Id, a => a.UnlockedAt);

            // Huy hiệu đã mở là vĩnh viễn: hiển thị mở dù điều kiện hiện tại không còn đúng.
            var badges = snapshot.Achievements
                .Select(r =>
                {
                    var unlocked = r.Unlocked || unlockedAt.ContainsKey(r.Id);
                    return new BadgeItem(
                        r.Id,
                        r.Definition.Group,
                        unlocked,
                        r.Definition.Secret && !unlocked,
                        Math.Max(r.Tier, unlocked ? 1 : 0),
                        r.Definition.Tiers.Count,
                        r.Progress,
                        r.Definition.Tiers[Math.Min(Math.Max(r.Tier, 1), r.Definition.Tiers.Count) - 1],
                        unlockedAt.TryGetValue(r.Id, out var at) ? at : null);
                })
                .ToList();

            Badges = badges;
            Name = profile.BuddyName;
            Color = profile.BuddyColor;
            Level = snapshot.Xp.Level;
            TotalXp = snapshot.Xp.TotalXp;
            XpIntoLevel = snapshot.Xp.XpIntoLevel;
            XpForNextLevel = snapshot.Xp.XpForNextLevel;
            LevelProgress = snapshot.Xp.LevelProgress;
            FreezeTokens = snapshot.FreezeTokens;
            OverallStreak = snapshot.OverallStreak;
            Stage = StageFor(snapshot.Xp.Level);
            UnlockedCount = badges.Count(b => b.Unlocked);
            OnPropertyChanged(nameof(Badges));
            OnPropertyChanged(nameof(TotalCount));
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}
