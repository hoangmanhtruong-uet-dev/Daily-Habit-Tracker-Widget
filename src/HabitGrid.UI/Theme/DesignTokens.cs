namespace HabitGrid.UI.Theme;

/// <summary>
/// Design token dùng chung cho toàn bộ giao diện.
/// Khoảng cách theo bội số 4/8/12/16/24, bo góc 4–12, font Segoe UI Variable.
/// </summary>
public static class DesignTokens
{
    /// <summary>Khoảng cách giữa các phần tử.</summary>
    public static class Spacing
    {
        public const double Xs = 4;
        public const double Small = 8;
        public const double Medium = 12;
        public const double Large = 16;
        public const double XLarge = 24;
        public const double Xxl = 32;
    }

    /// <summary>Bo góc.</summary>
    public static class Radius
    {
        public const double Small = 4;
        public const double Medium = 8;
        public const double Large = 12;
    }

    /// <summary>Cỡ chữ.</summary>
    public static class Typography
    {
        public const double Caption = 11;
        public const double Body = 13;
        public const double Subtitle = 15;
        public const double Title = 20;
        public const double LargeTitle = 28;
    }

    /// <summary>Màu sắc nền theo theme.</summary>
    public static class Palette
    {
        public static readonly System.Windows.Media.Color LightBackground = System.Windows.Media.Color.FromRgb(243, 243, 243);
        public static readonly System.Windows.Media.Color LightSurface = System.Windows.Media.Color.FromRgb(255, 255, 255);
        public static readonly System.Windows.Media.Color LightBorder = System.Windows.Media.Color.FromRgb(225, 225, 225);
        public static readonly System.Windows.Media.Color LightText = System.Windows.Media.Color.FromRgb(32, 32, 32);
        public static readonly System.Windows.Media.Color LightSecondaryText = System.Windows.Media.Color.FromRgb(110, 110, 110);

        public static readonly System.Windows.Media.Color DarkBackground = System.Windows.Media.Color.FromRgb(32, 32, 32);
        public static readonly System.Windows.Media.Color DarkSurface = System.Windows.Media.Color.FromRgb(45, 45, 45);
        public static readonly System.Windows.Media.Color DarkBorder = System.Windows.Media.Color.FromRgb(60, 60, 60);
        public static readonly System.Windows.Media.Color DarkText = System.Windows.Media.Color.FromRgb(245, 245, 245);
        public static readonly System.Windows.Media.Color DarkSecondaryText = System.Windows.Media.Color.FromRgb(180, 180, 180);

        /// <summary>Màu nhấn chính (accent).</summary>
        public static readonly System.Windows.Media.Color Accent = System.Windows.Media.Color.FromRgb(0, 120, 212);

        /// <summary>Màu nhấn phụ cho streak / kỷ lục.</summary>
        public static readonly System.Windows.Media.Color Streak = System.Windows.Media.Color.FromRgb(232, 120, 44);

        /// <summary>Màu cảnh báo khoá Pro.</summary>
        public static readonly System.Windows.Media.Color ProLock = System.Windows.Media.Color.FromRgb(196, 89, 17);
    }
}
