using FluentAssertions;
using HabitGrid.Core.Services;
using Xunit;

namespace HabitGrid.Core.Tests;

/// <summary>
/// Bộ kiểm thử bộ sinh bảng màu heatmap theo SPEC-01 mục 3.3.
/// </summary>
public class PaletteGeneratorTests
{
    private readonly PaletteGenerator _sut = new();

    [Fact]
    public void GenerateFromHex_WithCustomColor_ShouldReturnValid5Shades()
    {
        var shades = _sut.GenerateFromHex("#30A14E", isDarkTheme: false);

        var all = new[] { shades.Level0Empty, shades.Level1, shades.Level2, shades.Level3, shades.Level4 };

        all.Should().OnlyContain(c => IsValidHex(c));
        all.Should().OnlyHaveUniqueItems();

        // Theme sáng: mức 1 nhạt nhất -> mức 4 đậm nhất.
        var l1 = ColorMath.RelativeLuminance(shades.Level1);
        var l2 = ColorMath.RelativeLuminance(shades.Level2);
        var l3 = ColorMath.RelativeLuminance(shades.Level3);
        var l4 = ColorMath.RelativeLuminance(shades.Level4);

        l1.Should().BeGreaterThan(l2);
        l2.Should().BeGreaterThan(l3);
        l3.Should().BeGreaterThan(l4);
    }

    [Fact]
    public void GetPreset_WithDefaultPreset_ShouldReturnStandardGitHubColors()
    {
        var light = _sut.GetPreset(PaletteGenerator.DefaultPresetId, isDarkTheme: false);

        light.Level0Empty.Should().Be("#EBEDF0");
        light.Level1.Should().Be("#9BE9A8");
        light.Level2.Should().Be("#40C463");
        light.Level3.Should().Be("#30A14E");
        light.Level4.Should().Be("#216E39");

        var dark = _sut.GetPreset(PaletteGenerator.DefaultPresetId, isDarkTheme: true);

        dark.Level0Empty.Should().Be("#161B22");
        dark.Level1.Should().Be("#0E4429");
        dark.Level2.Should().Be("#006D32");
        dark.Level3.Should().Be("#26A641");
        dark.Level4.Should().Be("#39D353");
    }

    [Fact]
    public void GenerateFromHex_InDarkTheme_ShouldInvertLightnessOrder()
    {
        var shades = _sut.GenerateFromHex("#30A14E", isDarkTheme: true);

        var l1 = ColorMath.RelativeLuminance(shades.Level1);
        var l2 = ColorMath.RelativeLuminance(shades.Level2);
        var l3 = ColorMath.RelativeLuminance(shades.Level3);
        var l4 = ColorMath.RelativeLuminance(shades.Level4);

        // Theme tối: mức 1 tối nhất -> mức 4 sáng nhất.
        l1.Should().BeLessThan(l2);
        l2.Should().BeLessThan(l3);
        l3.Should().BeLessThan(l4);
    }

    [Fact]
    public void GenerateFromHex_WithExtremelyLightBase_ShouldStillProduceDistinctShades()
    {
        var shades = _sut.GenerateFromHex("#FFFF99", isDarkTheme: false);

        new[] { shades.Level1, shades.Level2, shades.Level3, shades.Level4 }
            .Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void GetPreset_WithUnknownPresetId_ShouldFallbackToDefault()
    {
        var shades = _sut.GetPreset("khong-ton-tai", isDarkTheme: false);

        shades.Should().BeEquivalentTo(_sut.GetPreset(PaletteGenerator.DefaultPresetId, isDarkTheme: false));
    }

    [Fact]
    public void GenerateFromHex_WithInvalidHex_ShouldThrow()
    {
        var act = () => _sut.GenerateFromHex("XYZ", isDarkTheme: false);

        act.Should().Throw<FormatException>();
    }

    [Theory]
    [InlineData("#9BE9A8")]
    [InlineData("9BE9A8")]
    public void ColorMath_ParseHex_ShouldAcceptBothFormats(string hex)
    {
        var (r, g, b) = ColorMath.ParseHex(hex);

        r.Should().Be(0x9B);
        g.Should().Be(0xE9);
        b.Should().Be(0xA8);
    }

    private static bool IsValidHex(string value)
    {
        return value.Length == 7
            && value[0] == '#'
            && value[1..].All(char.IsAsciiHexDigit);
    }
}
