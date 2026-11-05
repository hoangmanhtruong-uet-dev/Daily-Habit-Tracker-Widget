using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace HabitGrid.Presentation.Services;

/// <summary>Một báo lỗi đã lưu trên máy.</summary>
public sealed record CrashReport(string Path, DateTime CreatedAtUtc, string Where, string Summary);

/// <summary>
/// Báo lỗi cục bộ (SPEC-V2-11 §4, chưa có máy chủ): mỗi lỗi nghiêm trọng ghi một file trong thư mục log, gồm phiên bản app,
/// hệ điều hành, nơi xảy ra, ngoại lệ và vài dòng log gần nhất. Không gửi đi đâu; lần mở app sau, người dùng được hỏi có muốn
/// tự gửi file cho nhà phát triển không. Giữ tối đa <see cref="MaxReports"/> file mới nhất.
/// </summary>
public sealed class CrashReportStore
{
    public const int MaxReports = 20;
    public const string AckKey = "crash.ack";
    private const string Prefix = "crash-";

    private readonly string _directory;

    public CrashReportStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    public string Directory => _directory;

    /// <summary>Ghi báo lỗi; không bao giờ ném lỗi (đang trong lúc app gặp sự cố). Trả về đường dẫn file hoặc null.</summary>
    public string? Write(Exception? exception, string where, string appVersion, string? recentLog = null, DateTime? nowUtc = null)
    {
        try
        {
            System.IO.Directory.CreateDirectory(_directory);
            var now = nowUtc ?? DateTime.UtcNow;
            var path = System.IO.Path.Combine(_directory, $"{Prefix}{now:yyyyMMdd-HHmmss-fff}.txt");
            var builder = new StringBuilder()
                .AppendLine("HabitGrid crash report")
                .AppendLine(CultureInfo.InvariantCulture, $"Time (UTC): {now:O}")
                .AppendLine(CultureInfo.InvariantCulture, $"App: {appVersion}")
                .AppendLine(CultureInfo.InvariantCulture, $"OS: {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})")
                .AppendLine(CultureInfo.InvariantCulture, $"Runtime: {RuntimeInformation.FrameworkDescription}")
                .AppendLine(CultureInfo.InvariantCulture, $"Where: {where}")
                .AppendLine()
                .AppendLine(exception?.ToString() ?? "(no exception object)");

            if (!string.IsNullOrWhiteSpace(recentLog))
            {
                builder.AppendLine().AppendLine("--- Recent log ---").AppendLine(recentLog);
            }

            File.WriteAllText(path, builder.ToString());
            Prune();
            return path;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Báo lỗi mới hơn mốc người dùng đã xem lần trước.</summary>
    public IReadOnlyList<CrashReport> PendingSince(DateTime? acknowledgedUtc)
    {
        try
        {
            if (!System.IO.Directory.Exists(_directory))
            {
                return [];
            }

            return System.IO.Directory.EnumerateFiles(_directory, Prefix + "*.txt")
                .Select(Read)
                .Where(r => r is not null && (acknowledgedUtc is null || r.CreatedAtUtc > acknowledgedUtc))
                .Select(r => r!)
                .OrderByDescending(r => r.CreatedAtUtc)
                .ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }

    public static string FormatAck(DateTime utc) => utc.ToString("O", CultureInfo.InvariantCulture);

    public static DateTime? ParseAck(string? value) =>
        DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed) ? parsed.ToUniversalTime() : null;

    private static CrashReport? Read(string path)
    {
        var name = System.IO.Path.GetFileNameWithoutExtension(path)[Prefix.Length..];

        if (!DateTime.TryParseExact(name, "yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var created))
        {
            return null;
        }

        var lines = File.ReadLines(path).Take(12).ToList();
        var where = lines.FirstOrDefault(l => l.StartsWith("Where: ", StringComparison.Ordinal))?["Where: ".Length..] ?? string.Empty;
        var summary = lines.SkipWhile(l => !l.StartsWith("Where: ", StringComparison.Ordinal)).Skip(2).FirstOrDefault() ?? string.Empty;
        return new CrashReport(path, created, where, summary);
    }

    private void Prune()
    {
        foreach (var old in System.IO.Directory.EnumerateFiles(_directory, Prefix + "*.txt").OrderByDescending(f => f).Skip(MaxReports))
        {
            File.Delete(old);
        }
    }
}
