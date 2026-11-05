using FluentAssertions;
using HabitGrid.Core.Domain;
using HabitGrid.Core.Domain.Gamification;
using Xunit;
using static HabitGrid.Core.Tests.Domain.DomainTestData;

namespace HabitGrid.Core.Tests.Gamification;

public class LevelCurveTests
{
    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 152)]
    [InlineData(5, 657)]
    [InlineData(10, 1991)]
    public void XpToReach_FollowsTheSpecFormula(int level, long expected) =>
        LevelCurve.XpToReach(level).Should().Be(expected);

    [Fact]
    public void LevelFor_IsMonotonic_AndCapsAt100()
    {
        var previous = 0;

        for (long xp = 0; xp < 120_000; xp += 97)
        {
            var level = LevelCurve.LevelFor(xp);
            level.Should().BeGreaterThanOrEqualTo(previous);
            previous = level;
        }

        LevelCurve.LevelFor(0).Should().Be(1);
        LevelCurve.LevelFor(151).Should().Be(1);
        LevelCurve.LevelFor(152).Should().Be(2);
        LevelCurve.LevelFor(10_000_000).Should().Be(100);
    }
}

public class XpEngineTests
{
    private static DayLog At(DayLog log, DateTime checkedAtUtc) => log with { CheckedAt = checkedAtUtc };

    [Fact]
    public void Done_Is10_Partial_Is4_PerHabitPerDay()
    {
        var a = NewHabit(HabitKind.Count, target: 4);
        var b = NewHabit();
        var logs = new List<DayLog>
        {
            WithStatus(a, Today, DayStatus.Partial, 2),
            Done(b, Today)
        };

        var xp = XpEngine.Compute([a, b], logs, Today);

        xp.Awards.Where(x => x.Kind == "done").Sum(x => x.Amount).Should().Be(10);
        xp.Awards.Where(x => x.Kind == "partial").Sum(x => x.Amount).Should().Be(4);
    }

    [Fact]
    public void PerfectDay_AddsBonus_OnlyWhenEveryScheduledHabitIsDone()
    {
        var a = NewHabit();
        var b = NewHabit();

        var both = XpEngine.Compute([a, b], [Done(a, Today), Done(b, Today)], Today);
        var one = XpEngine.Compute([a, b], [Done(a, Today)], Today);

        both.Awards.Should().Contain(x => x.Kind == "perfect" && x.Amount == 20);
        both.TotalXp.Should().Be(10 + 10 + 20);
        one.Awards.Should().NotContain(x => x.Kind == "perfect");
    }

    [Fact]
    public void PerfectDay_IgnoresSkippedFrozenAndUnscheduledHabits()
    {
        var a = NewHabit();
        var skipped = NewHabit();
        var mondaysOnly = NewHabit(frequency: new HabitFrequency.WeekDays(Mask(DayOfWeek.Monday)));
        var logs = new List<DayLog> { Done(a, Today), WithStatus(skipped, Today, DayStatus.Skipped) };

        var xp = XpEngine.Compute([a, skipped, mondaysOnly], logs, Today);

        xp.Awards.Should().Contain(x => x.Kind == "perfect");
    }

    [Fact]
    public void StreakMilestone_7_IsAwardedOnce_OnTheDayItIsReached()
    {
        var h = NewHabit(start: Today.AddDays(-40));
        var logs = DoneRange(h, Today.AddDays(-9), Today);

        var xp = XpEngine.Compute([h], logs, Today);

        var milestone = xp.Awards.Where(x => x.Kind == "streak7").ToList();
        milestone.Should().ContainSingle();
        milestone[0].Amount.Should().Be(50);
        milestone[0].Day.Should().Be(Today.AddDays(-9).AddDays(6));
        xp.Awards.Should().NotContain(x => x.Kind == "streak30");
    }

    [Fact]
    public void Milestones_30_100_AreAwarded_WithTheirBonuses()
    {
        var h = NewHabit(start: Today.AddDays(-200));
        var logs = DoneRange(h, Today.AddDays(-120), Today);

        var xp = XpEngine.Compute([h], logs, Today);

        xp.Awards.Where(x => x.Kind.StartsWith("streak")).Select(x => (x.Kind, x.Amount))
            .Should().Equal(("streak7", 50), ("streak30", 150), ("streak100", 500));
    }

    [Fact]
    public void NotesAndMood_GiveTwoEach_CappedAtTenPerDay()
    {
        var habits = Enumerable.Range(0, 7).Select(_ => NewHabit()).ToList();
        var logs = habits.Select(h => Done(h, Today) with { Note = "ok" }).ToList();

        var xp = XpEngine.Compute(habits, logs, Today);

        xp.Awards.Single(x => x.Kind == "note").Amount.Should().Be(10);
    }

