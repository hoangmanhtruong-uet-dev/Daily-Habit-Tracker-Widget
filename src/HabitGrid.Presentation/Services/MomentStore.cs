using System.Globalization;
using HabitGrid.Core.Domain;
using HabitGrid.Core.Domain.Moments;
using IDayProvider = HabitGrid.Core.Services.IDayProvider;

namespace HabitGrid.Presentation.Services;

public enum MomentAddResult
{
    Added,
    LimitReached,
    InvalidImage,
    NotAllowed
}

/// <summary>
/// Kho ảnh khoảnh khắc trên đĩa: <c>{gốc}/{habitId}/{yyyy-MM-dd}_{HHmmssfff}.{jpg|png}</c>. Không đụng CSDL (không đổi lược đồ),
/// nên WidgetHost (tiến trình khác) đọc được ngay. Ảnh chỉ nằm trên máy người dùng.
/// </summary>
public sealed class MomentStore
{
    private static readonly string[] Extensions = [".jpg", ".jpeg", ".png"];
    private readonly string _root;

    public MomentStore(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        _root = root;
    }

    public string Root => _root;

    public IReadOnlyList<Moment> ForHabit(Guid habitId) => Enumerate(Path.Combine(_root, habitId.ToString("N")), habitId);

    public IReadOnlyList<Moment> ForDay(Guid habitId, DateOnly day) => ForHabit(habitId).Where(m => m.Day == day).ToList();

    public IReadOnlyList<Moment> All()
    {
        if (!Directory.Exists(_root))
        {
            return [];
        }

        return Directory.EnumerateDirectories(_root)
            .SelectMany(d => Guid.TryParseExact(Path.GetFileName(d), "N", out var id) ? Enumerate(d, id) : [])
            .OrderByDescending(m => m.CreatedAtUtc)
            .ToList();
    }

    /// <summary>Ghi ảnh (đã mã hóa JPEG/PNG). Không kiểm giới hạn — việc đó của <see cref="MomentService"/>.</summary>
    public Moment Save(Guid habitId, DateOnly day, byte[] image, string extension, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(image);
        var ext = extension.StartsWith('.') ? extension.ToLowerInvariant() : "." + extension.ToLowerInvariant();

        if (!Extensions.Contains(ext))
        {
            throw new ArgumentException("Chỉ nhận JPEG hoặc PNG.", nameof(extension));
        }

        var folder = Path.Combine(_root, habitId.ToString("N"));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"{day:yyyy-MM-dd}_{nowUtc:HHmmssfff}{ext}");
        File.WriteAllBytes(path, image);
        File.SetCreationTimeUtc(path, nowUtc);
        return new Moment(habitId, day, path, nowUtc);
    }

    public bool Delete(Moment moment)
    {
        ArgumentNullException.ThrowIfNull(moment);

        // Chỉ xóa file nằm trong kho (không cho đường dẫn lạ xóa file ngoài).
        var full = Path.GetFullPath(moment.Path);

        if (!full.StartsWith(Path.GetFullPath(_root), StringComparison.OrdinalIgnoreCase) || !File.Exists(full))
        {
            return false;
        }

        File.Delete(full);
        return true;
    }

    private static IReadOnlyList<Moment> Enumerate(string folder, Guid habitId)
    {
        if (!Directory.Exists(folder))
        {
            return [];
        }

        var list = new List<Moment>();

        foreach (var file in Directory.EnumerateFiles(folder))
        {
            if (!Extensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
            {
                continue;
            }

            var name = Path.GetFileNameWithoutExtension(file);

            if (name.Length < 10 || !DateOnly.TryParseExact(name[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
            {
                continue;
            }

            list.Add(new Moment(habitId, day, file, File.GetCreationTimeUtc(file)));
        }

        return list.OrderByDescending(m => m.Day).ThenByDescending(m => m.Path, StringComparer.Ordinal).ToList();
    }
}

/// <summary>Thêm/xem ảnh khoảnh khắc theo quyền: Free 1 ảnh mỗi thói quen mỗi ngày, Plus không giới hạn.</summary>
public sealed class MomentService
{
    public const int MaxBytes = 15 * 1024 * 1024;

    private readonly MomentStore _store;
    private readonly IEntitlements _entitlements;
    private readonly IDayProvider _days;
    private readonly Func<DateTime> _utcNow;

    public MomentService(MomentStore store, IEntitlements entitlements, IDayProvider days, Func<DateTime>? utcNow = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _entitlements = entitlements ?? throw new ArgumentNullException(nameof(entitlements));
        _days = days ?? throw new ArgumentNullException(nameof(days));
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    public MomentStore Store => _store;

    public bool CanAdd(Guid habitId, DateOnly day) => _store.ForDay(habitId, day).Count < _entitlements.MaxMomentsPerDay;

    public (MomentAddResult Result, Moment? Moment) Add(Habit habit, DateOnly day, byte[] image, string extension)
    {
        ArgumentNullException.ThrowIfNull(habit);

        if (habit.DeletedAt is not null || day > _days.GetToday() || day < habit.StartDate)
        {
            return (MomentAddResult.NotAllowed, null);
        }

        if (image is null || image.Length == 0 || image.Length > MaxBytes || !LooksLikeImage(image))
        {
            return (MomentAddResult.InvalidImage, null);
        }

        if (!CanAdd(habit.Id, day))
        {
            return (MomentAddResult.LimitReached, null);
        }

        return (MomentAddResult.Added, _store.Save(habit.Id, day, image, extension, _utcNow()));
    }

    /// <summary>Ảnh mới nhất (của mọi thói quen đang chạy) — cho widget khoảnh khắc.</summary>
    public Moment? Latest(IReadOnlyCollection<Guid>? activeHabits = null) =>
        _store.All().FirstOrDefault(m => activeHabits is null || activeHabits.Contains(m.HabitId));

    /// <summary>"Ngày này tháng trước": ảnh cùng ngày của tháng trước (hoặc ngày gần nhất trước đó trong vòng 3 ngày).</summary>
    public Moment? OneMonthAgo(DateOnly today, IReadOnlyCollection<Guid>? activeHabits = null)
    {
        var target = today.AddMonths(-1);
        return _store.All()
            .Where(m => (activeHabits is null || activeHabits.Contains(m.HabitId)) && m.Day <= target && m.Day >= target.AddDays(-3))
            .OrderByDescending(m => m.Day)
            .FirstOrDefault();
    }

    /// <summary>Kiểm tra chữ ký file (JPEG FF D8 FF, PNG 89 50 4E 47) để không lưu file lạ đổi đuôi.</summary>
    public static bool LooksLikeImage(byte[] data) =>
        data.Length > 8 && ((data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF) || (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47));
}
