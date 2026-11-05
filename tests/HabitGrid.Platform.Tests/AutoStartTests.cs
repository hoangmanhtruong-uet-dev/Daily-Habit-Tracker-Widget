using FluentAssertions;
using HabitGrid.Platform.Desktop;
using Microsoft.Win32;
using Xunit;

namespace HabitGrid.Platform.Tests;

public sealed class AutoStartTests : IDisposable
{
    private const string TestKey = @"Software\HabitGridTests\Run";

    public AutoStartTests() => Registry.CurrentUser.DeleteSubKeyTree(@"Software\HabitGridTests", throwOnMissingSubKey: false);

    public void Dispose() => Registry.CurrentUser.DeleteSubKeyTree(@"Software\HabitGridTests", throwOnMissingSubKey: false);

    [Fact]
    public void Enable_WritesQuotedPath_AndDisableRemovesIt()
    {
        var auto = new AutoStart("Widgets", TestKey);
        auto.IsEnabled.Should().BeFalse();

        auto.Apply(true, @"C:\Program Files\HabitGrid\HabitGrid.WidgetHost.exe");

        auto.IsEnabled.Should().BeTrue();
        auto.CurrentCommand.Should().Be("\"C:\\Program Files\\HabitGrid\\HabitGrid.WidgetHost.exe\"");

        auto.Apply(false, string.Empty);
        auto.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Apply_IsIdempotent_AndUpdatesWhenExeMoves()
    {
        var auto = new AutoStart("Widgets", TestKey);

        auto.Apply(true, @"C:\a\host.exe");
        auto.Apply(true, @"C:\a\host.exe");
        auto.CurrentCommand.Should().Be("\"C:\\a\\host.exe\"");

        auto.Apply(true, @"D:\b\host.exe");
        auto.CurrentCommand.Should().Be("\"D:\\b\\host.exe\"");
    }

    [Fact]
    public void Disable_WhenNeverEnabled_DoesNotThrow()
    {
        var act = () => new AutoStart("Widgets", TestKey).Apply(false, string.Empty);

        act.Should().NotThrow();
    }

    [Fact]
    public void Quote_DoesNotDoubleQuote() =>
        AutoStart.Quote("\"C:\\x.exe\"").Should().Be("\"C:\\x.exe\"");
}
