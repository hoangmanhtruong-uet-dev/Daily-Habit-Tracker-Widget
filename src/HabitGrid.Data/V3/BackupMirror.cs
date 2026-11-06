namespace HabitGrid.Data.V3;

/// <summary>Kết quả nhân bản một bản sao lưu sang thư mục khác.</summary>
public sealed record MirrorResult(bool Succeeded, string? Path, string? Error);

/// <summary>Một thư mục đồng bộ cloud phát hiện được trên máy (gợi ý cho người dùng).</summary>
public sealed record CloudFolder(string Name, string Path);

/// <summary>
/// Nhân bản sao lưu sang thư mục do người dùng chọn (OneDrive, Google Drive, Dropbox… — các dịch vụ đó tự đồng bộ thư mục lên mây) (P3).
/// App không tự đăng nhập hay gửi dữ liệu lên dịch vụ nào: chỉ ghi file vào một thư mục trên máy. Ghi bằng file tạm rồi đổi tên
/// để dịch vụ cloud không bao giờ đồng bộ một file ghi dở. Lỗi (ổ ngắt kết nối, hết dung lượng) trả về kết quả chứ không ném.
/// </summary>
public static class BackupMirror
{
    public const int DefaultRetention = 14;
    private const string ProbeName = ".habitgrid-write-test";

    /// <summary>Thư mục dùng được không: tồn tại (hoặc tạo được), ghi được, và không trùng thư mục sao lưu cục bộ.</summary>
    public static string? Validate(string? target, string localDirectory)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return "empty";
        }

        string full;

        try
        {
            full = Path.GetFullPath(target);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return "invalid";
        }

        if (string.Equals(full.TrimEnd('\\', '/'), Path.GetFullPath(localDirectory).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
        {
            return "same-as-local";
        }

        try
        {
            Directory.CreateDirectory(full);
            var probe = Path.Combine(full, ProbeName);
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return "not-writable";
        }
    }

    /// <summary>Chép <paramref name="source"/> sang <paramref name="targetDirectory"/> rồi dọn bản tự động cũ ở đó theo hạn giữ.</summary>
    public static async Task<MirrorResult> CopyAsync(string source, string targetDirectory, int retention = DefaultRetention, CancellationToken ct = default)
    {
        try
        {
            Directory.CreateDirectory(targetDirectory);
            var destination = Path.Combine(targetDirectory, Path.GetFileName(source));
            var temp = destination + ".tmp";

            await using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
            await using (var output = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await input.CopyToAsync(output, ct);
            }

            File.Move(temp, destination, overwrite: true);
            Prune(targetDirectory, retention);
            return new MirrorResult(true, destination, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new MirrorResult(false, null, ex.Message);
        }
    }

    /// <summary>Chỉ dọn bản TỰ ĐỘNG cũ; không bao giờ xóa file khác trong thư mục cloud của người dùng.</summary>
    public static void Prune(string directory, int retention)
    {
        foreach (var old in BackupManager.ListIn(directory).Where(b => b.IsAutomatic).Skip(retention))
        {
            try
            {
                File.Delete(old.Path);
            }
            catch (IOException)
            {
                // Dịch vụ cloud đang khóa file: lần sau dọn tiếp.
            }
        }
    }

    /// <summary>Gợi ý các thư mục cloud thường gặp (chỉ những cái thật sự tồn tại), kèm thư mục con "HabitGrid".</summary>
    public static IReadOnlyList<CloudFolder> DetectCloudFolders(Func<string, string?>? env = null, Func<string, bool>? exists = null)
    {
        env ??= Environment.GetEnvironmentVariable;
        exists ??= Directory.Exists;
        var home = env("USERPROFILE") ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var found = new List<CloudFolder>();

        void Add(string name, string? root)
        {
            if (!string.IsNullOrWhiteSpace(root) && exists(root) && found.All(f => !string.Equals(f.Path, Path.Combine(root, "HabitGrid"), StringComparison.OrdinalIgnoreCase)))
            {
                found.Add(new CloudFolder(name, Path.Combine(root, "HabitGrid")));
            }
        }

        Add("OneDrive", env("OneDriveConsumer"));
        Add("OneDrive", env("OneDrive"));
        Add("OneDrive", Path.Combine(home, "OneDrive"));
        Add("Google Drive", Path.Combine(home, "Google Drive"));
        Add("Google Drive", Path.Combine(home, "My Drive"));
        Add("Dropbox", Path.Combine(home, "Dropbox"));
        return found;
    }
}
