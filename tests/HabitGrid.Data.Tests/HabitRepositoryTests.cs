using FluentAssertions;
using HabitGrid.Core.Models;
using Xunit;

namespace HabitGrid.Data.Tests;

/// <summary>
/// Bộ kiểm thử CRUD thói quen theo SPEC-02 mục 5.2.
/// </summary>
public class HabitRepositoryTests : DatabaseTestBase
{
    [Fact]
    public async Task InsertAsync_ThenGetById_ShouldReturnSameHabit()
    {
        var habit = CreateHabit(name: "Uống nước", sortOrder: 0);

        await Habits.InsertAsync(habit);

        var loaded = await Habits.GetByIdAsync(habit.Id);

        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("Uống nước");
        loaded.Kind.Should().Be(HabitKind.Boolean);
        loaded.Target.Should().Be(1);
        loaded.ScheduleMask.Should().Be(DayOfWeekMask.AllWeek);
        loaded.StartDate.Should().Be(habit.StartDate);
        loaded.CreatedAt.Should().BeCloseTo(habit.CreatedAt, TimeSpan.FromSeconds(1));
        loaded.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_ShouldPersistChangedFields()
    {
        var habit = CreateHabit(name: "Cũ");
        await Habits.InsertAsync(habit);

        habit.Name = "Mới";
        habit.Kind = HabitKind.Count;
        habit.Target = 8;
        habit.ScheduleMask = DayOfWeekMask.Monday | DayOfWeekMask.Wednesday;
        habit.ReminderMask = DayOfWeekMask.AllWeek;
        habit.ReminderTime = new TimeOnly(21, 30);
        habit.CustomColorHex = "#30A14E";

        await Habits.UpdateAsync(habit);

        var loaded = await Habits.GetByIdAsync(habit.Id);

        loaded!.Name.Should().Be("Mới");
        loaded.Kind.Should().Be(HabitKind.Count);
        loaded.Target.Should().Be(8);
        loaded.ScheduleMask.Should().Be(DayOfWeekMask.Monday | DayOfWeekMask.Wednesday);
        loaded.ReminderTime.Should().Be(new TimeOnly(21, 30));
        loaded.CustomColorHex.Should().Be("#30A14E");
    }

    [Fact]
    public async Task SoftDeleteAsync_ShouldHideHabitFromListsButKeepRow()
    {
        var habit = CreateHabit();
        await Habits.InsertAsync(habit);

        await Habits.SoftDeleteAsync(habit.Id);

        (await Habits.GetAllActiveAsync()).Should().BeEmpty();
        (await Habits.GetAllIncludingArchivedAsync()).Should().BeEmpty();
        (await Habits.CountNonDeletedAsync()).Should().Be(0);

        var loaded = await Habits.GetByIdAsync(habit.Id);
        loaded.Should().NotBeNull("dữ liệu phải được giữ lại để có thể hoàn tác");
        loaded!.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ArchiveAsync_ShouldMoveHabitOutOfActiveList()
    {
        var habit = CreateHabit();
        await Habits.InsertAsync(habit);

        await Habits.ArchiveAsync(habit.Id);

        (await Habits.GetAllActiveAsync()).Should().BeEmpty();
        (await Habits.GetAllIncludingArchivedAsync()).Should().HaveCount(1);

        var loaded = await Habits.GetByIdAsync(habit.Id);
        loaded!.ArchivedAt.Should().NotBeNull();
        loaded.IsArchived.Should().BeTrue();
        loaded.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task UnarchiveAsync_ShouldRestoreHabitToActiveList()
    {
        var habit = CreateHabit();
        await Habits.InsertAsync(habit);
        await Habits.ArchiveAsync(habit.Id);

        await Habits.UnarchiveAsync(habit.Id);

        (await Habits.GetAllActiveAsync()).Should().HaveCount(1);
        (await Habits.GetByIdAsync(habit.Id))!.ArchivedAt.Should().BeNull();
    }

    [Fact]
    public async Task HardDeleteAsync_ShouldRemoveHabitAndCascadeEntries()
    {
        var habit = CreateHabit();
        await Habits.InsertAsync(habit);
        await Entries.UpsertAsync(new Entry { HabitId = habit.Id, Day = new DateOnly(2026, 10, 21), Value = 1 });

        await Habits.HardDeleteAsync(habit.Id);

        (await Habits.GetByIdAsync(habit.Id)).Should().BeNull();
        (await Entries.GetAllForHabitAsync(habit.Id)).Should().BeEmpty("entries phải bị xóa theo ON DELETE CASCADE");
    }

    [Fact]
    public async Task GetAllActiveAsync_ShouldReturnHabitsOrderedBySortOrder()
    {
        var first = CreateHabit(name: "A", sortOrder: 2);
        var second = CreateHabit(name: "B", sortOrder: 0);
        var third = CreateHabit(name: "C", sortOrder: 1);

        await Habits.InsertAsync(first);
        await Habits.InsertAsync(second);
        await Habits.InsertAsync(third);

        var habits = await Habits.GetAllActiveAsync();

        habits.Select(h => h.Name).Should().ContainInOrder("B", "C", "A");
    }

    [Fact]
    public async Task UpdateSortOrdersAsync_ShouldPersistNewOrder()
    {
        var first = CreateHabit(name: "A", sortOrder: 0);
        var second = CreateHabit(name: "B", sortOrder: 1);
        await Habits.InsertAsync(first);
        await Habits.InsertAsync(second);

        await Habits.UpdateSortOrdersAsync(new[]
        {
            (first.Id, 1),
            (second.Id, 0)
        });

        var habits = await Habits.GetAllActiveAsync();
        habits.Select(h => h.Name).Should().ContainInOrder("B", "A");
    }

    [Fact]
    public async Task CountNonDeletedAsync_ShouldCountActiveAndArchivedOnly()
    {
        var active = CreateHabit(name: "Active");
        var archived = CreateHabit(name: "Archived");
        var deleted = CreateHabit(name: "Deleted");

        await Habits.InsertAsync(active);
        await Habits.InsertAsync(archived);
        await Habits.InsertAsync(deleted);

        await Habits.ArchiveAsync(archived.Id);
        await Habits.SoftDeleteAsync(deleted.Id);

        (await Habits.CountNonDeletedAsync()).Should().Be(2);
    }

    [Fact]
    public async Task GetByIdAsync_WithUnknownId_ShouldReturnNull()
    {
        (await Habits.GetByIdAsync(Guid.NewGuid())).Should().BeNull();
    }

    private static Habit CreateHabit(string name = "Thói quen", int sortOrder = 0)
    {
        return new Habit
        {
            Name = name,
            SortOrder = sortOrder,
            StartDate = new DateOnly(2026, 10, 1)
        };
    }
}
