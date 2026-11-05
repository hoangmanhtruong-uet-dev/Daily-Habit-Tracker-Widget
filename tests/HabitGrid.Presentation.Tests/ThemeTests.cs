using System.Text.RegularExpressions;
using FluentAssertions;
using HabitGrid.Core.Domain;
using HabitGrid.Presentation.Theming;

namespace HabitGrid.Presentation.Tests;

public class ThemeTests
{
    private static IEnumerable<ThemePalette> AllPalettes() =>
        Themes.All.SelectMany(t => new[] { t.Dark, t.Light }).OfType<ThemePalette>();

    /// <summary>Màu đặc tương đương khi phủ một màu có alpha lên nền canvas.</summary>
    private static string Flatten(string color, string canvas)
    {
        var c = color.TrimStart('#');
        if (c.Length == 6)
        {
            return "#" + c.ToUpperInvariant();
        }

        var a = Convert.ToInt32(c[..2], 16) / 255.0;
        var bg = canvas.TrimStart('#');
        int Mix(int i) => (int)Math.Round(Convert.ToInt32(c.Substring(2 + i * 2, 2), 16) * a + Convert.ToInt32(bg.Substring(i * 2, 2), 16) * (1 - a));
        return $"#{Mix(0):X2}{Mix(1):X2}{Mix(2):X2}";
    }

    [Fact]
    public void EveryColorToken_IsAValidHex()
    {
        var hex = new Regex("^#([0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$");

        foreach (var palette in AllPalettes())
        {
            foreach (var property in typeof(ThemePalette).GetProperties().Where(p => p.PropertyType == typeof(string)))
            {
                hex.IsMatch((string)property.GetValue(palette)!).Should().BeTrue($"{property.Name}");
            }

            palette.Blobs.Should().HaveCountGreaterThanOrEqualTo(2);
            palette.Blobs.Should().OnlyContain(b => hex.IsMatch(b));
        }
    }

    [Fact]
    public void PrimaryAndSecondaryText_ReadWellOnCanvasAndCards()
    {
        foreach (var palette in AllPalettes())
        {
            var card = Flatten(palette.CardIdle, palette.Canvas);

            HabitGradient.ContrastRatio(palette.TextPrimary, palette.Canvas).Should().BeGreaterThanOrEqualTo(7, "chữ chính trên nền");
            HabitGradient.ContrastRatio(palette.TextPrimary, card).Should().BeGreaterThanOrEqualTo(7, "chữ chính trên thẻ");
            HabitGradient.ContrastRatio(palette.TextSecondary, palette.Canvas).Should().BeGreaterThanOrEqualTo(4.5, "chữ phụ trên nền");
            HabitGradient.ContrastRatio(palette.TextSecondary, card).Should().BeGreaterThanOrEqualTo(4.5, "chữ phụ trên thẻ");
        }
    }

    [Fact]
    public void MutedText_StaysLegible_AndHeatmapEmptyCellsAreVisibleOnCards()
    {
        foreach (var palette in AllPalettes())
        {
            var card = Flatten(palette.CardIdle, palette.Canvas);
            var empty = Flatten(palette.HeatEmpty, card);

            HabitGradient.ContrastRatio(palette.TextMuted, palette.Canvas).Should().BeGreaterThanOrEqualTo(3.0, "chữ mờ trên nền");
            HabitGradient.ContrastRatio(palette.TextMuted, card).Should().BeGreaterThanOrEqualTo(3.0, "chữ mờ trên thẻ");
            HabitGradient.ContrastRatio(empty, card).Should().BeGreaterThanOrEqualTo(1.08, "ô heatmap trống phải thấy được trên thẻ");
        }
    }

    [Fact]
    public void WhiteText_OnAccentGradient_MeetsAA_WithScrim()
    {
        foreach (var palette in AllPalettes())
        {
            foreach (var stop in new[] { palette.AccentStart, palette.AccentEnd })
            {
                var stops = HabitGradient.From(stop);
                HabitGradient.ContrastRatio("#FFFFFF", HabitGradient.Darken(stop, stops.ScrimOpacity + 0.2))
                    .Should().BeGreaterThanOrEqualTo(4.5, stop);
            }
        }
    }

    [Theory]
    [InlineData(ThemeMode.Dark, false, true)]
    [InlineData(ThemeMode.Light, true, false)]
    [InlineData(ThemeMode.System, true, true)]
    [InlineData(ThemeMode.System, false, false)]
    public void CandyBento_ResolvesRequestedMode(ThemeMode mode, bool systemDark, bool expectDark)
    {
        Themes.CandyBento.Resolve(mode, systemDark).IsDark.Should().Be(expectDark);
    }

    [Fact]
    public void SingleModeThemes_FallBackToTheirOnlyPalette()
    {
        Themes.NeonNight.Resolve(ThemeMode.Light, false).IsDark.Should().BeTrue();
        Themes.SoftPastel.Resolve(ThemeMode.Dark, true).IsDark.Should().BeFalse();
    }

    [Fact]
    public void Find_UnknownId_FallsBackToDefault()
    {
        Themes.Find("nope").Id.Should().Be(Themes.CandyBentoId);
        Themes.Find(null).Id.Should().Be(Themes.CandyBentoId);
        Themes.All.Select(t => t.Id).Should().OnlyHaveUniqueItems();
    }
}
