namespace HabitGrid.Core.Domain;

/// <summary>
/// Khóa sắp xếp dạng phân số cơ số 62 (0-9A-Za-z). Chèn giữa hai khóa luôn sinh được khóa mới
/// mà không phải đánh lại số các phần tử khác, nên kéo-thả không xung đột khi đồng bộ.
/// Khóa hợp lệ không kết thúc bằng '0'. So sánh bằng <see cref="StringComparer.Ordinal"/>.
/// </summary>
public static class FractionalIndex
{
    private const string Digits = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    private const int Base = 62;

    /// <summary>Khóa nằm giữa <paramref name="before"/> và <paramref name="after"/> (null = đầu/cuối danh sách).</summary>
    public static string Between(string? before, string? after)
    {
        if (before is not null)
        {
            Validate(before, nameof(before));
        }

        if (after is not null)
        {
            Validate(after, nameof(after));
        }

        if (before is not null && after is not null && string.CompareOrdinal(before, after) >= 0)
        {
            throw new ArgumentException("'before' phải nhỏ hơn 'after'.");
        }

        return Midpoint(before ?? string.Empty, after);
    }

    /// <summary>Khóa mới đặt ở cuối danh sách.</summary>
    public static string After(string? last) => Between(last, null);

    /// <summary>Khóa mới đặt ở đầu danh sách.</summary>
    public static string Before(string? first) => Between(null, first);

    /// <summary>Sinh <paramref name="count"/> khóa tăng dần liên tiếp nằm giữa hai khóa.</summary>
    public static IReadOnlyList<string> Generate(string? before, string? after, int count)
    {
        var keys = new List<string>(count);
        var previous = before;

        for (var i = 0; i < count; i++)
        {
            var key = Between(previous, after);
            keys.Add(key);
            previous = key;
        }

        return keys;
    }

    public static bool IsValid(string? key)
    {
        if (string.IsNullOrEmpty(key) || key[^1] == '0')
        {
            return false;
        }

        foreach (var c in key)
        {
            if (Digits.IndexOf(c) < 0)
            {
                return false;
            }
        }

        return true;
    }

    private static void Validate(string key, string name)
    {
        if (!IsValid(key))
        {
            throw new ArgumentException($"Khóa sắp xếp không hợp lệ: '{key}'.", name);
        }
    }

    private static string Midpoint(string a, string? b)
    {
        if (b is not null)
        {
            // Bỏ phần tiền tố chung (a được coi là đệm '0' ở bên phải).
            var n = 0;
            while (n < b.Length && (n < a.Length ? a[n] : '0') == b[n])
            {
                n++;
            }

            if (n > 0)
            {
                return b[..n] + Midpoint(a.Length > n ? a[n..] : string.Empty, b[n..]);
            }
        }

        var digitA = a.Length > 0 ? Digits.IndexOf(a[0]) : 0;
        var digitB = b is not null ? Digits.IndexOf(b[0]) : Base;

        if (digitB - digitA > 1)
        {
            var mid = (int)Math.Round(0.5 * (digitA + digitB));
            return Digits[mid].ToString();
        }

        if (b is not null && b.Length > 1)
        {
            return b[..1];
        }

        return Digits[digitA] + Midpoint(a.Length > 1 ? a[1..] : string.Empty, null);
    }
}
