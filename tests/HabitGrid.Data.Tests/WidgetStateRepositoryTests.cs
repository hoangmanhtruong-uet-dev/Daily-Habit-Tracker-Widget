using FluentAssertions;
using HabitGrid.Core.Models;
using Xunit;

namespace HabitGrid.Data.Tests;

/// <summary>
/// Bộ kiểm thử lưu trạng thái widget (vị trí tương đối, cỡ, ẩn/hiện).
/// </summary>
public class WidgetStateRepositoryTests : DatabaseTestBase
{
    [Fact]
    public async Task UpsertAsync_ThenGetAsync_ShouldRoundTripAllFields()
    {
        var habit = await CreateHabitAsync();
        var state = new WidgetState
        {
            HabitId = habit.Id,
            Monitor = @"\\.\DISPLAY2",
            X = 0.25,
            Y = 0.75,
            Size = WidgetSize.Large,
            Visible = true
        };

        await WidgetStates.UpsertAsync(state);

        var loaded = await WidgetStates.GetAsync(habit.Id);

        loaded.Should().NotBeNull();
        loaded!.Monitor.Should().Be(@"\\.\DISPLAY2");
        loaded.X.Should().Be(0.25);
        loaded.Y.Should().Be(0.75);
        loaded.Size.Should().Be(WidgetSize.Large);
        loaded.Visible.Should().BeTrue();
    }

    [Fact]
    public async Task UpsertAsync_CalledTwice_ShouldOverwrite()
    {
        var habit = await CreateHabitAsync();

        await WidgetStates.UpsertAsync(new WidgetState { HabitId = habit.Id, X = 0.1, Y = 0.2 });
        await WidgetStates.UpsertAsync(new WidgetState { HabitId = habit.Id, X = 0.9, Y = 0.8, Size = WidgetSize.Small });

        var loaded = await WidgetStates.GetAsync(habit.Id);

        loaded!.X.Should().Be(0.9);
        loaded.Y.Should().Be(0.8);
        loaded.Size.Should().Be(WidgetSize.Small);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnEveryState()
    {
        var first = await CreateHabitAsync();
        var second = await CreateHabitAsync();

        await WidgetStates.UpsertAsync(new WidgetState { HabitId = first.Id });
        await WidgetStates.UpsertAsync(new WidgetState { HabitId = second.Id });

        (await WidgetStates.GetAllAsync()).Should().HaveCount(2);
    }

    [Fact]
    public async Task SetVisibleAsync_ShouldToggleVisibility()
    {
        var habit = await CreateHabitAsync();
        await WidgetStates.UpsertAsync(new WidgetState { HabitId = habit.Id, Visible = true });

        await WidgetStates.SetVisibleAsync(habit.Id, false);

        (await WidgetStates.GetAsync(habit.Id))!.Visible.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveState()
    {
        var habit = await CreateHabitAsync();
        await WidgetStates.UpsertAsync(new WidgetState { HabitId = habit.Id });

        await WidgetStates.DeleteAsync(habit.Id);

        (await WidgetStates.GetAsync(habit.Id)).Should().BeNull();
    }

    [Fact]
    public async Task HardDeleteHabit_ShouldCascadeDeleteWidgetState()
    {
        var habit = await CreateHabitAsync();
        await WidgetStates.UpsertAsync(new WidgetState { HabitId = habit.Id });

        await Habits.HardDeleteAsync(habit.Id);

        (await WidgetStates.GetAsync(habit.Id)).Should().BeNull("widget_state phải bị xóa theo ON DELETE CASCADE");
    }

    [Fact]
    public async Task GetAsync_WithUnknownHabit_ShouldReturnNull()
    {
        (await WidgetStates.GetAsync(Guid.NewGuid())).Should().BeNull();
    }

    private async Task<Habit> CreateHabitAsync()
    {
        var habit = new Habit { Name = "Thói quen", StartDate = new DateOnly(2026, 10, 1) };
        await Habits.InsertAsync(habit);
        return habit;
    }
}
