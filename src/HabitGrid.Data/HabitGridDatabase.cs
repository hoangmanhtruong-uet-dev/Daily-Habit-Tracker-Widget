using Microsoft.Data.Sqlite;

namespace HabitGrid.Data;

/// <summary>
/// Quản lý kết nối SQLite cho ứng dụng.
/// - Áp dụng các PRAGMA bắt buộc: WAL, foreign_keys, synchronous.
/// - Hỗ trợ cơ sở dữ liệu trên đĩa và cơ sở dữ liệu in-memory (dùng cho kiểm thử).
/// </summary>
public sealed class HabitGridDatabase : IDisposable, IAsyncDisposable
{
    /// <summary>Phiên bản lược đồ CSDL hiện tại của ứng dụng.</summary>
    public const int CurrentSchemaVersion = 2;

    private readonly SqliteConnection? _keepAliveConnection;
    private bool _disposed;

    private HabitGridDatabase(string connectionString, string? databasePath, SqliteConnection? keepAliveConnection)
    {
        ConnectionString = connectionString;
        DatabasePath = databasePath;
        _keepAliveConnection = keepAliveConnection;
    }

    /// <summary>Chuỗi kết nối SQLite.</summary>
    public string ConnectionString { get; }

    /// <summary>Đường dẫn file CSDL, null nếu là cơ sở dữ liệu in-memory.</summary>
    public string? DatabasePath { get; }

    /// <summary>True nếu đang dùng cơ sở dữ liệu in-memory (dùng cho kiểm thử).</summary>
    public bool IsInMemory => DatabasePath is null;

    /// <summary>Đường dẫn CSDL mặc định: %LocalAppData%\HabitGrid\habitgrid.db</summary>
    public static string DefaultDatabasePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HabitGrid",
        "habitgrid.db");

    /// <summary>Thư mục lưu bản sao lưu mặc định: %LocalAppData%\HabitGrid\backups</summary>
    public static string DefaultBackupDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HabitGrid",
        "backups");

    /// <summary>Tạo cơ sở dữ liệu tại đường dẫn mặc định của ứng dụng.</summary>
    public static HabitGridDatabase CreateDefault() => CreateAtPath(DefaultDatabasePath);

    /// <summary>Tạo cơ sở dữ liệu tại một đường dẫn chỉ định (thư mục được tạo tự động nếu chưa có).</summary>
    public static HabitGridDatabase CreateAtPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = true
        }.ToString();

        return new HabitGridDatabase(connectionString, fullPath, null);
    }

    /// <summary>
    /// Tạo cơ sở dữ liệu in-memory dùng chung cho toàn bộ kết nối trong tiến trình.
    /// Giữ một kết nối "keep-alive" để dữ liệu không bị xóa khi các kết nối khác đóng lại.
    /// </summary>
    public static HabitGridDatabase CreateInMemory(string? name = null)
    {
        var databaseName = name ?? $"habitgrid_{Guid.NewGuid():N}";

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = $"file:{databaseName}?mode=memory&cache=shared",
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = false
        }.ToString();

        var keepAlive = new SqliteConnection(connectionString);
        keepAlive.Open();

        return new HabitGridDatabase(connectionString, null, keepAlive);
    }

    /// <summary>Mở một kết nối mới, đã áp dụng PRAGMA.</summary>
    public SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        ApplyPragmas(connection);
        return connection;
    }

    /// <summary>Mở một kết nối mới (bất đồng bộ), đã áp dụng PRAGMA.</summary>
    public async Task<SqliteConnection> OpenConnectionAsync()
    {
        var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        ApplyPragmas(connection);
        return connection;
    }

    /// <summary>Áp dụng các PRAGMA bắt buộc theo đặc tả.</summary>
    private static void ApplyPragmas(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();

        // WAL cho phép đọc/ghi song song; foreign_keys bật ràng buộc khóa ngoại; synchronous = NORMAL cân bằng an toàn và tốc độ.
        command.CommandText = "PRAGMA journal_mode = WAL; PRAGMA foreign_keys = ON; PRAGMA synchronous = NORMAL;";
        command.ExecuteNonQuery();
    }

    /// <summary>Chạy các migration còn thiếu để đưa CSDL lên phiên bản hiện tại.</summary>
    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var runner = new Migrations.MigrationRunner(this);
        runner.ApplyMigrations();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Xóa các kết nối đang được giữ trong connection pool.
    /// Cần thiết khi đóng ứng dụng để giải phóng file CSDL trên đĩa.
    /// </summary>
    public void ClearPool()
    {
        using var connection = new SqliteConnection(ConnectionString);
        SqliteConnection.ClearPool(connection);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _keepAliveConnection?.Dispose();
        ClearPool();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_keepAliveConnection is not null)
        {
            await _keepAliveConnection.DisposeAsync();
        }

        ClearPool();
    }
}
