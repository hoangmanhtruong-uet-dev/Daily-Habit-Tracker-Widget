using HabitGrid.Core.Models;
using HabitGrid.Core.Repositories;
using Microsoft.Data.Sqlite;

namespace HabitGrid.Data.Repositories;

/// <summary>
/// Hiện thực <see cref="IWidgetStateRepository"/> trên SQLite.
/// </summary>
public sealed class WidgetStateRepository : IWidgetStateRepository
{
    private const string SelectColumns = "habit_id, monitor, x, y, size, visible";

    private readonly HabitGridDatabase _database;

    public WidgetStateRepository(HabitGridDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _database = database;
    }

    public async Task<IReadOnlyList<WidgetState>> GetAllAsync()
    {
        await using var connection = await _database.OpenConnectionAsync();
        using var command = connection.CreateCommand();

        command.CommandText = $"SELECT {SelectColumns} FROM widget_state;";

        var states = new List<WidgetState>();

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            states.Add(ToWidgetState(reader));
        }

        return states;
    }

    public async Task<WidgetState?> GetAsync(Guid habitId)
    {
        await using var connection = await _database.OpenConnectionAsync();
        using var command = connection.CreateCommand();

        command.CommandText = $"SELECT {SelectColumns} FROM widget_state WHERE habit_id = $habitId;";
        command.Parameters.AddWithValue("$habitId", habitId.ToString("D"));

        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? ToWidgetState(reader) : null;
    }

    public async Task UpsertAsync(WidgetState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        await using var connection = await _database.OpenConnectionAsync();
        await using var transaction = connection.BeginTransaction();

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO widget_state (habit_id, monitor, x, y, size, visible)
            VALUES ($habitId, $monitor, $x, $y, $size, $visible)
            ON CONFLICT(habit_id) DO UPDATE SET
                monitor = excluded.monitor,
                x = excluded.x,
                y = excluded.y,
                size = excluded.size,
                visible = excluded.visible;
            """;

        command.Parameters.AddWithValue("$habitId", state.HabitId.ToString("D"));
        command.Parameters.AddWithValue("$monitor", state.Monitor);
        command.Parameters.AddWithValue("$x", state.X);
        command.Parameters.AddWithValue("$y", state.Y);
        command.Parameters.AddWithValue("$size", (int)state.Size);
        command.Parameters.AddWithValue("$visible", state.Visible ? 1 : 0);

        await command.ExecuteNonQueryAsync();

        transaction.Commit();
    }

    public async Task SetVisibleAsync(Guid habitId, bool visible)
    {
        await using var connection = await _database.OpenConnectionAsync();
        await using var transaction = connection.BeginTransaction();

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE widget_state SET visible = $visible WHERE habit_id = $habitId;";
        command.Parameters.AddWithValue("$habitId", habitId.ToString("D"));
        command.Parameters.AddWithValue("$visible", visible ? 1 : 0);

        await command.ExecuteNonQueryAsync();

        transaction.Commit();
    }

    public async Task DeleteAsync(Guid habitId)
    {
        await using var connection = await _database.OpenConnectionAsync();
        await using var transaction = connection.BeginTransaction();

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM widget_state WHERE habit_id = $habitId;";
        command.Parameters.AddWithValue("$habitId", habitId.ToString("D"));

        await command.ExecuteNonQueryAsync();

        transaction.Commit();
    }

    private static WidgetState ToWidgetState(SqliteDataReader reader)
    {
        return new WidgetState
        {
            HabitId = Guid.Parse(reader.GetString(0)),
            Monitor = reader.GetString(1),
            X = reader.GetDouble(2),
            Y = reader.GetDouble(3),
            Size = (WidgetSize)reader.GetInt32(4),
            Visible = reader.GetInt32(5) != 0
        };
    }
}
