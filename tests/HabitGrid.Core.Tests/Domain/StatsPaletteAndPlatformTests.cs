using FluentAssertions;
using HabitGrid.Core.Domain;
using HabitGrid.Core.Services;
using Xunit;
using static HabitGrid.Core.Tests.Domain.DomainTestData;

namespace HabitGrid.Core.Tests.Domain;

public class StatsPaletteAndPlatformTests
{
    private readonly StatsCalculator _stats = new();

    // ---- Tỷ lệ hoàn thành ----

    [Fact]
    public void Rate_Daily_ExcludesSkippedAndFrozenFromDenominator()
    {
        var habit = NewHabit(start: Today.AddDays(-9));
        var logs = new List<DayLog>();
        logs.AddRange(DoneRange(habit, Today.AddDays(-9), Today.AddDays(-5))); // 5 xong
        logs.Add(WithStatus(habit, Today.AddDays(-4), DayStatus.Skipped));
        logs.Add(WithStatus(habit, Today.AddDays(-3), DayStatus.Frozen));
        // 5 ngày còn lại không có log

        var rate = _stats.CompletionRate(habit, logs, Today.AddDays(-9), Today.AddDays(-0));

        // 10 ngày có lịch, loại 2 → mẫu số 8, xong 5.
        rate.Should().BeApproximately(5 / 8.0, 1e-9);
    }

    [Fact]
    public void Rate_NoScheduledDays_IsNull()
    {
        var habit = NewHabit(frequency: new HabitFrequency.WeekDays(HabitGrid.Core.Models.DayOfWeekMask.Monday));

        // Khoảng 21–22/10 (thứ Tư, thứ Năm) không có thứ Hai nào.
        _stats.CompletionRate(habit, [], Today.AddDays(-1), Today).Should().BeNull();
    }

    [Fact]
    public void Rate_BeforeStartDate_IsClippedOrNull()
    {
        var habit = NewHabit(start: Today);

        _stats.CompletionRate(habit, [], Today.AddDays(-10), Today.AddDays(-5)).Should().BeNull();
    }

    [Fact]
    public void Rate_TimesPerWeek_UsesProportionalExpectation()
    {
        // 3 lần/tuần, đúng 2 tuần, làm 6 lần → 100%.
        var habit = NewHabit(frequency: new HabitFrequency.TimesPerWeek(3), start: new DateOnly(2026, 10, 5));
        var logs = new[] { 5, 7, 9, 12, 14, 16 }.Select(d => Done(habit, new DateOnly(2026, 10, d))).ToList();

        _stats.CompletionRate(habit, logs, new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 18))
            .Should().BeApproximately(1.0, 1e-9);

