using FluentAssertions;
using HabitGrid.Core.Domain;
using Xunit;

namespace HabitGrid.Core.Tests.Appearance;

public class AppFontsTests
{
    [Theory]
    [InlineData("serif", "serif")]
    [InlineData(" MONO ", "mono")]
    [InlineData(null, "default")]
    [InlineData("", "default")]
    [InlineData("comic-sans", "default")]
    public void Resolve_FallsBackToDefault(string? input, string expected) =>
        AppFonts.Resolve(input).Id.Should().Be(expected);

    [Fact]
    public void Catalog_StartsWithTheBundledDefault_AndHasUniqueIds()
    {
        AppFonts.All[0].Family.Should().BeNull();
        AppFonts.All.Skip(1).Should().OnlyContain(f => !string.IsNullOrWhiteSpace(f.Family));
        AppFonts.All.Select(f => f.Id).Should().OnlyHaveUniqueItems();
    }
}
