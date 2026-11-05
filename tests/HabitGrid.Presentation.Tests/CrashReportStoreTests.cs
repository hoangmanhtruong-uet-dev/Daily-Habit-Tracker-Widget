using FluentAssertions;
using HabitGrid.Presentation.Services;

namespace HabitGrid.Presentation.Tests;

public sealed class CrashReportStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "hg-crash-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void Write_CreatesAReportWithVersionOsWhereAndException()
    {
        var store = new CrashReportStore(_dir);

        var path = store.Write(new InvalidOperationException("boom"), "UnhandledException", "2.0.0-test", "line1\nline2");

        path.Should().NotBeNull();
        var text = File.ReadAllText(path!);
        text.Should().Contain("App: 2.0.0-test").And.Contain("Where: UnhandledException").And.Contain("boom").And.Contain("line2");
    }

    [Fact]
    public void Pending_OnlyReportsNewerThanTheAcknowledgement()
    {
        var store = new CrashReportStore(_dir);
        var t0 = new DateTime(2026, 11, 6, 8, 0, 0, DateTimeKind.Utc);
        store.Write(new Exception("old"), "a", "v", nowUtc: t0);
        store.Write(new Exception("new"), "b", "v", nowUtc: t0.AddMinutes(5));

        store.PendingSince(null).Should().HaveCount(2);
        var pending = store.PendingSince(t0.AddMinutes(1));
        pending.Should().ContainSingle().Which.Where.Should().Be("b");
        pending[0].Summary.Should().Contain("new");
    }

    [Fact]
    public void KeepsOnlyTheNewestReports()
    {
        var store = new CrashReportStore(_dir);
        var t0 = new DateTime(2026, 11, 6, 8, 0, 0, DateTimeKind.Utc);

        for (var i = 0; i < CrashReportStore.MaxReports + 5; i++)
        {
            store.Write(new Exception($"e{i}"), "x", "v", nowUtc: t0.AddSeconds(i));
        }

        Directory.GetFiles(_dir, "crash-*.txt").Should().HaveCount(CrashReportStore.MaxReports);
        store.PendingSince(null)[0].Summary.Should().Contain($"e{CrashReportStore.MaxReports + 4}");
    }

    [Fact]
    public void Write_NeverThrows_EvenWithAnUnusablePath()
    {
        var store = new CrashReportStore(Path.Combine(_dir, new string('x', 300), "y"));

        var act = () => store.Write(new Exception("x"), "x", "v");

        act.Should().NotThrow();
    }

    [Fact]
    public void Ack_RoundTrips()
    {
        var now = new DateTime(2026, 11, 6, 8, 30, 15, DateTimeKind.Utc);

        CrashReportStore.ParseAck(CrashReportStore.FormatAck(now)).Should().Be(now);
        CrashReportStore.ParseAck("garbage").Should().BeNull();
    }
}
