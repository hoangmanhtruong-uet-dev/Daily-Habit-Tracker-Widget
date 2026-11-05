namespace HabitGrid.Data.Backup;

/// <summary>
/// File sao lưu không hợp lệ (JSON hỏng, thiếu trường bắt buộc, ...).
/// Dữ liệu hiện tại của người dùng được giữ nguyên.
/// </summary>
public class BackupImportException : Exception
{
    public BackupImportException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
