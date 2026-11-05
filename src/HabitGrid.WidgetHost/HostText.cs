using System.Globalization;

namespace HabitGrid.WidgetHost;

/// <summary>Chữ hiển thị của WidgetHost (tiến trình nhẹ, không nạp bộ localizer của app): Việt/Anh theo ngôn ngữ hệ thống.</summary>
public static class HostText
{
    public static bool IsVietnamese { get; set; } =
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "vi";

    public static string Today => IsVietnamese ? "Hôm nay" : "Today";

    public static string AllDone => IsVietnamese ? "Xong hết rồi! 🎉" : "All done! 🎉";

    public static string NothingDue => IsVietnamese ? "Hôm nay không có gì cần làm 😴" : "Nothing due today 😴";

    public static string QuickPlaceholder => IsVietnamese ? "Gõ để lọc, Enter để check…" : "Type to filter, Enter to check…";

    public static string QuickHint => IsVietnamese
        ? "Enter: check   + / −: tăng giảm   Esc: đóng"
        : "Enter: check   + / −: adjust   Esc: close";

    public static string NoMatch => IsVietnamese ? "Không có thói quen nào khớp" : "No habit matches";

    public static string Progress(int done, int total) => $"{done}/{total}";
}
