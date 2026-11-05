namespace HabitGrid.Data.Migrations;

/// <summary>
/// Ngoại lệ khi migration thất bại. Lỗi gốc được bảo toàn trong <see cref="Exception.InnerException"/>.
/// </summary>
public class MigrationException : Exception
{
    public MigrationException(int version, string message, Exception innerException)
        : base(message, innerException)
    {
        Version = version;
    }

    /// <summary>Phiên bản migration gây ra lỗi.</summary>
    public int Version { get; }
}
