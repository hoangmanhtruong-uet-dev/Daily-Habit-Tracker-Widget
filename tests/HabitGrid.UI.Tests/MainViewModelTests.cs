using FluentAssertions;
using HabitGrid.Core.Models;
using HabitGrid.Core.Repositories;
using HabitGrid.Core.Services;
using HabitGrid.Data;
using HabitGrid.Data.Repositories;
using HabitGrid.Platform.Licensing;
using HabitGrid.UI.Services;
using HabitGrid.UI.ViewModels;
using Xunit;

namespace HabitGrid.UI.Tests;

/// <summary>
/// Bộ kiểm thử ViewModel chính: rào chắn paywall, kéo-thả sắp xếp, xóa có hoàn tác.
/// </summary>
public class MainViewModelTests : IAsyncLifetime
{
    private HabitGridDatabase _database = null!;
    private IHabitRepository _habits = null!;
    private IEntryRepository _entries = null!;
    private IWidgetStateRepository _widgetStates = null!;
    private FakeDialogService _dialogs = null!;
    private FakeDeletionTimer _timer = null!;

    private static readonly DateOnly Today = new(2026, 10, 22);

    public async Task InitializeAsync()
    {
        _database = HabitGridDatabase.CreateInMemory();
        await _database.InitializeAsync();

        _habits = new HabitRepository(_database);
        _entries = new EntryRepository(_database);
        _widgetStates = new WidgetStateRepository(_database);
        _dialogs = new FakeDialogService();
        _timer = new FakeDeletionTimer();
    }

    public async Task DisposeAsync()
    {
        await _database.DisposeAsync();
    }

    private MainViewModel CreateViewModel(ILicenseService? license = null)
    {
        return new MainViewModel(
            _habits,
            _entries,
            _widgetStates,
            license ?? new FreeLicenseService(),
            new StreakCalculator(new ScheduleCalculator()),
            new FixedDayProvider(Today),
            _dialogs,
            new SafeDeletionManager(_habits, _timer));
    }

    [Fact]
    public async Task AddHabitCommand_WhenFreeLimitReached_ShouldShowPaywallInsteadOfEditDialog()
    {
        await SeedHabitsAsync(2);

        var viewModel = CreateViewModel();
        await viewModel.LoadAsync();

        await viewModel.AddHabitCommand.ExecuteAsync(null);

        _dialogs.PaywallShownCount.Should().Be(1, "không được báo lỗi thô thiển");
        _dialogs.EditDialogShownCount.Should().Be(0, "không được mở hộp thoại tạo habit khi đã hết hạn mức");
        _dialogs.LastPaywallReason.Should().Contain("2");
    }

    [Fact]
    public async Task AddHabitCommand_WhenUnderLimit_ShouldOpenEditDialog()
    {
        await SeedHabitsAsync(1);

        var viewModel = CreateViewModel();
        await viewModel.LoadAsync();

        _dialogs.EditResult = new HabitEditResult { Name = "Thói quen mới", IconKey = "Book24Regular" };

        await viewModel.AddHabitCommand.ExecuteAsync(null);

        _dialogs.PaywallShownCount.Should().Be(0);
        _dialogs.EditDialogShownCount.Should().Be(1);

        var all = await _habits.GetAllActiveAsync();
        all.Should().HaveCount(2);
        all.Should().Contain(h => h.Name == "Thói quen mới");
    }

    [Fact]
    public async Task AddHabitCommand_WhenPro_ShouldAllowBeyondLimit()
    {
        await SeedHabitsAsync(3);

        var viewModel = CreateViewModel(new AlwaysProLicense());
        await viewModel.LoadAsync();

        await viewModel.AddHabitCommand.ExecuteAsync(null);

        _dialogs.PaywallShownCount.Should().Be(0);
        _dialogs.EditDialogShownCount.Should().Be(1);
    }

    [Fact]
    public async Task MoveAsync_ShouldReorderListAndPersistSortOrder()
    {
        var first = await SeedHabitAsync("A", 0);
        var second = await SeedHabitAsync("B", 1);
        var third = await SeedHabitAsync("C", 2);

        var viewModel = CreateViewModel();
        await viewModel.LoadAsync();

        await viewModel.MoveAsync(0, 2);

        viewModel.Items.Select(i => i.Name).Should().ContainInOrder("B", "C", "A");

        var stored = await _habits.GetAllActiveAsync();
        stored.OrderBy(h => h.SortOrder).Select(h => h.Name).Should().ContainInOrder("B", "C", "A");
        stored.Single(h => h.Id == first.Id).SortOrder.Should().Be(2);
        stored.Single(h => h.Id == second.Id).SortOrder.Should().Be(0);
        stored.Single(h => h.Id == third.Id).SortOrder.Should().Be(1);
    }

    [Fact]
    public async Task MoveAsync_WithInvalidIndex_ShouldNotChangeAnything()
    {
        await SeedHabitAsync("A", 0);
        await SeedHabitAsync("B", 1);

        var viewModel = CreateViewModel();
        await viewModel.LoadAsync();

        await viewModel.MoveAsync(0, 0);

        viewModel.Items.Select(i => i.Name).Should().ContainInOrder("A", "B");
    }

