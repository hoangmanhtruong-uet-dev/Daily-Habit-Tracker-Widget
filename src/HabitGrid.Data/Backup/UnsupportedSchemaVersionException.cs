namespace HabitGrid.Data.Backup;

/// <summary>
/// File sao lưu có phiên bản lược đồ mới hơn ứng dụng hiện tại nên không thể nạp.
/// </summary>
public class UnsupportedSchemaVersionException : Exception
{
    public UnsupportedSchemaVersionException(int fileSchemaVersion, int currentSchemaVersion)
        : base($"File sao lưu dùng lược đồ phiên bản {fileSchemaVersion}, trong khi ứng dụng chỉ hỗ trợ đến phiên bản {currentSchemaVersion}. Vui lòng cập nhật ứng dụng.")
    {
        FileSchemaVersion = fileSchemaVersion;
        CurrentSchemaVersion = currentSchemaVersion;
    }

    /// <summary>Phiên bản lược đô trong file sao lưu.</summary>
    public int FileSchemaVersion { get; }

    /// <summary>Phiên bản lược đô ứng dụng đang hỗ trợ.</summary>
    public int CurrentSchemaVersion { get; }
}