        // Làm 3/6 → 50%.
        _stats.CompletionRate(habit, logs.Take(3).ToList(), new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 18))
            .Should().BeApproximately(0.5, 1e-9);
    }

    [Fact]
    public void Rate_Quit_IsShareOfCleanDays()
    {
        var habit = NewHabit(HabitKind.Quit, start: Today.AddDays(-9));
        var logs = new List<DayLog> { WithStatus(habit, Today.AddDays(-2), DayStatus.Failed, 1) };

        _stats.CompletionRate(habit, logs, Today.AddDays(-9), Today).Should().BeApproximately(0.9, 1e-9);
    }

    [Fact]
    public void Rate_RandomData_AlwaysWithinZeroAndOne()
    {
        var random = new Random(555);

        for (var i = 0; i < 400; i++)
        {
            var habit = NewHabit(
                random.Next(5) == 0 ? HabitKind.Quit : HabitKind.Boolean,
                StreakCalculatorV2Tests.RandomFrequency(random),
                start: Today.AddDays(-random.Next(1, 300)));
            var logs = StreakCalculatorV2Tests.RandomLogs(random, habit);

            var rate = _stats.CompletionRate(habit, logs, Today.AddDays(-random.Next(1, 100)), Today);

            if (rate is { } value)
            {
                value.Should().BeInRange(0.0, 1.0, $"case {i}");
            }
        }
    }

    [Fact]
    public void RateByWeekday_SeparatesDays()
    {
        var habit = NewHabit(start: new DateOnly(2026, 10, 5));
        // Làm mọi thứ Hai và không làm thứ Ba trong 2 tuần.
        var logs = new List<DayLog>
        {
            Done(habit, new DateOnly(2026, 10, 5)),
            Done(habit, new DateOnly(2026, 10, 12))
        };

        var rates = _stats.RateByWeekday(habit, logs, new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 18));

        rates[DayOfWeek.Monday].Should().Be(1.0);
        rates[DayOfWeek.Tuesday].Should().Be(0.0);
    }

    [Fact]
    public void CheckInsByHour_UsesRequestedTimeZone()
    {
        var habit = NewHabit();
        var utc = new DateTime(2026, 10, 22, 17, 30, 0, DateTimeKind.Utc);
        var log = Done(habit, Today) with { CheckedAt = utc };
        var vietnam = TimeZoneInfo.CreateCustomTimeZone("vn", TimeSpan.FromHours(7), "vn", "vn");

        var buckets = _stats.CheckInsByHour([log], vietnam);

        buckets[0].Should().Be(1, "17:30 UTC là 00:30 ngày hôm sau ở UTC+7");
        buckets.Sum().Should().Be(1);
    }

    [Fact]
    public void TotalDoneAndValue()
    {
        var habit = NewHabit(HabitKind.Duration, target: 30);
        var logs = new List<DayLog>
        {
            Done(habit, Today) with { Value = 45 },
            WithStatus(habit, Today.AddDays(-1), DayStatus.Partial, 20),
            WithStatus(habit, Today.AddDays(-9), DayStatus.None, 0)
        };

        _stats.TotalDone(logs).Should().Be(1);
        _stats.TotalValue(logs, Today.AddDays(-5), Today).Should().Be(65);
    }

    // ---- DayProvider theo múi giờ ----

    [Fact]
    public void DayProvider_UsesProfileTimeZone_NotMachineZone()
    {
        var tokyo = TimeZoneInfo.CreateCustomTimeZone("jp", TimeSpan.FromHours(9), "jp", "jp");
        var provider = new DayProvider(TimeSpan.FromHours(3), timeZone: tokyo);

        // 17:00 UTC = 02:00 ngày hôm sau ở UTC+9, trước mốc 03:00 → vẫn là ngày logic trước đó.
        var instant = new DateTime(2026, 10, 22, 17, 0, 0, DateTimeKind.Utc);

        provider.ToLogicalDate(instant).Should().Be(new DateOnly(2026, 10, 22));
        provider.ToLogicalDate(instant.AddHours(2)).Should().Be(new DateOnly(2026, 10, 23));
    }

    [Fact]
    public void DayProvider_NextTransition_IsInTheFuture()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("x", TimeSpan.FromHours(-5), "x", "x");
        var now = new DateTime(2026, 10, 22, 23, 30, 0);
        var provider = new DayProvider(TimeSpan.FromHours(2), () => now, zone);

        var next = provider.GetNextDayTransitionUtc();

        // 23/10 02:00 giờ UTC-5 = 23/10 07:00 UTC.
        next.Should().Be(new DateTime(2026, 10, 23, 7, 0, 0, DateTimeKind.Utc));
    }

    // ---- Bảng màu OKLCH ----

    [Theory]
    [InlineData("#8B5CF6")]
    [InlineData("#10B981")]
    [InlineData("#FACC15")]
    [InlineData("#64748B")]
    [InlineData("#F43F5E")]
    public void Palette_ProducesValidDistinctHexShades(string hex)
    {
        foreach (var dark in new[] { false, true })
        {
            var shades = OklchPalette.Generate(hex, dark);
            var all = Enumerable.Range(0, 5).Select(i => shades[i]).ToList();

            all.Should().OnlyContain(c => System.Text.RegularExpressions.Regex.IsMatch(c, "^#[0-9A-F]{6}$"));
            all.Distinct().Should().HaveCount(5);
        }
    }

    [Theory]
    [InlineData("#8B5CF6")]
    [InlineData("#10B981")]
    [InlineData("#F43F5E")]
    public void Palette_LightThemeGetsDarkerWithLevel_DarkThemeGetsBrighter(string hex)
    {
        double Lightness(string c) => OklchPalette.ToOklch(
            Convert.ToInt32(c.Substring(1, 2), 16),
            Convert.ToInt32(c.Substring(3, 2), 16),
            Convert.ToInt32(c.Substring(5, 2), 16)).L;

        var light = OklchPalette.Generate(hex, isDark: false);
        var dark = OklchPalette.Generate(hex, isDark: true);

        Lightness(light.Level1).Should().BeGreaterThan(Lightness(light.Level4));
        Lightness(dark.Level1).Should().BeLessThan(Lightness(dark.Level4));
    }

    [Fact]
    public void Palette_RejectsBadHex()
    {
        FluentActions.Invoking(() => OklchPalette.Generate("nope", false)).Should().Throw<FormatException>();
    }

    [Fact]
    public void Oklch_RoundTripKeepsColorClose()
    {
        var (l, c, h) = OklchPalette.ToOklch(0x8B, 0x5C, 0xF6);

        OklchPalette.FromOklch(l, c, h).Should().Be("#8B5CF6");
    }

    // ---- Cô lập Core ----

    [Fact]
    public void CoreAssembly_ReferencesOnlyBaseClassLibrary()
    {
        var references = typeof(HabitGrid.Core.Domain.Habit).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name!)
            .ToList();

        references.Should().OnlyContain(
            n => n.StartsWith("System", StringComparison.Ordinal) || n == "netstandard" || n == "mscorlib",
            "Core không được phụ thuộc UI, SQLite hay HTTP");
    }
}
