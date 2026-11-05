using System.Globalization;

namespace HabitGrid.Core.Domain;

/// <summary>
/// Hybrid Logical Clock: <c>wallMs:counter:deviceId</c>. So sánh theo thứ tự (wallMs, counter, deviceId)
/// nên cùng một dữ liệu luôn cho cùng một thứ tự trên mọi thiết bị.
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(HlcJsonConverter))]
public readonly record struct Hlc : IComparable<Hlc>
{
    public Hlc(long wallMs, int counter, string deviceId)
    {
        WallMs = wallMs;
        Counter = counter;
        DeviceId = deviceId ?? string.Empty;
    }

    public long WallMs { get; }

    public int Counter { get; }

    public string DeviceId { get; }

    /// <summary>Giá trị chưa từng được gán (nhỏ hơn mọi Hlc hợp lệ).</summary>
    public bool IsEmpty => WallMs == 0 && Counter == 0 && string.IsNullOrEmpty(DeviceId);

    public static Hlc Empty => default;

    public int CompareTo(Hlc other)
    {
        var byWall = WallMs.CompareTo(other.WallMs);
        if (byWall != 0)
        {
            return byWall;
        }

        var byCounter = Counter.CompareTo(other.Counter);
        return byCounter != 0
            ? byCounter
            : string.CompareOrdinal(DeviceId ?? string.Empty, other.DeviceId ?? string.Empty);
    }

    public static bool operator <(Hlc a, Hlc b) => a.CompareTo(b) < 0;

    public static bool operator >(Hlc a, Hlc b) => a.CompareTo(b) > 0;

    public static bool operator <=(Hlc a, Hlc b) => a.CompareTo(b) <= 0;

    public static bool operator >=(Hlc a, Hlc b) => a.CompareTo(b) >= 0;

    public static Hlc Max(Hlc a, Hlc b) => a >= b ? a : b;

    /// <summary>Chuỗi sắp xếp được theo từ điển: 16 hex : 4 hex : deviceId.</summary>
    public override string ToString() =>
        IsEmpty
            ? string.Empty
            : $"{WallMs.ToString("x16", CultureInfo.InvariantCulture)}:{Counter.ToString("x4", CultureInfo.InvariantCulture)}:{DeviceId}";

    public static bool TryParse(string? text, out Hlc result)
    {
        result = default;

        if (string.IsNullOrEmpty(text))
        {
            return true;
        }

        var parts = text.Split(':', 3);
        if (parts.Length != 3 ||
            !long.TryParse(parts[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var wall) ||
            !int.TryParse(parts[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var counter))
        {
            return false;
        }

        result = new Hlc(wall, counter, parts[2]);
        return true;
    }

    public static Hlc Parse(string text) =>
        TryParse(text, out var value) ? value : throw new FormatException($"HLC không hợp lệ: '{text}'.");
}

/// <summary>Nguồn Hlc cho một thiết bị.</summary>
public interface IHlcClock
{
    string DeviceId { get; }

    /// <summary>Sinh Hlc mới cho một thay đổi cục bộ.</summary>
    Hlc Now();

    /// <summary>Nhận Hlc từ thiết bị khác để đồng hồ cục bộ luôn đi trước mọi thứ đã thấy.</summary>
    Hlc Receive(Hlc remote);
}

/// <summary>Hiện thực <see cref="IHlcClock"/> an toàn đa luồng.</summary>
public sealed class HlcClock : IHlcClock
{
    /// <summary>Lệch đồng hồ tối đa chấp nhận khi nhận Hlc từ xa (1 giờ).</summary>
    public static readonly TimeSpan MaxDrift = TimeSpan.FromHours(1);

    private readonly Func<long> _nowMs;
    private readonly object _gate = new();
    private long _wall;
    private int _counter;

    public HlcClock(string deviceId, Func<long>? nowMs = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        DeviceId = deviceId;
        _nowMs = nowMs ?? (() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }

    public string DeviceId { get; }

    /// <summary>True nếu lần <see cref="Receive"/> gần nhất thấy Hlc từ xa lệch quá <see cref="MaxDrift"/>.</summary>
    public bool DriftDetected { get; private set; }

    public Hlc Now()
    {
        lock (_gate)
        {
            var physical = _nowMs();

            if (physical > _wall)
            {
                _wall = physical;
                _counter = 0;
            }
            else
            {
                _counter++;
            }

            return new Hlc(_wall, _counter, DeviceId);
        }
    }

    public Hlc Receive(Hlc remote)
    {
        lock (_gate)
        {
            var physical = _nowMs();

            DriftDetected = remote.WallMs - physical > MaxDrift.TotalMilliseconds;

            var maxWall = Math.Max(Math.Max(_wall, remote.WallMs), physical);

            if (maxWall == _wall && maxWall == remote.WallMs)
            {
                _counter = Math.Max(_counter, remote.Counter) + 1;
            }
            else if (maxWall == _wall)
            {
                _counter++;
            }
            else if (maxWall == remote.WallMs)
            {
                _counter = remote.Counter + 1;
            }
            else
            {
                _counter = 0;
            }

            _wall = maxWall;
            return new Hlc(_wall, _counter, DeviceId);
        }
    }
}
