using System.Text.Json;
using HabitGrid.Core.Domain;
using Microsoft.Data.Sqlite;
using Sql = HabitGrid.Data.Repositories.SqlMapping;

namespace HabitGrid.Data.V3;

/// <summary>
/// Nền chung cho repository v3: mọi lần ghi gán HLC, ghi bản ghi và một dòng <c>sync_outbox</c> trong CÙNG transaction,
/// rồi mới phát <see cref="IDataChangeBus"/> sau khi commit.
/// </summary>
public abstract class RepositoryBase
{
    protected RepositoryBase(HabitGridDatabase database, IHlcClock clock, IDataChangeBus? bus)
    {
        Database = database ?? throw new ArgumentNullException(nameof(database));
        Clock = clock ?? throw new ArgumentNullException(nameof(clock));
        Bus = bus;
    }

    protected HabitGridDatabase Database { get; }

    protected IHlcClock Clock { get; }

    protected IDataChangeBus? Bus { get; }

    protected void Publish(DataEntity entity, string key) => Bus?.Publish(new DataChange(entity, key));

    protected static void Enqueue(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string entity,
        string entityId,
        string op,
        object payload,
        Hlc hlc)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO sync_outbox (entity, entity_id, op, payload_json, hlc, created_at)
            VALUES ($entity, $id, $op, $payload, $hlc, $createdAt);
            """;
        command.Parameters.AddWithValue("$entity", entity);
        command.Parameters.AddWithValue("$id", entityId);
        command.Parameters.AddWithValue("$op", op);
        command.Parameters.AddWithValue("$payload", JsonSerializer.Serialize(payload, DomainJson.Options));
        command.Parameters.AddWithValue("$hlc", hlc.ToString());
        command.Parameters.AddWithValue("$createdAt", Sql.ToSqlTimestamp(DateTime.UtcNow));
        command.ExecuteNonQuery();
    }

    /// <summary>Chạy <paramref name="work"/> trong một transaction; lỗi thì rollback toàn bộ (kể cả dòng outbox).</summary>
    protected async Task<T> InTransactionAsync<T>(Func<SqliteConnection, SqliteTransaction, T> work)
    {
        await using var connection = await Database.OpenConnectionAsync();
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync();

        try
        {
            var result = work(connection, transaction);
            await transaction.CommitAsync();
            return result;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    protected static SqliteCommand Command(SqliteConnection connection, SqliteTransaction? transaction, string sql)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        return command;
    }
}
