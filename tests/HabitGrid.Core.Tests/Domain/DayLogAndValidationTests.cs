using FluentAssertions;
using HabitGrid.Core.Domain;
using Xunit;
using static HabitGrid.Core.Tests.Domain.DomainTestData;

namespace HabitGrid.Core.Tests.Domain;

public class DayLogAndValidationTests
{
    [Theory]
    [InlineData(0, DayStatus.None)]
    [InlineData(1, DayStatus.Done)]
    [InlineData(5, DayStatus.Done)]
    public void Boolean_StatusDerivedFromValue(int value, DayStatus expected)
    {
        var habit = NewHabit();

        DayLogRules.DeriveStatus(habit, value).Should().Be(expected);
    }

    [Theory]
    [InlineData(0, DayStatus.None)]
    [InlineData(1, DayStatus.None)]   // < 50%
    [InlineData(4, DayStatus.Partial)] // đúng 50%
    [InlineData(7, DayStatus.Partial)]
    [InlineData(8, DayStatus.Done)]
    [InlineData(20, DayStatus.Done)]
    public void Count_StatusDerivedFromTarget(int value, DayStatus expected)
    {
        var habit = NewHabit(HabitKind.Count, target: 8);

        DayLogRules.DeriveStatus(habit, value).Should().Be(expected);
    }

    [Theory]
    [InlineData(0, DayStatus.Done)]
    [InlineData(1, DayStatus.Failed)]
    [InlineData(3, DayStatus.Failed)]
    public void Quit_ZeroIsKept(int value, DayStatus expected)
    {
        var habit = NewHabit(HabitKind.Quit);

        DayLogRules.DeriveStatus(habit, value).Should().Be(expected);
    }

    [Fact]
    public void Normalize_ClampsAndCleansFields()
    {
        var habit = NewHabit(HabitKind.Count, target: 3);
        var log = new DayLog
        {
            HabitId = habit.Id,
            Day = Today,
            Value = -4,
            Status = DayStatus.Done, // sai: sẽ bị suy ra lại
            Note = "   " + new string('x', 800) + "   ",
            Mood = 9
        };

        var normalized = DayLogRules.Normalize(habit, log);

        normalized.Value.Should().Be(0);
        normalized.Status.Should().Be(DayStatus.None);
        normalized.Note.Should().HaveLength(DayLog.MaxNoteLength);
        normalized.Mood.Should().BeNull();
    }

    [Fact]
    public void Normalize_KeepsSkippedAndFrozen()
    {
        var habit = NewHabit();

        DayLogRules.Normalize(habit, WithStatus(habit, Today, DayStatus.Skipped)).Status.Should().Be(DayStatus.Skipped);
        DayLogRules.Normalize(habit, WithStatus(habit, Today, DayStatus.Frozen)).Status.Should().Be(DayStatus.Frozen);
    }

    [Fact]
    public void Normalize_BlankNoteBecomesNull()
    {
        var habit = NewHabit();
        var log = Done(habit, Today) with { Note = "  \t " };

        DayLogRules.Normalize(habit, log).Note.Should().BeNull();
    }

    [Fact]
    public void Normalize_IsIdempotent()
    {
        var random = new Random(7);

        for (var i = 0; i < 500; i++)
        {
            var kind = (HabitKind)random.Next(4);
            var habit = NewHabit(kind, target: random.Next(1, 20));
            var log = new DayLog
            {
                HabitId = habit.Id,
                Day = Today,
                Value = random.Next(-5, 40),
                Status = (DayStatus)random.Next(6),
                Mood = random.Next(-1, 8),
                Note = random.Next(3) == 0 ? new string('a', random.Next(0, 700)) : null
            };

            var once = DayLogRules.Normalize(habit, log);
            var twice = DayLogRules.Normalize(habit, once);

            twice.Should().Be(once, $"case {i}");
        }
    }

