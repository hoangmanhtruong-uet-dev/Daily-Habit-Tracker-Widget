using FluentAssertions;
using HabitGrid.Core.Domain;
using Xunit;

namespace HabitGrid.Core.Tests.Appearance;

public class AccentColorsTests
{
    [Theory]
    [InlineData("#ff5c8a", "#FF5C8A")]
    [InlineData("  22c55e ", "#22C55E")]
    [InlineData("#ABCDEF", "#ABCDEF")]
    public void Normalize_AcceptsValidHex(string input, string expected) =>
        AccentColors.Normalize(input).Should().Be(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("#12345")]
    [InlineData("#GGGGGG")]
    [InlineData("red")]
    [InlineData("#1234567")]
    public void Normalize_RejectsInvalid(string? input) => AccentColors.Normalize(input).Should().BeNull();

    [Fact]
    public void DeriveEnd_ReturnsAValidDifferentColor_ForEveryPreset()
    {
        foreach (var preset in AccentColors.Presets)
        {
            var end = AccentColors.DeriveEnd(preset);
            AccentColors.Normalize(end).Should().Be(end);
            end.Should().NotBe(preset);
        }
    }

    [Fact]
    public void DeriveEnd_RotatesRedTowardOrange_AndKeepsGreyGrey()
    {
        AccentColors.DeriveEnd("#FF0000").Should().Be("#FF9500");
        AccentColors.DeriveEnd("#808080").Should().Be("#808080");
    }

    [Fact]
    public void DeriveEnd_ThrowsOnInvalid() =>
        FluentActions.Invoking(() => AccentColors.DeriveEnd("nope")).Should().Throw<ArgumentException>();
}
