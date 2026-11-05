using HabitGrid.Core.Models;
using HabitGrid.Core.Repositories;
using Microsoft.Data.Sqlite;

namespace HabitGrid.Data.Repositories;

/// <summary>
/// Hiện thực <see cref="IEntryRepository"/> trên SQLite.
/// </summary>
public sealed class EntryRepository : IEntryRepository
{
    private readonly HabitGridDatabase _database;

    public EntryRepository(HabitGridDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _database = database;
    }

    public async Task<Entry?> GetAsync(Guid habitId, DateOnly day)
    {
        await using var connection = await _database.OpenConnectionAsync();
        using var command = connection.CreateCommand();

        command.CommandText = "SELECT habit_id, day, value, updated_at FROM entries WHERE habit_id = $habitId AND day = $day;";
        command.Parameters.AddWithValue("$habitId", habitId.ToString("D"));
        command.Parameters.AddWithValue("$day", SqlMapping.ToSqlDate(day));

        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? SqlMapping.ToEntry(reader) : null;
    }

    public async Task<IReadOnlyList<Entry>> GetRangeAsync(Guid habitId, DateOnly fromDay, DateOnly toDay)
    {
        await using var connection = await _database.OpenConnectionAsync();
        using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT habit_id, day, value, updated_at FROM entries
            WHERE habit_id = $habitId AND day >= $fromDay AND day <= $toDay
            ORDER BY day;
            """;
        command.Parameters.AddWithValue("$habitId", habitId.ToString("D"));
        command.Parameters.AddWithValue("$fromDay", SqlMapping.ToSqlDate(fromDay));
        command.Parameters.AddWithValue("$toDay", SqlMapping.ToSqlDate(toDay));

        return await ReadEntriesAsync(command);
    }

    public async Task<IReadOnlyList<Entry>> GetAllForHabitAsync(Guid habitId)
    {
        await using var connection = await _database.OpenConnectionAsync();
        using var command = connection.CreateCommand();

        command.CommandText = "SELECT habit_id, day, value, updated_at FROM entries WHERE habit_id = $habitId ORDER BY day;";
        command.Parameters.AddWithValue("$habitId", habitId.ToString("D"));

        return await ReadEntriesAsync(command);
    }

    public async Task UpsertAsync(Entry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        await using var connection = await _database.OpenConnectionAsync();
        await using var transaction = connection.BeginTransaction();

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO entries (habit_id, day, value, updated_at)
            VALUES ($habitId, $day, $value, $updatedAt)
            ON CONFLICT(habit_id, day) DO UPDATE SET
                value = excluded.value,
                updated_at = excluded.updated_at;
            """;

        command.Parameters.AddWithValue("$habitId", entry.HabitId.ToString("D"));
        command.Parameters.AddWithValue("$day", SqlMapping.ToSqlDate(entry.Day));
        command.Parameters.AddWithValue("$value", entry.Value);
        command.Parameters.AddWithValue("$updatedAt", SqlMapping.ToSqlTimestamp(entry.UpdatedAt));

        await command.ExecuteNonQueryAsync();

        transaction.Commit();
    }

    public async Task DeleteAsync(Guid habitId, DateOnly day)
    {
        await using var connection = await _database.OpenConnectionAsync();
        await using var transaction = connection.BeginTransaction();

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM entries WHERE habit_id = $habitId AND day = $day;";
        command.Parameters.AddWithValue("$habitId", habitId.ToString("D"));
        command.Parameters.AddWithValue("$day", SqlMapping.ToSqlDate(day));

        await command.ExecuteNonQueryAsync();

        transaction.Commit();
    }

    private static async Task<IReadOnlyList<Entry>> ReadEntriesAsync(SqliteCommand command)
    {
        var entries = new List<Entry>();

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            entries.Add(SqlMapping.ToEntry(reader));
        }

        return entries;
    }
}
