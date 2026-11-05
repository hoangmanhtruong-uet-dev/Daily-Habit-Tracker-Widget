using HabitGrid.Core.Domain;
using Sql = HabitGrid.Data.Repositories.SqlMapping;

namespace HabitGrid.Data.V3;

/// <summary>Huy hiệu đã mở khóa. Mở một lần rồi giữ vĩnh viễn (kể cả khi dữ liệu nền đổi); mỗi lần mở vào outbox để đồng bộ.</summary>
public sealed class AchievementRepository : RepositoryBase, IAchievementRepository
{
    public AchievementRepository(HabitGridDatabase database, IHlcClock clock, IDataChangeBus? bus = null)
        : base(database, clock, bus)
    {
    }

    public async Task<IReadOnlyList<Achievement>> GetAllAsync(CancellationToken ct = default)
    {
        await using var connection = await Database.OpenConnectionAsync();
        await using var command = Command(connection, null, "SELECT id, unlocked_at FROM achievements ORDER BY unlocked_at, id;");
        await using var reader = await command.ExecuteReaderAsync(ct);
        var list = new List<Achievement>();

        while (await reader.ReadAsync(ct))
        {
            list.Add(new Achievement(reader.GetString(0), Sql.ToTimestamp(reader.GetString(1))));
        }

        return list;
    }

    public async Task<bool> UnlockAsync(string id, DateTime unlockedAtUtc, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var inserted = await InTransactionAsync((connection, transaction) =>
        {
            var hlc = Clock.Now();
            using var command = Command(connection, transaction, """
                INSERT OR IGNORE INTO achievements (id, unlocked_at, hlc) VALUES ($id, $at, $hlc);
                """);
            command.Parameters.AddWithValue("$id", id);
            command.Parameters.AddWithValue("$at", Sql.ToSqlTimestamp(unlockedAtUtc));
            command.Parameters.AddWithValue("$hlc", hlc.ToString());

            if (command.ExecuteNonQuery() == 0)
            {
                return false;
            }

            Enqueue(connection, transaction, "achievements", id, "upsert", new { id, unlockedAt = unlockedAtUtc, version = hlc }, hlc);
            return true;
        });

        if (inserted)
        {
            Publish(DataEntity.Achievement, id);
        }

        return inserted;
    }
}
