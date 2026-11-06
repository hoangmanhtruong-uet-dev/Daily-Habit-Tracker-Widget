using FluentAssertions;
using HabitGrid.Core.Domain;
using HabitGrid.Core.Domain.Monetization;
using HabitGrid.Core.Domain.Moments;
using HabitGrid.Presentation.Services;

namespace HabitGrid.Presentation.Tests;

public sealed class MomentsTests : IDisposable
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46, 0x49, 0x46, 0, 1];
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];
    private readonly string _root = Path.Combine(Path.GetTempPath(), "hg-moments-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private MomentService Service(AppFixture f, bool plus, Func<DateTime>? now = null)
    {
        var live = new LiveEntitlements();
        live.Set(plus);
        return new MomentService(new MomentStore(_root), live, f.Days, now);
    }

    [Fact]
    public async Task Free_OnePhotoPerHabitPerDay_PlusUnlimited()
    {
        using var f = new AppFixture();
        var h = await f.AddAsync("A", startDaysAgo: 5);
        var free = Service(f, plus: false);

        free.Add(h, AppFixture.Today, Jpeg, ".jpg").Result.Should().Be(MomentAddResult.Added);
        free.Add(h, AppFixture.Today, Jpeg, ".jpg").Result.Should().Be(MomentAddResult.LimitReached);
        free.Add(h, AppFixture.Today.AddDays(-1), Png, ".png").Result.Should().Be(MomentAddResult.Added, "giới hạn tính theo ngày");

        var plus = Service(f, plus: true);
        plus.Add(h, AppFixture.Today, Jpeg, ".jpg").Result.Should().Be(MomentAddResult.Added);
        plus.Add(h, AppFixture.Today, Jpeg, ".jpg").Result.Should().Be(MomentAddResult.Added);
        plus.Store.ForDay(h.Id, AppFixture.Today).Should().HaveCount(3);
    }

    [Fact]
    public async Task RejectsNonImages_FutureDays_AndBeforeStart()
    {
        using var f = new AppFixture();
        var h = await f.AddAsync("A", startDaysAgo: 2);
        var service = Service(f, plus: true);

        service.Add(h, AppFixture.Today, "MZ fake exe..."u8.ToArray(), ".jpg").Result.Should().Be(MomentAddResult.InvalidImage);
        service.Add(h, AppFixture.Today, [], ".jpg").Result.Should().Be(MomentAddResult.InvalidImage);
        service.Add(h, AppFixture.Today.AddDays(1), Jpeg, ".jpg").Result.Should().Be(MomentAddResult.NotAllowed);
        service.Add(h, AppFixture.Today.AddDays(-10), Jpeg, ".jpg").Result.Should().Be(MomentAddResult.NotAllowed);
        Directory.Exists(_root).Should().BeFalse("không có gì được ghi");
    }

    [Fact]
    public async Task ListLatestAndOneMonthAgo()
    {
        using var f = new AppFixture();
        var a = await f.AddAsync("A", startDaysAgo: 60);
        var b = await f.AddAsync("B", startDaysAgo: 60);
        var t = new DateTime(2026, 10, 22, 8, 0, 0, DateTimeKind.Utc);
        var service = Service(f, plus: true, () => t);

        service.Add(a, AppFixture.Today.AddMonths(-1), Jpeg, ".jpg");
        t = t.AddMinutes(1);
        service.Add(b, AppFixture.Today, Png, ".png");

        service.Store.ForHabit(a.Id).Should().ContainSingle();
        service.Latest()!.HabitId.Should().Be(b.Id);
        service.Latest([a.Id])!.HabitId.Should().Be(a.Id, "chỉ xét thói quen đang chạy");
        service.OneMonthAgo(AppFixture.Today)!.HabitId.Should().Be(a.Id);
        service.OneMonthAgo(AppFixture.Today.AddDays(10)).Should().BeNull();
    }

    [Fact]
    public async Task Delete_OnlyRemovesFilesInsideTheStore()
    {
        using var f = new AppFixture();
        var h = await f.AddAsync("A", startDaysAgo: 5);
        var service = Service(f, plus: true);
        var moment = service.Add(h, AppFixture.Today, Jpeg, ".jpg").Moment!;
        var outside = Path.Combine(Path.GetTempPath(), "hg-outside-" + Guid.NewGuid().ToString("N") + ".jpg");
        File.WriteAllBytes(outside, Jpeg);

        try
        {
            service.Store.Delete(moment with { Path = outside }).Should().BeFalse();
            File.Exists(outside).Should().BeTrue();
            service.Store.Delete(moment).Should().BeTrue();
            service.Store.ForHabit(h.Id).Should().BeEmpty();
        }
        finally
        {
            File.Delete(outside);
        }
    }

    [Fact]
    public async Task ShareCard_FreeGetsFreeFramesAndWatermark_PlusGetsEverything()
    {
        using var f = new AppFixture();
        var h = await f.AddAsync("Đọc sách", startDaysAgo: 30);
        for (var i = 0; i < 9; i++) await f.LogAsync(h, AppFixture.Today.AddDays(-i), 1);
        var freeLive = new LiveEntitlements();
        var plusLive = new LiveEntitlements();
        plusLive.Set(true);
        var store = new MomentStore(_root);
        new MomentService(store, plusLive, f.Days).Add(h, AppFixture.Today, Jpeg, ".jpg");

        var free = await new ShareCardService(f.Habits, f.Logs, f.Profile, freeLive, f.Days, store).BuildAsync(h.Id, "midnight", hideWatermark: true);
        var plus = await new ShareCardService(f.Habits, f.Logs, f.Profile, plusLive, f.Days, store).BuildAsync(h.Id, "midnight", hideWatermark: true);

        free!.Frame.Should().Be(ShareFrames.Default, "khung Nửa đêm thuộc Plus");
        free.ShowWatermark.Should().BeTrue();
        plus!.Frame.Id.Should().Be("midnight");
        plus.ShowWatermark.Should().BeFalse();
        plus.Streak.Should().Be(9);
        plus.Heatmap.Weeks.Should().HaveCount(ShareCardService.HeatmapWeeks);
        plus.PhotoPath.Should().NotBeNull("lấy ảnh khoảnh khắc mới nhất");
        plus.HabitName.Should().Be("Đọc sách");
    }

    [Fact]
    public void Frames_TwoFree_SixTotal_UniqueIds()
    {
        ShareFrames.All.Should().HaveCount(6);
        ShareFrames.All.Count(f => f.IsFree).Should().Be(2);
        ShareFrames.All.Select(f => f.Id).Should().OnlyHaveUniqueItems();
        ShareFrames.Find("nope").Should().Be(ShareFrames.Default);
    }

    [Theory]
    [InlineData("vi")]
    [InlineData("en")]
    public void EveryFrame_HasAName_InBothLanguages(string language)
    {
        using var f = new AppFixture();
        f.Localizer.SetLanguage(language);

        foreach (var frame in ShareFrames.All)
        {
            f.Localizer[frame.NameKey].Should().NotStartWith("[");
        }
    }
}