    [Fact]
    public void DailyCap_Is200()
    {
        var habits = Enumerable.Range(0, 40).Select(_ => NewHabit()).ToList();
        var logs = habits.Select(h => Done(h, Today)).ToList();

        var xp = XpEngine.Compute(habits, logs, Today);

        xp.PerDay[Today].Should().Be(200);
        xp.TotalXp.Should().Be(200);
    }

    [Fact]
    public void LateEdits_OlderThanTwoDays_GiveNoXp_ButRecentOnesDo()
    {
        var h = NewHabit();
        var oldDay = Today.AddDays(-10);
        var late = At(Done(h, oldDay), DateTime.SpecifyKind(Today.ToDateTime(new TimeOnly(12, 0)), DateTimeKind.Local).ToUniversalTime());
        var recent = At(Done(h, Today.AddDays(-1)), DateTime.SpecifyKind(Today.ToDateTime(new TimeOnly(12, 0)), DateTimeKind.Local).ToUniversalTime());
        var legacy = Done(h, Today.AddDays(-20));

        var xp = XpEngine.Compute([h], [late, recent, legacy], Today);

        xp.Awards.Where(x => x.Kind == "done").Select(x => x.Day).Should().BeEquivalentTo([Today.AddDays(-1), Today.AddDays(-20)]);
    }

    [Fact]
    public void RemovingACheckIn_RevokesItsXp_AndRecomputeIsDeterministic()
    {
        var h = NewHabit();
        var logs = DoneRange(h, Today.AddDays(-5), Today);

        var full = XpEngine.Compute([h], logs, Today);
        var again = XpEngine.Compute([h], logs, Today);
        var withoutToday = XpEngine.Compute([h], logs.Where(l => l.Day != Today).ToList(), Today);

        again.TotalXp.Should().Be(full.TotalXp);
        withoutToday.TotalXp.Should().BeLessThan(full.TotalXp);
        full.TotalXp.Should().Be(withoutToday.TotalXp + 10 + 20, "một lần check và thưởng ngày hoàn hảo của hôm nay bị thu hồi");
    }

    [Fact]
    public void DeletedHabitsAndQuitHabits_GiveNoXp()
    {
        var deleted = NewHabit();
        deleted.DeletedAt = DateTime.UtcNow;
        var quit = NewHabit(HabitKind.Quit);

        var xp = XpEngine.Compute([deleted, quit], [Done(deleted, Today), Done(quit, Today)], Today);

        xp.TotalXp.Should().Be(0);
    }

    [Fact]
    public void Summary_ReportsLevelProgress()
    {
        var h = NewHabit(start: Today.AddDays(-400));
        var logs = DoneRange(h, Today.AddDays(-30), Today);

        var xp = XpEngine.Compute([h], logs, Today);

        xp.Level.Should().Be(LevelCurve.LevelFor(xp.TotalXp));
        xp.XpIntoLevel.Should().Be(xp.TotalXp - LevelCurve.XpToReach(xp.Level));
        xp.LevelProgress.Should().BeInRange(0, 1);
    }

    [Fact]
    public void ThreeYearsOfDailyData_ComputesQuickly()
    {
        var habits = Enumerable.Range(0, 5).Select(_ => NewHabit(start: Today.AddDays(-1100))).ToList();
        var logs = habits.SelectMany(h => DoneRange(h, Today.AddDays(-1000), Today)).ToList();

        var best = TimeSpan.MaxValue;

        for (var i = 0; i < 3; i++)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            XpEngine.Compute(habits, logs, Today);
            sw.Stop();
            best = sw.Elapsed < best ? sw.Elapsed : best;
        }

        best.Should().BeLessThan(TimeSpan.FromSeconds(3));
    }
}

public class AchievementEngineTests
{
    private static AchievementContext Ctx(IReadOnlyList<Habit> habits, IReadOnlyList<DayLog> logs, params string[] flags) =>
        new(habits, logs, Today, flags.ToHashSet());

    private static Dictionary<string, AchievementResult> Run(AchievementContext ctx) =>
        new AchievementEngine().Evaluate(ctx).ToDictionary(r => r.Id);

    [Fact]
    public void Catalog_LoadsFromEmbeddedJson_WithUniqueIds_AndSecretBadges()
    {
        var catalog = AchievementCatalog.Default;

        catalog.Should().HaveCountGreaterThanOrEqualTo(20);
        catalog.Select(d => d.Id).Should().OnlyHaveUniqueItems();
        catalog.Count(d => d.Secret).Should().Be(3);
        catalog.Should().OnlyContain(d => d.Tiers.Count > 0 && d.Tiers.All(t => t > 0));
    }

