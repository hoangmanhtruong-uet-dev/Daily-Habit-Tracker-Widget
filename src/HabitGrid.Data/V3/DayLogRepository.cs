using HabitGrid.Core.Domain;
using Microsoft.Data.Sqlite;
using Sql = HabitGrid.Data.Repositories.SqlMapping;

namespace HabitGrid.Data.V3;

public sealed class DayLogRepository : RepositoryBase, IDayLogRepository
{
    internal const string Columns = "habit_id, day, value, status, note, mood, checked_at, updated_at, hlc";

    public DayLogRepository(HabitGridDatabase database, IHlcClock clock, IDataChangeBus? bus = null)
        : base(database, clock, bus)
    {
    }

    public async Task<DayLog?> GetAsync(Guid habitId, DateOnly day, CancellationToken ct = default)
    {
        await using var connection = await Database.OpenConnectionAsync();
        await using var command = Command(
            connection,
            null,
            $"SELECT {Columns} FROM day_logs WHERE habit_id = $habit AND day = $day;");
        command.Parameters.AddWithValue("$habit", habitId.ToString("D"));
        command.Parameters.AddWithValue("$day", Sql.ToSqlDate(day));

        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? Read(reader) : null;
    }

    public async Task<IReadOnlyList<DayLog>> GetRangeAsync(
        Guid habitId,
        DateOnly from,
        DateOnly to,
        CancellationToken ct = default) =>
        await ListAsync(
            "habit_id = $habit AND day >= $from AND day <= $to",
            c =>
            {
                c.Parameters.AddWithValue("$habit", habitId.ToString("D"));
                c.Parameters.AddWithValue("$from", Sql.ToSqlDate(from));
                c.Parameters.AddWithValue("$to", Sql.ToSqlDate(to));
            },
            ct);

    public async Task<IReadOnlyList<DayLog>> GetAllForHabitAsync(Guid habitId, CancellationToken ct = default) =>
        await ListAsync("habit_id = $habit", c => c.Parameters.AddWithValue("$habit", habitId.ToString("D")), ct);

    public async Task<IReadOnlyList<DayLog>> GetForDayAsync(DateOnly day, CancellationToken ct = default) =>
        await ListAsync("day = $day", c => c.Parameters.AddWithValue("$day", Sql.ToSqlDate(day)), ct);

    public async Task UpsertAsync(DayLog log, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(log);

        var stamped = log with { UpdatedAt = DateTime.UtcNow, Version = Clock.Now() };

        await InTransactionAsync((connection, transaction) =>
        {
            using (var command = Command(connection, transaction, $"""
                INSERT INTO day_logs ({Columns})
                VALUES ($habit, $day, $value, $status, $note, $mood, $checked_at, $updated_at, $hlc)
                ON CONFLICT(habit_id, day) DO UPDATE SET
                    value = excluded.value, status = excluded.status, note = excluded.note, mood = excluded.mood,
                    checked_at = excluded.checked_at, updated_at = excluded.updated_at, hlc = excluded.hlc;
                """))
            {
                Bind(command, stamped);
                command.ExecuteNonQuery();
            }

            Enqueue(connection, transaction, "dayLog", Key(stamped.HabitId, stamped.Day), "upsert", stamped, stamped.Version);
            return 0;
        });

        Publish(DataEntity.DayLog, Key(log.HabitId, log.Day));
    }

    public async Task DeleteAsync(Guid habitId, DateOnly day, CancellationToken ct = default)
    {
        var hlc = Clock.Now();

        var removed = await InTransactionAsync((connection, transaction) =>
        {
            using var command = Command(connection, transaction, "DELETE FROM day_logs WHERE habit_id = $habit AND day = $day;");
            command.Parameters.AddWithValue("$habit", habitId.ToString("D"));
            command.Parameters.AddWithValue("$day", Sql.ToSqlDate(day));
            var rows = command.ExecuteNonQuery();

            if (rows > 0)
            {
                Enqueue(
                    connection,
                    transaction,
                    "dayLog",
                    Key(habitId, day),
                    "delete",
                    new { habitId, day, version = hlc },
                    hlc);
            }

            return rows;
        });

        if (removed > 0)
        {
            Publish(DataEntity.DayLog, Key(habitId, day));
        }
    }

    internal static string Key(Guid habitId, DateOnly day) => $"{habitId:D}|{Sql.ToSqlDate(day)}";

    private async Task<IReadOnlyList<DayLog>> ListAsync(string where, Action<SqliteCommand> bind, CancellationToken ct)
    {
        await using var connection = await Database.OpenConnectionAsync();
        await using var command = Command(
            connection,
            null,
            $"SELECT {Columns} FROM day_logs WHERE {where} ORDER BY day;");
        bind(command);

        var list = new List<DayLog>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(Read(reader));
        }

        return list;
    }

    internal static DayLog Read(SqliteDataReader r) => new()
    {
        HabitId = Guid.Parse(r.GetString(0)),
        Day = Sql.ToDateOnly(r.GetString(1)),
        Value = r.GetInt32(2),
        Status = (DayStatus)r.GetInt32(3),
        Note = r.IsDBNull(4) ? null : r.GetString(4),
        Mood = r.IsDBNull(5) ? null : r.GetInt32(5),
        CheckedAt = Sql.ToTimestampOrNull(r.IsDBNull(6) ? null : r.GetString(6)),
        UpdatedAt = Sql.ToTimestamp(r.GetString(7)),
        Version = Hlc.Parse(r.GetString(8))
    };

    internal static void Bind(SqliteCommand c, DayLog l)
    {
        c.Parameters.AddWithValue("$habit", l.HabitId.ToString("D"));
        c.Parameters.AddWithValue("$day", Sql.ToSqlDate(l.Day));
        c.Parameters.AddWithValue("$value", l.Value);
        c.Parameters.AddWithValue("$status", (int)l.Status);
        c.Parameters.AddWithValue("$note", (object?)l.Note ?? DBNull.Value);
        c.Parameters.AddWithValue("$mood", (object?)l.Mood ?? DBNull.Value);
        c.Parameters.AddWithValue("$checked_at", (object?)Sql.ToSqlTimestamp(l.CheckedAt) ?? DBNull.Value);
        c.Parameters.AddWithValue("$updated_at", Sql.ToSqlTimestamp(l.UpdatedAt));
        c.Parameters.AddWithValue("$hlc", l.Version.ToString());
    }
}