    [Fact]
    public async Task DeleteHabitCommand_ShouldSoftDeleteAndAllowUndo()
    {
        var habit = await SeedHabitAsync("Xóa em", 0);

        var viewModel = CreateViewModel();
        await viewModel.LoadAsync();

        await viewModel.DeleteHabitCommand.ExecuteAsync(viewModel.Items[0]);

        // Đã xóa mềm: vẫn còn trong CSDL nhưng bị đánh dấu deleted_at.
        var stored = await _habits.GetByIdAsync(habit.Id);
        stored.Should().NotBeNull("xóa mềm phải giữ dữ liệu để hoàn tác");
        stored!.DeletedAt.Should().NotBeNull();

        viewModel.Items.Should().BeEmpty();

        // Hoàn tác.
        var manager = (SafeDeletionManager)typeof(MainViewModel)
            .GetField("_deletion", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(viewModel)!;

        await manager.UndoAsync();

        var restored = await _habits.GetByIdAsync(habit.Id);
        restored.Should().NotBeNull();
        restored!.DeletedAt.Should().BeNull();
    }

    [Fact]
    public async Task SafeDeletion_ShouldHardDeleteAfterUndoWindowExpires()
    {
        var habit = await SeedHabitAsync("Xóa hẳn", 0);

        var manager = new SafeDeletionManager(_habits, _timer);

        await manager.RequestDeleteAsync(habit);

        (await _habits.GetByIdAsync(habit.Id)).Should().NotBeNull();

        // Hết 10 giây -> xóa hẳn.
        _timer.Fire();
        await Task.Yield();

        (await _habits.GetByIdAsync(habit.Id)).Should().BeNull();
    }

    [Fact]
    public async Task SetWidgetVisibleAsync_ShouldPersistVisibility()
    {
        var habit = await SeedHabitAsync("Widget", 0);

        var viewModel = CreateViewModel();
        await viewModel.LoadAsync();

        await viewModel.SetWidgetVisibleAsync(habit.Id, false);

        var state = await _widgetStates.GetAsync(habit.Id);
        state.Should().NotBeNull();
        state!.Visible.Should().BeFalse();

        viewModel.Items[0].IsWidgetVisible.Should().BeFalse();
    }

    [Fact]
    public async Task ToggleEntryAsync_ShouldIncrementCountHabit()
    {
        var habit = new Habit
        {
            Name = "Uống nước",
            Kind = HabitKind.Count,
            Target = 8,
            StartDate = Today.AddDays(-10)
        };

        await _habits.InsertAsync(habit);

        var viewModel = CreateViewModel();
        await viewModel.LoadAsync();

        await viewModel.ToggleEntryAsync(habit.Id, Today);
        (await _entries.GetAsync(habit.Id, Today))!.Value.Should().Be(1);

        await viewModel.ToggleEntryAsync(habit.Id, Today);
        (await _entries.GetAsync(habit.Id, Today))!.Value.Should().Be(2);
    }

    [Fact]
    public async Task ToggleEntryAsync_WithBooleanHabit_ShouldToggle()
    {
        var habit = new Habit { Name = "Đọc sách", Kind = HabitKind.Boolean, StartDate = Today.AddDays(-10) };
        await _habits.InsertAsync(habit);

        var viewModel = CreateViewModel();
        await viewModel.LoadAsync();

        await viewModel.ToggleEntryAsync(habit.Id, Today);
        (await _entries.GetAsync(habit.Id, Today))!.Value.Should().Be(1);

        await viewModel.ToggleEntryAsync(habit.Id, Today);
        (await _entries.GetAsync(habit.Id, Today))!.Value.Should().Be(0);
    }

    [Fact]
    public async Task LoadAsync_ShouldPopulateListWithStreakAndWidgetVisibility()
    {
        var habit = await SeedHabitAsync("Chuỗi", 0);
        await _entries.UpsertAsync(new Entry { HabitId = habit.Id, Day = Today, Value = 1 });
        await _entries.UpsertAsync(new Entry { HabitId = habit.Id, Day = Today.AddDays(-1), Value = 1 });

        var viewModel = CreateViewModel();
        await viewModel.LoadAsync();

        viewModel.Items.Should().HaveCount(1);
        viewModel.Items[0].CurrentStreak.Should().Be(2);
        viewModel.IsEmpty.Should().BeFalse();
        viewModel.UsedCount.Should().Be(1);
        viewModel.SelectedItem.Should().NotBeNull();
        viewModel.Detail.Should().NotBeNull();
    }

    private async Task<Habit> SeedHabitAsync(string name, int sortOrder)
    {
        var habit = new Habit { Name = name, SortOrder = sortOrder, StartDate = Today.AddDays(-30) };
        await _habits.InsertAsync(habit);
        return habit;
    }

    private async Task SeedHabitsAsync(int count)
    {
        for (var index = 0; index < count; index++)
        {
            await SeedHabitAsync($"Thói quen {index + 1}", index);
        }
    }

    /// <summary>Nguồn thời gian cố định để kiểm thử không phụ thuộc đồng hồ thật.</summary>
    private sealed class FixedDayProvider : IDayProvider
    {
        public FixedDayProvider(DateOnly today) => Today = today;

        public DateOnly Today { get; }

        public TimeSpan DayStartOffset => TimeSpan.Zero;

        public DateOnly GetToday() => Today;

        public DateOnly ToLogicalDate(DateTime localDateTime) => DateOnly.FromDateTime(localDateTime);

        public DateTime GetNextDayTransitionUtc() => DateTime.UtcNow.AddHours(1);
    }

    /// <summary>Giả lập người dùng đã mua Pro.</summary>
    private sealed class AlwaysProLicense : FreeLicenseService
    {
        public AlwaysProLicense() : base(initialPro: true)
        {
        }
    }
}
