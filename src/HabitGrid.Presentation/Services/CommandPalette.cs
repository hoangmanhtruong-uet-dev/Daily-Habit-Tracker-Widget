namespace HabitGrid.Presentation.Services;

public enum PaletteKind
{
    Page,
    Action,
    OpenHabit,
    CheckHabit
}

/// <summary>Một lệnh trong command palette (Ctrl+K). <see cref="Argument"/>: khóa trang, mã hành động hoặc Id thói quen.</summary>
public sealed record PaletteCommand(PaletteKind Kind, string Title, string Argument, string Glyph = "");

/// <summary>Lọc lệnh cho command palette: không dấu, mọi từ phải khớp; lệnh có tiêu đề bắt đầu bằng truy vấn xếp trước.</summary>
public static class CommandPalette
{
    public const int MaxResults = 12;

    public static IReadOnlyList<PaletteCommand> Filter(IEnumerable<PaletteCommand> commands, string? query, int max = MaxResults)
    {
        ArgumentNullException.ThrowIfNull(commands);

        var list = commands.ToList();

        if (string.IsNullOrWhiteSpace(query))
        {
            // Chưa gõ gì: trang và hành động trước, thói quen sau.
            return list.OrderBy(c => c.Kind is PaletteKind.Page or PaletteKind.Action ? 0 : 1).Take(max).ToList();
        }

        var folded = QuickCheckInModel.Fold(query.Trim());

        return list
            .Where(c => QuickCheckInModel.Matches(c.Title, query))
            .OrderBy(c => QuickCheckInModel.Fold(c.Title).StartsWith(folded, StringComparison.Ordinal) ? 0 : 1)
            .ThenBy(c => c.Kind)
            .ThenBy(c => c.Title, StringComparer.CurrentCulture)
            .Take(max)
            .ToList();
    }
}
