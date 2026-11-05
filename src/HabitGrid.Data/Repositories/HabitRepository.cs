using HabitGrid.Core.Models;
using HabitGrid.Core.Repositories;
using Microsoft.Data.Sqlite;

namespace HabitGrid.Data.Repositories;

/// <summary>
/// Hiện thực <see cref="IHabitRepository"/> trên SQLite.
/// Mọi thao tác ghi đều được bọc trong <see cref="SqliteTransaction"/>.
/// </summary>
public sealed class HabitRepository : IHabitRepository
{
    private const string SelectColumns = """
        id, name, icon, kind, target, schedule_mask, palette_id, custom_color,
        start_date, reminder_time, reminder_mask, sort_order,
        archived_at, deleted_at, created_at, read_only
        """;

    private readonly HabitGridDatabase _database;

    public HabitRepository(HabitGridDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _database = database;
    }

    public async Task<IReadOnlyList<Habit>> GetAllActiveAsync()
    {
        await using var connection = await _database.OpenConnectionAsync();
        using var command = connection.CreateCommand();

        command.CommandText = $"""
            SELECT {SelectColumns} FROM habits
            WHERE deleted_at IS NULL AND archived_at IS NULL
            ORDER BY sort_order, created_at;
            """;

        return await ReadHabitsAsync(command);
    }

    public async Task<IReadOnlyList<Habit>> GetAllIncludingArchivedAsync()
    {
        await using var connection = await _database.OpenConnectionAsync();
        using var command = connection.CreateCommand();

        command.CommandText = $"""
            SELECT {SelectColumns} FROM habits
            WHERE deleted_at IS NULL
            ORDER BY sort_order, created_at;
            """;

        return await ReadHabitsAsync(command);
    }

    public async Task<Habit?> GetByIdAsync(Guid id)
    {
        await using var connection = await _database.OpenConnectionAsync();
        using var command = connection.CreateCommand();

        command.CommandText = $"SELECT {SelectColumns} FROM habits WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id.ToString("D"));

        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? SqlMapping.ToHabit(reader) : null;
    }

    public async Task InsertAsync(Habit habit)
    {
        ArgumentNullException.ThrowIfNull(habit);

        await using var connection = await _database.OpenConnectionAsync();
        await using var transaction = connection.BeginTransaction();

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO habits (
                id, name, icon, kind, target, schedule_mask, palette_id, custom_color,
                start_date, reminder_time, reminder_mask, sort_order,
                archived_at, deleted_at, created_at, read_only)
            VALUES (
                $id, $name, $icon, $kind, $target, $scheduleMask, $paletteId, $customColor,
                $startDate, $reminderTime, $reminderMask, $sortOrder,
                $archivedAt, $deletedAt, $createdAt, $readOnly);
            """;

        BindHabitParameters(command, habit);
        await command.ExecuteNonQueryAsync();

        transaction.Commit();
    }

    public async Task UpdateAsync(Habit habit)
    {
        ArgumentNullException.ThrowIfNull(habit);

        await using var connection = await _database.OpenConnectionAsync();
        await using var transaction = connection.BeginTransaction();

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE habits SET
                name = $name,
                icon = $icon,
                kind = $kind,
                target = $target,
                schedule_mask = $scheduleMask,
                palette_id = $paletteId,
                custom_color = $customColor,
                start_date = $startDate,
                reminder_time = $reminderTime,
                reminder_mask = $reminderMask,
                sort_order = $sortOrder,
                archived_at = $archivedAt,
                deleted_at = $deletedAt,
                read_only = $readOnly
            WHERE id = $id;
            """;

        BindHabitParameters(command, habit);
        await command.ExecuteNonQueryAsync();

        transaction.Commit();
    }

    public async Task SoftDeleteAsync(Guid id)
    {
        await using var connection = await _database.OpenConnectionAsync();
        await using var transaction = connection.BeginTransaction();

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE habits SET deleted_at = $deletedAt WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        command.Parameters.AddWithValue("$deletedAt", SqlMapping.ToSqlTimestamp(DateTime.UtcNow));

        await command.ExecuteNonQueryAsync();

        transaction.Commit();
    }

