using FluentAssertions;
using HabitGrid.Data.V3;

namespace HabitGrid.Data.Tests.V3;

public class SettingsStoreTests
{
    [Fact]
    public async Task GetSetRemove_RoundTrip_AndOverwrite()
    {
        using var f = new V3Fixture();
        var store = new SettingsStore(f.Database);

        (await store.GetAsync("language")).Should().BeNull();

        await store.SetAsync("language", "vi");
        (await store.GetAsync("language")).Should().Be("vi");

        await store.SetAsync("language", "en");
        (await store.GetAsync("language")).Should().Be("en");
        f.Scalar("SELECT COUNT(*) FROM settings WHERE key = 'language';").Should().Be(1);

        await store.RemoveAsync("language");
        (await store.GetAsync("language")).Should().BeNull();
        await store.RemoveAsync("language"); // xóa lần hai không lỗi
    }

    [Fact]
    public async Task Settings_AreNeverQueuedForSync()
    {
        using var f = new V3Fixture();
        var store = new SettingsStore(f.Database);

        await store.SetAsync("onboarding.done", "1");

        f.OutboxCount.Should().Be(0, "cài đặt theo máy không đồng bộ");
    }

    [Fact]
    public async Task EmptyKey_IsRejected()
    {
        using var f = new V3Fixture();
        var store = new SettingsStore(f.Database);

        await FluentActions.Awaiting(() => store.SetAsync(" ", "x")).Should().ThrowAsync<ArgumentException>();
    }
}
