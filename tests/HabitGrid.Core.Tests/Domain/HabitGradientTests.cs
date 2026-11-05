using System.Text.RegularExpressions;
using FluentAssertions;
using HabitGrid.Core.Domain;
using Xunit;

namespace HabitGrid.Core.Tests.Domain;

public class HabitGradientTests
{
    /// <summary>12 màu preset của design system.</summary>
    public static readonly string[] Presets =
    [
        "#8B5CF6", "#3B82F6", "#10B981", "#84CC16", "#FACC15", "#FB923C",
        "#F43F5E", "#EC4899", "#06B6D4", "#A78BFA", "#A16207", "#64748B"
    ];

    public static IEnumerable<object[]> PresetData => Presets.Select(p => new object[] { p });

    [Theory]
    [MemberData(nameof(PresetData))]
    public void Gradient_HasValidDistinctStops(string preset)
    {
        var stops = HabitGradient.From(preset);

        foreach (var hex in new[] { stops.Start, stops.End, stops.Glow })
        {
            Regex.IsMatch(hex, "^#[0-9A-F]{6}$").Should().BeTrue(hex);
        }

        stops.Start.Should().NotBe(stops.End, "gradient phải có hai đầu khác nhau");
    }

    [Theory]
    [MemberData(nameof(PresetData))]
    public void WhiteText_MeetsWcagAA_OnBothStops_AfterScrim(string preset)
    {
        var stops = HabitGradient.From(preset);

        HabitGradient.ContrastRatio("#FFFFFF", HabitGradient.Darken(stops.Start, stops.ScrimOpacity))
            .Should().BeGreaterThanOrEqualTo(HabitGradient.MinTextContrast, $"{preset} đầu gradient");
        HabitGradient.ContrastRatio("#FFFFFF", HabitGradient.Darken(stops.End, stops.ScrimOpacity))
            .Should().BeGreaterThanOrEqualTo(HabitGradient.MinTextContrast, $"{preset} cuối gradient");
    }

    [Fact]
    public void Scrim_IsKeptSmall_SoColorsStayVivid()
    {
        foreach (var preset in Presets)
        {
            HabitGradient.From(preset).ScrimOpacity.Should().BeLessThanOrEqualTo(0.4, preset);
        }
    }

    [Fact]
    public void Gradient_IsDeterministicAndCached()
    {
        HabitGradient.From("#8B5CF6").Should().BeSameAs(HabitGradient.From("#8b5cf6"));
    }

    [Fact]
    public void ContrastRatio_MatchesKnownValues()
    {
        HabitGradient.ContrastRatio("#FFFFFF", "#000000").Should().BeApproximately(21.0, 0.01);
        HabitGradient.ContrastRatio("#FFFFFF", "#FFFFFF").Should().BeApproximately(1.0, 0.001);
        HabitGradient.ContrastRatio("#FFFFFF", "#767676").Should().BeApproximately(4.54, 0.02);
    }

    [Fact]
    public void Darken_ScalesChannels()
    {
        HabitGradient.Darken("#FFFFFF", 0.5).Should().Be("#808080");
        HabitGradient.Darken("#336699", 0).Should().Be("#336699");
    }

    [Fact]
    public void InvalidHex_Throws()
    {
        FluentActions.Invoking(() => HabitGradient.From("purple")).Should().Throw<FormatException>();
    }
}