public sealed class StripLayoutTests : IDisposable
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46, 0x49, 0x46, 0, 1];
    private readonly string _root = Path.Combine(Path.GetTempPath(), "hg-strip-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static LiveEntitlements Live(bool plus)
    {
        var live = new LiveEntitlements();
        live.Set(plus);
        return live;
    }

    private async Task<(AppFixture F, HabitGrid.Core.Domain.Habit H, MomentStore Store)> SeedAsync(int photoDays, int perDay = 1)
    {
        var f = new AppFixture();
        var h = await f.AddAsync("A", startDaysAgo: 20);
        var store = new MomentStore(_root);
        var service = new MomentService(store, Live(true), f.Days, () => DateTime.UtcNow);

        for (var i = 0; i < photoDays; i++)
        {
            for (var k = 0; k < perDay; k++)
            {
                service.Add(h, AppFixture.Today.AddDays(-i), Jpeg, ".jpg");
            }
        }

        return (f, h, store);
    }

    [Fact]
    public async Task PickStrip_TakesOnePhotoPerDay_UpToFour_NewestDaysFirst()
    {
        var (f, h, store) = await SeedAsync(photoDays: 6, perDay: 2);
        using var _ = f;

        var picked = ShareLayouts.PickStrip(store.ForHabit(h.Id));

        picked.Should().HaveCount(4);
        picked.Select(m => m.Day).Should().Equal(Enumerable.Range(0, 4).Select(i => AppFixture.Today.AddDays(-i)));
    }

    [Fact]
    public async Task Plus_WithEnoughPhotos_GetsTheStrip()
    {
        var (f, h, store) = await SeedAsync(photoDays: 4);
        using var _ = f;

        var card = await new ShareCardService(f.Habits, f.Logs, f.Profile, Live(true), f.Days, store).BuildAsync(h.Id, layout: ShareLayout.Strip4);

        card!.Layout.Should().Be(ShareLayout.Strip4);
        card.StripPhotos.Should().HaveCount(4);
    }

    [Fact]
    public async Task Free_AlwaysGetsASinglePhoto_EvenWhenAskingForTheStrip()
    {
        var (f, h, store) = await SeedAsync(photoDays: 4);
        using var _ = f;

        var card = await new ShareCardService(f.Habits, f.Logs, f.Profile, Live(false), f.Days, store).BuildAsync(h.Id, layout: ShareLayout.Strip4);

        card!.Layout.Should().Be(ShareLayout.Single);
        card.StripPhotos.Should().BeNull();
    }

    [Fact]
    public async Task TooFewPhotos_FallsBackToASingleLayout()
    {
        var (f, h, store) = await SeedAsync(photoDays: 1);
        using var _ = f;

        var card = await new ShareCardService(f.Habits, f.Logs, f.Profile, Live(true), f.Days, store).BuildAsync(h.Id, layout: ShareLayout.Strip4);

        card!.Layout.Should().Be(ShareLayout.Single);
    }

    [Fact]
    public async Task TwoOrThreePhotos_StillMakeAStrip_WithEmptySlots()
    {
        var (f, h, store) = await SeedAsync(photoDays: 3);
        using var _ = f;

        var card = await new ShareCardService(f.Habits, f.Logs, f.Profile, Live(true), f.Days, store).BuildAsync(h.Id, layout: ShareLayout.Strip4);

        card!.StripPhotos.Should().HaveCount(3);
    }

    [Theory]
    [InlineData(ShareLayout.Single, false, true)]
    [InlineData(ShareLayout.Strip4, false, false)]
    [InlineData(ShareLayout.Strip4, true, true)]
    public void CanUse_FollowsThePlan(ShareLayout layout, bool plus, bool expected) =>
        ShareLayouts.CanUse(layout, plus).Should().Be(expected);
}
