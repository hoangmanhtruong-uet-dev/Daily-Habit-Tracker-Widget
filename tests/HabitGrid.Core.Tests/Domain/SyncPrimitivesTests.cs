using FluentAssertions;
using HabitGrid.Core.Domain;
using Xunit;

namespace HabitGrid.Core.Tests.Domain;

public class SyncPrimitivesTests
{
    // ---- Hlc ----

    [Fact]
    public void Hlc_OrdersByWallThenCounterThenDevice()
    {
        var a = new Hlc(100, 0, "a");
        var b = new Hlc(100, 1, "a");
        var c = new Hlc(100, 1, "b");
        var d = new Hlc(101, 0, "a");

        (a < b).Should().BeTrue();
        (b < c).Should().BeTrue();
        (c < d).Should().BeTrue();
        Hlc.Max(a, d).Should().Be(d);
    }

    [Fact]
    public void Hlc_StringFormIsLexicographicallySortable_AndRoundTrips()
    {
        var items = new[]
        {
            new Hlc(5, 0, "z"), new Hlc(5, 10, "a"), new Hlc(255, 0, "a"), new Hlc(5, 2, "b"), new Hlc(4096, 0, "a")
        };

        var byValue = items.OrderBy(h => h).Select(h => h.ToString()).ToList();
        var byText = items.Select(h => h.ToString()).OrderBy(s => s, StringComparer.Ordinal).ToList();

        byText.Should().Equal(byValue);

        foreach (var hlc in items)
        {
            Hlc.Parse(hlc.ToString()).Should().Be(hlc);
        }
    }

    [Fact]
    public void Hlc_EmptyIsLessThanEverything_AndParsesFromEmptyString()
    {
        Hlc.Empty.IsEmpty.Should().BeTrue();
        (Hlc.Empty < new Hlc(1, 0, "a")).Should().BeTrue();
        Hlc.TryParse("", out var parsed).Should().BeTrue();
        parsed.IsEmpty.Should().BeTrue();
        Hlc.TryParse("not-an-hlc", out _).Should().BeFalse();
    }

    [Fact]
    public void HlcClock_IsStrictlyMonotonic_EvenWhenWallClockStandsStillOrGoesBack()
    {
        var times = new Queue<long>([1000, 1000, 1000, 900, 2000]);
        var clock = new HlcClock("dev", () => times.Dequeue());

        var stamps = Enumerable.Range(0, 5).Select(_ => clock.Now()).ToList();

        for (var i = 1; i < stamps.Count; i++)
        {
            stamps[i].Should().BeGreaterThan(stamps[i - 1]);
        }
    }

    [Fact]
    public void HlcClock_Receive_JumpsAheadOfRemote()
    {
        var clock = new HlcClock("local", () => 1000);
        var remote = new Hlc(5000, 7, "remote");

        var received = clock.Receive(remote);
        var next = clock.Now();

        received.Should().BeGreaterThan(remote);
        next.Should().BeGreaterThan(received);
    }

    [Fact]
    public void HlcClock_Receive_FlagsLargeDrift()
    {
        var clock = new HlcClock("local", () => 1000);

        clock.Receive(new Hlc(1000 + (long)TimeSpan.FromHours(2).TotalMilliseconds, 0, "remote"));

        clock.DriftDetected.Should().BeTrue();
    }

    [Fact]
    public async Task HlcClock_ConcurrentCalls_NeverProduceDuplicates()
    {
        var clock = new HlcClock("dev", () => 1000);
        var bag = new System.Collections.Concurrent.ConcurrentBag<Hlc>();

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < 500; i++)
            {
                bag.Add(clock.Now());
            }
        })));

        bag.Distinct().Should().HaveCount(4000);
    }

    // ---- FractionalIndex ----

    [Fact]
    public void FractionalIndex_FirstKeys()
    {
        FractionalIndex.Between(null, null).Should().Be("V");
        string.CompareOrdinal(FractionalIndex.After("V"), "V").Should().BePositive();
        string.CompareOrdinal(FractionalIndex.Before("V"), "V").Should().BeNegative();
    }

    [Fact]
    public void FractionalIndex_Between_IsStrictlyBetween()
    {
        var pairs = new (string?, string?)[]
        {
            ("V", "W"), ("V", "V1"), ("1", "2"), ("zz", null), (null, "1"), ("a", "b"), ("Vz", "W"), ("0V", "1")
        };

        foreach (var (a, b) in pairs)
        {
            var mid = FractionalIndex.Between(a, b);

            FractionalIndex.IsValid(mid).Should().BeTrue($"{a}~{b} -> {mid}");
            if (a is not null)
            {
                string.CompareOrdinal(a, mid).Should().BeNegative($"{a} < {mid}");
            }

            if (b is not null)
            {
                string.CompareOrdinal(mid, b).Should().BeNegative($"{mid} < {b}");
            }
        }
    }

    [Fact]
    public void FractionalIndex_RandomInsertions_AlwaysKeepStrictOrder()
    {
        var random = new Random(99);
        var keys = new List<string> { FractionalIndex.Between(null, null) };

        for (var i = 0; i < 3000; i++)
        {
            var position = random.Next(keys.Count + 1);
            var before = position == 0 ? null : keys[position - 1];
            var after = position == keys.Count ? null : keys[position];

            var key = FractionalIndex.Between(before, after);
            keys.Insert(position, key);
        }

        keys.Should().BeInAscendingOrder(StringComparer.Ordinal);
        keys.Distinct().Should().HaveCount(keys.Count);
        keys.Should().OnlyContain(k => FractionalIndex.IsValid(k));
    }

    [Fact]
    public void FractionalIndex_RepeatedInsertionAtSamePosition_KeysGrowSlowly()
    {
        string? left = "V";
        const string right = "W";

        for (var i = 0; i < 200; i++)
        {
            left = FractionalIndex.Between(left, right);
        }

        left!.Length.Should().BeLessThan(200, "mỗi lần chèn không được kéo dài khóa 1 ký tự");
    }

    [Fact]
    public void FractionalIndex_Generate_ProducesIncreasingKeys()
    {
        var keys = FractionalIndex.Generate(null, null, 50);

        keys.Should().BeInAscendingOrder(StringComparer.Ordinal);
        keys.Distinct().Should().HaveCount(50);
    }

    [Fact]
    public void FractionalIndex_RejectsInvalidArguments()
    {
        FluentActions.Invoking(() => FractionalIndex.Between("b", "a")).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => FractionalIndex.Between("a", "a")).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => FractionalIndex.Between("a0", null)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => FractionalIndex.Between("a!", null)).Should().Throw<ArgumentException>();
    }
}
