using HabitGrid.Core.Domain;
using HabitGrid.Data;
using HabitGrid.Data.V3;
using Microsoft.Data.Sqlite;

namespace HabitGrid.Data.Tests.V3;

/// <summary>CSDL v3 trong bộ nhớ cùng các repository dùng chung cho test.</summary>
public sealed class V3Fixture : IDisposable
{
    private long _ticks = 1_700_000_000_000;

    public V3Fixture()
    {
        Database = HabitGridV3.OpenInMemory();
        Clock = new HlcClock("test-device", () => ++_ticks);
        Bus = new DataChangeBus();
        Changes = [];
        Bus.Changed += (_, change) => Changes.Add(change);

        Habits = new HabitRepository(Database, Clock, Bus);
        DayLogs = new DayLogRepository(Database, Clock, Bus);
        Routines = new RoutineRepository(Database, Clock, Bus);
        Profile = new ProfileRepository(Database, Clock, Bus);
    }

    public HabitGridDatabase Database { get; }

    public HlcClock Clock { get; }

    public DataChangeBus Bus { get; }

    public List<DataChange> Changes { get; }

    public HabitRepository Habits { get; }

    public DayLogRepository DayLogs { get; }

    public RoutineRepository Routines { get; }

    public ProfileRepository Profile { get; }

    public static Habit NewHabit(string name = "Đọc sách", string sortKey = "V") => new()
    {
        Name = name,
        SortKey = sortKey,
        StartDate = new DateOnly(2026, 1, 1)
    };

    public long Scalar(string sql)
    {
        using var connection = Database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar());
    }

    public List<string> Strings(string sql)
    {
        using var connection = Database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        var list = new List<string>();
        while (reader.Read())
        {
            list.Add(reader.GetString(0));
        }

        return list;
    }

    public void Execute(string sql)
    {
        using var connection = Database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    public long OutboxCount => Scalar("SELECT COUNT(*) FROM sync_outbox;");

    public void Dispose() => Database.Dispose();
}
