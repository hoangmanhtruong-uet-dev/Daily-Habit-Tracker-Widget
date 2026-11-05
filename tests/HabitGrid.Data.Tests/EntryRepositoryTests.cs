using FluentAssertions;
using HabitGrid.Core.Models;
using Xunit;

namespace HabitGrid.Data.Tests;

/// <summary>
/// Bộ kiểm thử nhật ký hoàn thành theo SPEC-02 mục 5.2.
/// </summary>
public class EntryRepositoryTests : DatabaseTestBase
{
    [Fact]
    public async Task UpsertAsync_NewEntry_ShouldInsert()
    {
        var habit = await CreateHabitAsync();

        await Entries.UpsertAsync(new Entry { HabitId = habit.Id, Day = new DateOnly(2026, 10, 21), Value = 1 });

        var entry = await Entries.GetAsync(habit.Id, new DateOnly(2026, 10, 21));

        entry.Should().NotBeNull();
        entry!.Value.Should().Be(1);
    }

    [Fact]
    public async Task UpsertAsync_ExistingEntry_ShouldUpdateValueAndUpdatedAt()
    {
        var habit = await CreateHabitAsync();
        var day = new DateOnly(2026, 10, 21);

        var first = new Entry { HabitId = habit.Id, Day = day, Value = 1, UpdatedAt = new DateTime(2026, 10, 21, 8, 0, 0, DateTimeKind.Utc) };
        await Entries.UpsertAsync(first);

        var second = new Entry { HabitId = habit.Id, Day = day, Value = 5, UpdatedAt = new DateTime(2026, 10, 22, 9, 30, 0, DateTimeKind.Utc) };
        await Entries.UpsertAsync(second);

        var entry = await Entries.GetAsync(habit.Id, day);

        entry.Should().NotBeNull();
        entry!.Value.Should().Be(5, "giá trị mới phải ghi đè giá trị cũ");
        entry.UpdatedAt.Should().Be(new DateTime(2026, 10, 22, 9, 30, 0, DateTimeKind.Utc), "updated_at phải được cập nhật");
        (await Entries.GetAllForHabitAsync(habit.Id)).Should().HaveCount(1, "upsert không được tạo bản ghi trùng");
    }

    [Fact]
    public async Task GetRangeAsync_ShouldReturnEntriesInRangeOrderedByDay()
    {
        var habit = await CreateHabitAsync();

        await Entries.UpsertAsync(new Entry { HabitId = habit.Id, Day = new DateOnly(2026, 10, 19), Value = 1 });
        await Entries.UpsertAsync(new Entry { HabitId = habit.Id, Day = new DateOnly(2026, 10, 21), Value = 1 });
        await Entries.UpsertAsync(new Entry { HabitId = habit.Id, Day = new DateOnly(2026, 10, 23), Value = 1 });

        var range = await Entries.GetRangeAsync(habit.Id, new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 22));

        range.Should().HaveCount(1);
        range[0].Day.Should().Be(new DateOnly(2026, 10, 21));
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveEntry()
    {
        var habit = await CreateHabitAsync();
        var day = new DateOnly(2026, 10, 21);
        await Entries.UpsertAsync(new Entry { HabitId = habit.Id, Day = day, Value = 1 });

        await Entries.DeleteAsync(habit.Id, day);

        (await Entries.GetAsync(habit.Id, day)).Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_WithUnknownDay_ShouldReturnNull()
    {
        var habit = await CreateHabitAsync();

        (await Entries.GetAsync(habit.Id, new DateOnly(2026, 10, 21))).Should().BeNull();
    }

    private async Task<Habit> CreateHabitAsync()
    {
        var habit = new Habit { Name = "Uống nước", StartDate = new DateOnly(2026, 10, 1) };
        await Habits.InsertAsync(habit);
        return habit;
    }
}