    [Fact]
    public void NewUser_HasNothingUnlocked()
    {
        var results = new AchievementEngine().Evaluate(Ctx([], []));

        results.Should().OnlyContain(r => !r.Unlocked);
    }

    [Fact]
    public void StartBadges_UnlockFromBasicActions()
    {
        var a = NewHabit();
        var b = NewHabit();
        var c = NewHabit();
        var logs = new List<DayLog> { Done(a, Today) with { Note = "hi" }, Done(b, Today), Done(c, Today) };

        var r = Run(Ctx([a, b, c], logs));

        r["first_checkin"].Unlocked.Should().BeTrue();
        r["first_perfect_day"].Unlocked.Should().BeTrue();
        r["three_habits"].Unlocked.Should().BeTrue();
        r["first_note"].Unlocked.Should().BeTrue();
        r["streak_7"].Unlocked.Should().BeFalse();
    }

    [Fact]
    public void StreakBadges_UnlockAtTheirThresholds()
    {
        var h = NewHabit(start: Today.AddDays(-80));
        var logs = DoneRange(h, Today.AddDays(-40), Today);

        var r = Run(Ctx([h], logs));

        r["streak_7"].Unlocked.Should().BeTrue();
        r["streak_30"].Unlocked.Should().BeTrue();
        r["streak_100"].Unlocked.Should().BeFalse();
        r["streak_100"].Progress.Should().BeGreaterThanOrEqualTo(41);
    }

    [Fact]
    public void Comeback_RequiresTwoSeparateRunsOfSevenDays()
    {
        var h = NewHabit(start: Today.AddDays(-60));
        var once = DoneRange(h, Today.AddDays(-30), Today.AddDays(-22));
        var twice = once.Concat(DoneRange(h, Today.AddDays(-10), Today.AddDays(-2))).ToList();

        Run(Ctx([h], once))["comeback"].Unlocked.Should().BeFalse();
        Run(Ctx([h], twice))["comeback"].Unlocked.Should().BeTrue();
    }

    [Fact]
    public void Volume_Checkins100_AndWaterByTag()
    {
        var water = NewHabit(HabitKind.Count, target: 8, start: Today.AddDays(-200));
        water.Tags = ["Water"];
        var logs = Enumerable.Range(0, 120).Select(i => Done(water, Today.AddDays(-i)) with { Value = 8 }).ToList();

        var r = Run(Ctx([water], logs));

        r["checkins_100"].Unlocked.Should().BeTrue();
        r["checkins_1000"].Unlocked.Should().BeFalse();
        r["water_100_glasses"].Unlocked.Should().BeTrue();
    }

    [Fact]
    public void TimeBadges_UseLocalCheckInHours()
    {
        var h = NewHabit(start: Today.AddDays(-30));
        DateTime Local(int day, int hour, int minute = 30) =>
            DateTime.SpecifyKind(Today.AddDays(-day).ToDateTime(new TimeOnly(hour, minute)), DateTimeKind.Local).ToUniversalTime();

        var early = Enumerable.Range(1, 10).Select(i => Done(h, Today.AddDays(-i)) with { CheckedAt = Local(i, 6) }).ToList();
        var night = Enumerable.Range(1, 10).Select(i => Done(h, Today.AddDays(-i)) with { CheckedAt = Local(i, 23, 40) }).ToList();

        Run(Ctx([h], early))["early_bird"].Unlocked.Should().BeTrue();
        Run(Ctx([h], early))["night_owl"].Unlocked.Should().BeFalse();
        Run(Ctx([h], night))["night_owl"].Unlocked.Should().BeTrue();
    }

    [Fact]
    public void DiscoveryBadges_FollowFlags()
    {
        var r = Run(Ctx([], [], "widget_pinned", "theme_changed"));

        r["widget_pinned"].Unlocked.Should().BeTrue();
        r["theme_changed"].Unlocked.Should().BeTrue();
        r["quick_checkin_used"].Unlocked.Should().BeFalse();
    }

    [Fact]
    public void SecretBadges_HideTheirConditionUntilUnlocked_AndUnlockCorrectly()
    {
        var habits = Enumerable.Range(0, 10).Select(_ => NewHabit(start: Today.AddDays(-30))).ToList();
        var midnight = DateTime.SpecifyKind(Today.ToDateTime(new TimeOnly(0, 0)), DateTimeKind.Local).ToUniversalTime();
        var logs = habits.Select(h => Done(h, Today) with { CheckedAt = midnight }).ToList();

        var locked = Run(Ctx([], []));
        var open = Run(Ctx(habits, logs));

        locked["midnight_checkin"].HideCondition.Should().BeTrue();
        open["midnight_checkin"].Unlocked.Should().BeTrue();
        open["midnight_checkin"].HideCondition.Should().BeFalse();
        open["marathon_day"].Unlocked.Should().BeTrue();
        locked["perfect_week"].HideCondition.Should().BeTrue();
    }

