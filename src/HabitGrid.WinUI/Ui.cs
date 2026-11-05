using HabitGrid.Core.Domain;
using HabitGrid.Presentation.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace HabitGrid.WinUI;

/// <summary>Hàm trợ giúp cho <c>x:Bind</c> trong XAML: định dạng chữ, màu, hiển thị theo trạng thái.</summary>
public static class Ui
{
    public static ILocalizer Loc => App.Host.Get<ILocalizer>();

    public static string T(string key) => Loc[key];

    public static Color ParseColor(string? hex, Color fallback = default)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return fallback;
        }

        var s = hex.TrimStart('#');
        if (!uint.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out var v))
        {
            return fallback;
        }

        return s.Length switch
        {
            6 => Color.FromArgb(255, (byte)(v >> 16), (byte)(v >> 8), (byte)v),
            8 => Color.FromArgb((byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v),
            _ => fallback
        };
    }

    public static SolidColorBrush Brush(string? hex) => new(ParseColor(hex, Colors.Gray));

    /// <summary>Nền nhạt (22% độ đậm) của màu thói quen, dùng làm nền icon.</summary>
    public static SolidColorBrush SoftBrush(string? hex)
    {
        var c = ParseColor(hex, Colors.Gray);
        return new SolidColorBrush(Color.FromArgb(56, c.R, c.G, c.B));
    }

    public static string GroupTitle(TimeOfDay time) => time switch
    {
        TimeOfDay.Morning => T("group.morning"),
        TimeOfDay.Afternoon => T("group.afternoon"),
        TimeOfDay.Evening => T("group.evening"),
        _ => T("group.anytime")
    };

    public static string StreakText(int streak, StreakUnit unit)
    {
        var key = unit switch
        {
            StreakUnit.Week => "streak.weeks",
            StreakUnit.Month => "streak.months",
            StreakUnit.Window => "streak.windows",
            _ => "streak.days"
        };

        return Loc.Format(key, streak);
    }

    /// <summary>Dòng phụ dưới tên thói quen: tiến độ (đếm/thời lượng) hoặc streak.</summary>
    public static string SubText(HabitKind kind, int value, int target, string? unit, int streak, StreakUnit streakUnit, bool atRisk)
    {
        var parts = new List<string>();

        if (kind is HabitKind.Count or HabitKind.Duration)
        {
            var label = kind == HabitKind.Duration ? "min" : unit ?? string.Empty;
            parts.Add(Loc.Format("today.progressUnit", value, target, label).Trim());
        }

        if (kind == HabitKind.Quit)
        {
            parts.Add(Loc.Format("quit.clean", streak));
        }
        else if (streak > 0)
        {
            parts.Add("🔥 " + StreakText(streak, streakUnit));
        }

        if (atRisk)
        {
            parts.Add(T("streak.atRisk"));
        }

        return string.Join(" · ", parts);
    }

    public static string Progress(int done, int total) => Loc.Format("today.progress", done, total);

    public static Visibility Show(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility ShowWhenAny(int count) => count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public static string Greeting(DateTime now) => now.Hour switch
    {
        < 12 => T("today.greeting.morning"),
        < 18 => T("today.greeting.afternoon"),
        _ => T("today.greeting.evening")
    };

    public static string LongDate(DateOnly day) =>
        day.ToDateTime(TimeOnly.MinValue).ToString("dddd, d MMMM", Loc.Language == "en"
            ? System.Globalization.CultureInfo.GetCultureInfo("en")
            : System.Globalization.CultureInfo.GetCultureInfo("vi"));

    /// <summary>Icon hiển thị: emoji dùng nguyên; icon Fluent của v1 được ánh xạ sang emoji gần nghĩa.</summary>
    public static string IconText(string icon)
    {
        if (icon.StartsWith("emoji:", StringComparison.Ordinal))
        {
            return icon["emoji:".Length..];
        }

        var key = icon.StartsWith("fluent:", StringComparison.Ordinal) ? icon["fluent:".Length..] : icon;

        return key switch
        {
            _ when key.StartsWith("Drop", StringComparison.OrdinalIgnoreCase) => "💧",
            _ when key.StartsWith("Book", StringComparison.OrdinalIgnoreCase) => "📚",
            _ when key.StartsWith("Dumbbell", StringComparison.OrdinalIgnoreCase) => "🏋️",
            _ when key.StartsWith("Heart", StringComparison.OrdinalIgnoreCase) => "❤️",
            _ when key.StartsWith("Sleep", StringComparison.OrdinalIgnoreCase) => "😴",
            _ when key.StartsWith("Run", StringComparison.OrdinalIgnoreCase) => "🏃",
            _ => "✨"
        };
    }

    /// <summary>Chữ nhỏ trong vòng check-in của loại đếm/thời lượng ("3/8"); rỗng với loại khác.</summary>
    public static string RingLabel(HabitKind kind, int value, int target) =>
        kind is HabitKind.Count or HabitKind.Duration && value > 0 ? $"{value}/{target}" : string.Empty;

    /// <summary>Dòng phụ trong danh sách thói quen: streak và tỷ lệ 30 ngày.</summary>
    public static string HabitSubText(int streak, StreakUnit unit, int? rate30)
    {
        var parts = new List<string>();
        if (streak > 0)
        {
            parts.Add("🔥 " + StreakText(streak, unit));
        }

        if (rate30 is { } rate)
        {
            parts.Add(Loc.Format("habits.rate30", rate));
        }

        return string.Join(" · ", parts);
    }

    public static string TopicEmoji(Presentation.Templates.HabitTopic topic) => topic switch
    {
        Presentation.Templates.HabitTopic.Study => "📚",
        Presentation.Templates.HabitTopic.Health => "🍎",
        Presentation.Templates.HabitTopic.Fitness => "💪",
        Presentation.Templates.HabitTopic.Mind => "🧘",
        Presentation.Templates.HabitTopic.Creative => "🎨",
        Presentation.Templates.HabitTopic.Work => "💼",
        Presentation.Templates.HabitTopic.Home => "🏠",
        _ => "🚭"
    };

    public static string TopicTitle(Presentation.Templates.HabitTopic topic) =>
        T($"topic.{topic.ToString().ToLowerInvariant()}");
}
