using HabitGrid.Core.Repositories;
using HabitGrid.Data;
using HabitGrid.Data.Repositories;
using Xunit;

namespace HabitGrid.Data.Tests;

/// <summary>
/// Lớp cơ sở cung cấp CSDL SQLite in-memory đã chạy migration cho mỗi test.
/// </summary>
public abstract class DatabaseTestBase : IAsyncLifetime
{
    protected HabitGridDatabase Database { get; private set; } = null!;

    protected IHabitRepository Habits => new HabitRepository(Database);

    protected IEntryRepository Entries => new EntryRepository(Database);

    protected ISettingsRepository Settings => new SettingsRepository(Database);

    protected IWidgetStateRepository WidgetStates => new WidgetStateRepository(Database);

    public async Task InitializeAsync()
    {
        Database = HabitGridDatabase.CreateInMemory();
        await Database.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        await Database.DisposeAsync();
    }
}
