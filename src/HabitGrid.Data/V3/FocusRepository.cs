using HabitGrid.Core.Domain;
using HabitGrid.Core.Domain.Focus;
using Sql = HabitGrid.Data.Repositories.SqlMapping;

namespace HabitGrid.Data.V3;

/// <summary>
/// Nhật ký phiên Focus, lưu trong bảng sự kiện <c>xp_events</c> có sẵn của lược đồ v3 (không cần đổi lược đồ):
/// <c>kind = "focus"</c>, <c>amount</c> = số phút, <c>occurred_at</c> = lúc kết thúc. Mỗi phiên vào outbox để đồng bộ.
/// </summary>
public sealed class FocusRepository : RepositoryBase, IFocusRepository
{
    public const string Kind = "focus";

    public FocusRepository(HabitGridDatabase database, IHlcClock clock, IDataChangeBus? bus = null)
        : base(database, clock, bus)
    {
    }

    public async Task<IReadOnlyList<FocusRecord>> GetAllAsync(CancellationToken ct = default)
    {
        await using var connection = await Database.OpenConnectionAsync();
        await using var command = Command(connection, null,
            "SELECT id, amount, occurred_at, habit_id FROM xp_events WHERE kind = $kind ORDER BY occurred_at;");
        command.Parameters.AddWithValue("$kind", Kind);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var list = new List<FocusRecord>();

        while (await reader.ReadAsync(ct))
        {
            var minutes = reader.GetInt32(1);
            var ended = Sql.ToTimestamp(reader.GetString(2));
            Guid? habit = reader.IsDBNull(3) ? null : Guid.Parse(reader.GetString(3));
            list.Add(new FocusRecord(Guid.Parse(reader.GetString(0)), habit, ended.AddMinutes(-minutes), ended, minutes, FocusMode.Pomodoro));
        }

        return list;
    }

    public async Task AddAsync(FocusRecord record, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentOutOfRangeException.ThrowIfLessThan(record.Minutes, 1);

        await InTransactionAsync((connection, transaction) =>
        {
            var hlc = Clock.Now();
            using var command = Command(connection, transaction, """
                INSERT INTO xp_events (id, kind, amount, occurred_at, habit_id, hlc)
                VALUES ($id, $kind, $amount, $at, $habit, $hlc);
                """);
            command.Parameters.AddWithValue("$id", record.Id.ToString());
            command.Parameters.AddWithValue("$kind", Kind);
            command.Parameters.AddWithValue("$amount", record.Minutes);
            command.Parameters.AddWithValue("$at", Sql.ToSqlTimestamp(record.EndedAtUtc));
            command.Parameters.AddWithValue("$habit", (object?)record.HabitId?.ToString() ?? DBNull.Value);
            command.Parameters.AddWithValue("$hlc", hlc.ToString());
            command.ExecuteNonQuery();

            Enqueue(connection, transaction, "xp_events", record.Id.ToString(), "upsert",
                new { id = record.Id, kind = Kind, amount = record.Minutes, occurredAt = record.EndedAtUtc, habitId = record.HabitId, version = hlc }, hlc);
            return 0;
        });

        Publish(DataEntity.XpEvent, record.Id.ToString());
    }
}
