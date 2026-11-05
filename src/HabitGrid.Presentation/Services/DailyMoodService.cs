using System.Globalization;
using System.Text;
using HabitGrid.Core.Domain;
using IDayProvider = HabitGrid.Core.Services.IDayProvider;

namespace HabitGrid.Presentation.Services;

/// <summary>Tâm trạng cả ngày (1–5) kèm câu ghi chú ngắn tùy chọn.</summary>
public sealed record DayMood(DateOnly Day, int Mood, string? Note);

/// <summary>
/// Check-in cảm xúc theo ngày (F4). Lưu trong settings dưới một khối văn bản (mỗi dòng <c>yyyy-MM-dd=mood|ghi chú</c>), đọc một lần
/// cho cả khoảng thời gian nên Insights nhanh. Lưu cục bộ (settings không đồng bộ); khi có sync cần chuyển sang bảng riêng.
/// </summary>
public sealed class DailyMoodService
{
    public const string Key = "daymoods";
    public const int MaxNote = 140;

    private readonly ISettingsStore _settings;
    private readonly IDayProvider _days;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public DailyMoodService(ISettingsStore settings, IDayProvider days)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _days = days ?? throw new ArgumentNullException(nameof(days));
    }

    public async Task<DayMood?> GetAsync(DateOnly day, CancellationToken ct = default) =>
        (await AllAsync(ct)).GetValueOrDefault(day);

    /// <summary>Mọi ngày đã ghi, theo ngày.</summary>
    public async Task<IReadOnlyDictionary<DateOnly, DayMood>> AllAsync(CancellationToken ct = default) =>
        Parse(await _settings.GetAsync(Key, ct));

    /// <summary>Ghi (hoặc xóa khi <paramref name="mood"/> null) tâm trạng của một ngày. Không ghi cho ngày tương lai. Trả về true nếu đã lưu.</summary>
    public async Task<bool> SetAsync(DateOnly day, int? mood, string? note = null, CancellationToken ct = default)
    {
        if (day > _days.GetToday() || mood is < 1 or > 5)
        {
            return false;
        }

        await _gate.WaitAsync(ct);

        try
        {
            var all = Parse(await _settings.GetAsync(Key, ct)).ToDictionary(p => p.Key, p => p.Value);

            if (mood is null)
            {
                all.Remove(day);
            }
            else
            {
                all[day] = new DayMood(day, mood.Value, Clean(note));
            }

            await _settings.SetAsync(Key, Format(all.Values), ct);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Cắt ngắn, bỏ xuống dòng và ký tự phân cách để giữ định dạng một dòng.</summary>
    public static string? Clean(string? note)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            return null;
        }

        var cleaned = note.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return cleaned.Length > MaxNote ? cleaned[..MaxNote] : cleaned;
    }

    public static string Format(IEnumerable<DayMood> moods)
    {
        var builder = new StringBuilder();

        foreach (var m in moods.OrderBy(x => x.Day))
        {
            builder.Append(m.Day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append('=').Append(m.Mood.ToString(CultureInfo.InvariantCulture));

            if (m.Note is not null)
            {
                builder.Append('|').Append(m.Note);
            }

            builder.Append('\n');
        }

        return builder.ToString();
    }

    public static IReadOnlyDictionary<DateOnly, DayMood> Parse(string? raw)
    {
        var result = new Dictionary<DateOnly, DayMood>();

        foreach (var line in (raw ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = line.IndexOf('=');

            if (eq != 10 || !DateOnly.TryParseExact(line[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
            {
                continue;
            }

            var rest = line[(eq + 1)..];
            var bar = rest.IndexOf('|');
            var moodText = bar < 0 ? rest : rest[..bar];

            if (int.TryParse(moodText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var mood) && mood is >= 1 and <= 5)
            {
                result[day] = new DayMood(day, mood, bar < 0 ? null : Clean(rest[(bar + 1)..]));
            }
        }

        return result;
    }
}
