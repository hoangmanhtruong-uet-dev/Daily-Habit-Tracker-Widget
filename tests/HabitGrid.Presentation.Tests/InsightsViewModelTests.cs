using FluentAssertions;
using HabitGrid.Core.Domain;
using HabitGrid.Core.Domain.Monetization;
using HabitGrid.Presentation.ViewModels;

namespace HabitGrid.Presentation.Tests;

public class InsightsViewModelTests
{
    private static InsightsViewModel Create(AppFixture f, bool plus)
    {
        var live = new LiveEntitlements();
        live.Set(plus);
        return new InsightsViewModel(f.Habits, f.Logs, f.Days, live, f.Settings);
    }

    [Fact]
    public async Task Free_Sees30Days_AndCannotUseLongerRangesOrExport()
    {
        using var f = new AppFixture();
        var h = await f.AddAsync("A", startDaysAgo: 200);
        for (var i = 0; i < 100; i++) await f.LogAsync(h, AppFixture.Today.AddDays(-i), 1);
        var vm = Create(f, plus: false);

        await vm.LoadAsync();

        vm.Report!.From.Should().Be(AppFixture.Today.AddDays(-29));
        vm.Report.CheckIns.Should().Be(30);
        (await vm.SetRangeAsync(InsightsRange.All)).Should().BeFalse();
        vm.Range.Should().Be(InsightsRange.Days30);
        (await vm.ExportCsvAsync()).Should().BeNull();
    }

    [Fact]
    public async Task Plus_SeesWholeHistory_AndExportsCsv()
    {
        using var f = new AppFixture();
        var h = await f.AddAsync("Đọc sách", startDaysAgo: 200);
        for (var i = 0; i < 100; i++) await f.LogAsync(h, AppFixture.Today.AddDays(-i), 1);
        var vm = Create(f, plus: true);

        (await vm.SetRangeAsync(InsightsRange.All)).Should().BeTrue();

        vm.Report!.CheckIns.Should().Be(100);
        vm.Report.From.Should().Be(AppFixture.Today.AddDays(-200));
        var csv = await vm.ExportCsvAsync();
        csv.Should().StartWith("date,habit,kind");
        csv!.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Should().HaveCount(101);
        csv.Should().Contain("Đọc sách");
    }

    [Fact]
    public async Task Insights_ShowAtMostThree_AndRotateAfterTheFirstDay()
    {
        using var f = new AppFixture();
        var h = await f.AddAsync("A", startDaysAgo: 60);
        // 5 ngày liên tiếp tới hôm qua → còn 2 ngày tới mốc 7.
        for (var i = 1; i <= 5; i++) await f.LogAsync(h, AppFixture.Today.AddDays(-i), 1);
        var vm = Create(f, plus: false);

        await vm.LoadAsync();
        vm.Insights.Should().HaveCountLessThanOrEqualTo(3).And.Contain(i => i.Key == "near_milestone");

        await vm.LoadAsync();
        vm.Insights.Should().Contain(i => i.Key == "near_milestone", "cùng ngày thì vẫn hiện");

        f.Days.Today = AppFixture.Today.AddDays(1);
        await f.LogAsync(h, AppFixture.Today, 1);
        await vm.LoadAsync();
        vm.Insights.Should().NotContain(i => i.Key == "near_milestone" && i.HabitId == h.Id, "đã hiện trong 7 ngày qua");
    }

    [Fact]
    public async Task NoHabits_GivesEmptyReport_NoError()
    {
        using var f = new AppFixture();
        var vm = Create(f, plus: false);

        await vm.LoadAsync();

        vm.ErrorMessage.Should().BeNull();
        vm.Report!.IsEmpty.Should().BeTrue();
        vm.Insights.Should().BeEmpty();
    }
}