    public async Task HardDeleteAsync(Guid id)
    {
        await using var connection = await _database.OpenConnectionAsync();
        await using var transaction = connection.BeginTransaction();

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM habits WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id.ToString("D"));

        await command.ExecuteNonQueryAsync();

        transaction.Commit();
    }

    public async Task ArchiveAsync(Guid id)
    {
        await using var connection = await _database.OpenConnectionAsync();
        await using var transaction = connection.BeginTransaction();

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE habits SET archived_at = $archivedAt WHERE id = $id AND archived_at IS NULL;";
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        command.Parameters.AddWithValue("$archivedAt", SqlMapping.ToSqlTimestamp(DateTime.UtcNow));

        await command.ExecuteNonQueryAsync();

        transaction.Commit();
    }

    public async Task UnarchiveAsync(Guid id)
    {
        await using var connection = await _database.OpenConnectionAsync();
        await using var transaction = connection.BeginTransaction();

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE habits SET archived_at = NULL WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id.ToString("D"));

        await command.ExecuteNonQueryAsync();

        transaction.Commit();
    }

    public async Task UpdateSortOrdersAsync(IReadOnlyList<(Guid HabitId, int SortOrder)> orders)
    {
        ArgumentNullException.ThrowIfNull(orders);

        if (orders.Count == 0)
        {
            return;
        }

        await using var connection = await _database.OpenConnectionAsync();
        await using var transaction = connection.BeginTransaction();

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE habits SET sort_order = $sortOrder WHERE id = $id;";

        foreach (var (habitId, sortOrder) in orders)
        {
            command.Parameters.Clear();
            command.Parameters.AddWithValue("$id", habitId.ToString("D"));
            command.Parameters.AddWithValue("$sortOrder", sortOrder);

            await command.ExecuteNonQueryAsync();
        }

        transaction.Commit();
    }

    public async Task<int> CountNonDeletedAsync()
    {
        await using var connection = await _database.OpenConnectionAsync();
        using var command = connection.CreateCommand();

        command.CommandText = "SELECT COUNT(*) FROM habits WHERE deleted_at IS NULL;";

        var result = await command.ExecuteScalarAsync();
        return result is null or DBNull ? 0 : Convert.ToInt32(result);
    }

    private static void BindHabitParameters(SqliteCommand command, Habit habit)
    {
        command.Parameters.AddWithValue("$id", habit.Id.ToString("D"));
        command.Parameters.AddWithValue("$name", habit.Name);
        command.Parameters.AddWithValue("$icon", habit.Icon);
        command.Parameters.AddWithValue("$kind", (int)habit.Kind);
        command.Parameters.AddWithValue("$target", habit.Target);
        command.Parameters.AddWithValue("$scheduleMask", (int)habit.ScheduleMask);
        command.Parameters.AddWithValue("$paletteId", habit.PaletteId);
        command.Parameters.AddWithValue("$customColor", (object?)habit.CustomColorHex ?? DBNull.Value);
        command.Parameters.AddWithValue("$startDate", SqlMapping.ToSqlDate(habit.StartDate));
        command.Parameters.AddWithValue("$reminderTime", (object?)SqlMapping.ToSqlTime(habit.ReminderTime) ?? DBNull.Value);
        command.Parameters.AddWithValue("$reminderMask", (int)habit.ReminderMask);
        command.Parameters.AddWithValue("$sortOrder", habit.SortOrder);
        command.Parameters.AddWithValue("$archivedAt", (object?)SqlMapping.ToSqlTimestamp(habit.ArchivedAt) ?? DBNull.Value);
        command.Parameters.AddWithValue("$deletedAt", (object?)SqlMapping.ToSqlTimestamp(habit.DeletedAt) ?? DBNull.Value);
        command.Parameters.AddWithValue("$createdAt", SqlMapping.ToSqlTimestamp(habit.CreatedAt));
        command.Parameters.AddWithValue("$readOnly", habit.IsReadOnly ? 1 : 0);
    }

    private static async Task<IReadOnlyList<Habit>> ReadHabitsAsync(SqliteCommand command)
    {
        var habits = new List<Habit>();

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            habits.Add(SqlMapping.ToHabit(reader));
        }

        return habits;
    }
}
