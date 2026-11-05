using FluentAssertions;
using HabitGrid.Core.Domain.Reminders;
using HabitGrid.Presentation.Services;

namespace HabitGrid.Presentation.Tests;

public class CommandPaletteTests
{
    private static readonly PaletteCommand[] Commands =
    [
        new(PaletteKind.CheckHabit, "Check: Đọc sách", "1"),
        new(PaletteKind.Page, "Hôm nay", "today"),
        new(PaletteKind.Page, "Cài đặt", "settings"),
        new(PaletteKind.Action, "Thói quen mới", "new"),
        new(PaletteKind.OpenHabit, "Mở: Đọc sách", "1"),
        new(PaletteKind.Action, "Bắt đầu Focus", "focus")
    ];

    [Fact]
    public void EmptyQuery_ListsPagesAndActionsFirst()
    {
        var result = CommandPalette.Filter(Commands, "");

        result.Take(4).Should().OnlyContain(c => c.Kind == PaletteKind.Page || c.Kind == PaletteKind.Action);
    }

    [Fact]
    public void Query_IsDiacriticInsensitive_AndPrefersPrefixMatches()
    {
        var result = CommandPalette.Filter(Commands, "doc");

        result.Should().HaveCount(2).And.OnlyContain(c => c.Title.Contains("Đọc sách"));

        CommandPalette.Filter(Commands, "cai dat").Should().ContainSingle().Which.Argument.Should().Be("settings");
        CommandPalette.Filter(Commands, "focus")[0].Argument.Should().Be("focus");
    }

    [Fact]
    public void NoMatch_ReturnsEmpty() =>
        CommandPalette.Filter(Commands, "xyz").Should().BeEmpty();

    [Fact]
    public void ResultsAreCapped() =>
        CommandPalette.Filter(Enumerable.Range(0, 50).Select(i => new PaletteCommand(PaletteKind.OpenHabit, $"Habit {i}", $"{i}")), "habit")
            .Should().HaveCount(CommandPalette.MaxResults);
}

public class ReminderSettingsStoreTests
{
    [Fact]
    public async Task Defaults_WhenNothingSaved()
    {
        using var f = new AppFixture();

        var settings = await ReminderSettingsStore.LoadAsync(f.Settings);

        settings.Should().BeEquivalentTo(new ReminderSettings());
        (await ReminderSettingsStore.RemindersEnabledAsync(f.Settings)).Should().BeTrue();
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrips()
    {
        using var f = new AppFixture();
        var custom = new ReminderSettings
        {
            QuietHoursEnabled = false,
            QuietStart = new TimeOnly(22, 15),
            QuietEnd = new TimeOnly(6, 45),
            StreakRescue = false,
            MorningSummary = true,
            MorningSummaryAt = new TimeOnly(8, 5)
        };

        await ReminderSettingsStore.SaveAsync(f.Settings, custom);
        var loaded = await ReminderSettingsStore.LoadAsync(f.Settings);

        loaded.Should().BeEquivalentTo(custom);
    }

    [Fact]
    public void ParseTime_FallsBackOnGarbage() =>
        ReminderSettingsStore.ParseTime("25:99", new TimeOnly(7, 0)).Should().Be(new TimeOnly(7, 0));
}