    [Fact]
    public void CanSkip_AllowsTwoWithinSevenDays_RejectsThird()
    {
        var habit = NewHabit();
        var logs = new List<DayLog>
        {
            WithStatus(habit, Today.AddDays(-1), DayStatus.Skipped),
            WithStatus(habit, Today.AddDays(-3), DayStatus.Skipped)
        };

        DayLogRules.CanSkip(logs, Today).Should().BeFalse();
        DayLogRules.CanSkip(logs, Today.AddDays(-8)).Should().BeTrue("ngoài cửa sổ 7 ngày của hai lần đã bỏ qua");
        DayLogRules.CanSkip([logs[0]], Today).Should().BeTrue();
    }

    [Fact]
    public void CanSkip_ReSkippingSameDayIsAllowed()
    {
        var habit = NewHabit();
        var logs = new List<DayLog>
        {
            WithStatus(habit, Today, DayStatus.Skipped),
            WithStatus(habit, Today.AddDays(-2), DayStatus.Skipped)
        };

        DayLogRules.CanSkip(logs, Today).Should().BeTrue();
    }

    // ---- Validator ----

    [Fact]
    public void Validator_ValidHabit_HasNoErrors()
    {
        HabitValidator.Validate(NewHabit()).Should().BeEmpty();
    }

    [Theory]
    [InlineData("", "name.required")]
    [InlineData("   ", "name.required")]
    public void Validator_RejectsBlankName(string name, string code)
    {
        var habit = NewHabit();
        habit.Name = name;

        HabitValidator.Validate(habit).Select(e => e.Code).Should().Contain(code);
    }

    [Fact]
    public void Validator_RejectsLongName()
    {
        var habit = NewHabit();
        habit.Name = new string('a', 41);

        HabitValidator.Validate(habit).Select(e => e.Code).Should().Contain("name.too_long");
    }

    [Fact]
    public void Validator_CountNeedsPositiveTarget()
    {
        var habit = NewHabit(HabitKind.Count, target: 0);

        HabitValidator.Validate(habit).Select(e => e.Code).Should().Contain("target.range");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public void Validator_TimesPerWeekRange(int times)
    {
        var habit = NewHabit(frequency: new HabitFrequency.TimesPerWeek(times));

        HabitValidator.Validate(habit).Select(e => e.Code).Should().Contain("frequency.times.range");
    }

    [Fact]
    public void Validator_TimesPerMonthRange_And_IntervalRange()
    {
        HabitValidator.Validate(NewHabit(frequency: new HabitFrequency.TimesPerMonth(28)))
            .Select(e => e.Code).Should().Contain("frequency.times.range");
        HabitValidator.Validate(NewHabit(frequency: new HabitFrequency.EveryNDays(1, Today)))
            .Select(e => e.Code).Should().Contain("frequency.interval.range");
        HabitValidator.Validate(NewHabit(frequency: new HabitFrequency.EveryNDays(31, Today)))
            .Select(e => e.Code).Should().Contain("frequency.interval.range");
    }

    [Fact]
    public void Validator_RejectsEmptyWeekDays_BadColor_TooManyTags()
    {
        var habit = NewHabit(frequency: new HabitFrequency.WeekDays(HabitGrid.Core.Models.DayOfWeekMask.None));
        habit.ColorHue = "green";
        habit.Tags = ["a", "b", "c", "d", "e", "f"];

        var codes = HabitValidator.Validate(habit).Select(e => e.Code).ToList();

        codes.Should().Contain(["frequency.days.empty", "color.invalid", "tags.too_many"]);
    }

    [Fact]
    public void Icon_AcceptsV1AndV2Formats()
    {
        new HabitIcon("Drop24Regular").Value.Should().Be("fluent:Drop24Regular");
        new HabitIcon("emoji:📚").IsEmoji.Should().BeTrue();
        HabitIcon.Emoji("📚").Key.Should().Be("📚");
        default(HabitIcon).Value.Should().Be(HabitIcon.DefaultValue);
        new HabitIcon("").Value.Should().Be(HabitIcon.DefaultValue);
    }

    [Fact]
    public void NewHabit_GetsTimeOrderedIds()
    {
        var a = new Habit();
        var b = new Habit();

        a.Id.Should().NotBe(b.Id);
        a.Id.Version.Should().Be(7);
    }
}