    [Fact]
    public void PerfectWeek_NeedsSevenPerfectDaysInARow()
    {
        var h = NewHabit(start: Today.AddDays(-30));

        Run(Ctx([h], DoneRange(h, Today.AddDays(-6), Today)))["perfect_week"].Unlocked.Should().BeTrue();
        Run(Ctx([h], DoneRange(h, Today.AddDays(-5), Today)))["perfect_week"].Unlocked.Should().BeFalse();
    }

    [Fact]
    public void CustomJsonCatalog_AddsBadgesWithoutCodeChanges()
    {
        var json = """[{ "id": "five_checkins", "group": "volume", "rule": "totalCheckins", "tiers": [5, 20, 50] }]""";
        var engine = new AchievementEngine(AchievementCatalog.FromJson(json));
        var h = NewHabit();

        var result = engine.Evaluate(Ctx([h], DoneRange(h, Today.AddDays(-24), Today))).Single();

        result.Tier.Should().Be(2, "25 lần check đạt mốc 5 và 20 nhưng chưa tới 50");
        result.Unlocked.Should().BeTrue();
    }
}

public class FreezeEngineTests
{
    private static HashSet<DateOnly> Days(params int[] daysAgo) => daysAgo.Select(d => Today.AddDays(-d)).ToHashSet();

    [Fact]
    public void NothingMissed_NoPlan() =>
        FreezeEngine.Plan(Today, Days(1, 2, 3), new HashSet<DateOnly>(), tokens: 2, autoUse: true).HasPlan.Should().BeFalse();

    [Fact]
    public void TodayIsNeverCountedAsMissed() =>
        FreezeEngine.Plan(Today, Days(1, 2), new HashSet<DateOnly>(), tokens: 1, autoUse: true).HasPlan.Should().BeFalse();

    [Fact]
    public void MissedOneDay_WithOneToken_FreezesThatDay()
    {
        var plan = FreezeEngine.Plan(Today, Days(2, 3, 4), new HashSet<DateOnly>(), tokens: 1, autoUse: true);

        plan.Days.Should().Equal(Today.AddDays(-1));
        plan.TokensNeeded.Should().Be(1);
        plan.NeedsConfirmation.Should().BeFalse();
    }

    [Fact]
    public void MissedTwoDays_WithOneToken_CannotSave_SoSpendsNothing() =>
        FreezeEngine.Plan(Today, Days(3, 4, 5), new HashSet<DateOnly>(), tokens: 1, autoUse: true).HasPlan.Should().BeFalse();

    [Fact]
    public void MissedTwoDays_WithTwoTokens_FreezesBothInOrder()
    {
        var plan = FreezeEngine.Plan(Today, Days(3, 4, 5), new HashSet<DateOnly>(), tokens: 2, autoUse: true);

        plan.Days.Should().Equal(Today.AddDays(-2), Today.AddDays(-1));
        plan.TokensNeeded.Should().Be(2);
    }

    [Fact]
    public void AutoUseOff_AsksForConfirmation() =>
        FreezeEngine.Plan(Today, Days(2, 3), new HashSet<DateOnly>(), tokens: 1, autoUse: false).NeedsConfirmation.Should().BeTrue();

    [Fact]
    public void AlreadyFrozenDays_BridgeTheGap_AndAreNotFrozenAgain()
    {
        var plan = FreezeEngine.Plan(Today, Days(4, 5), Days(2, 3), tokens: 1, autoUse: true);

        plan.Days.Should().Equal(Today.AddDays(-1));
    }

    [Fact]
    public void NoStreakToSave_LongAbsence_NoPlan() =>
        FreezeEngine.Plan(Today, Days(60), new HashSet<DateOnly>(), tokens: 3, autoUse: true).HasPlan.Should().BeFalse();

    [Theory]
    [InlineData(0, 0)]
    [InlineData(6, 0)]
    [InlineData(7, 1)]
    [InlineData(21, 3)]
    public void EarnedFromStreak_IsOnePerSevenDays(int streak, int expected) =>
        FreezeEngine.EarnedFromStreak(streak).Should().Be(expected);

    [Fact]
    public void Add_RespectsFreeAndPlusCaps()
    {
        FreezeEngine.Add(2, 5, isPlus: false).Should().Be(3);
        FreezeEngine.Add(2, 5, isPlus: true).Should().Be(5);
        FreezeEngine.Add(0, -4, isPlus: false).Should().Be(0);
    }
}
