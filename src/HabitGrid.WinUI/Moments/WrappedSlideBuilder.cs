using HabitGrid.Core.Domain.Insights;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.UI;

namespace HabitGrid.WinUI.Moments;

/// <summary>Thông tin Buddy cho slide cuối khi tháng đó không có ảnh khoảnh khắc.</summary>
public sealed record WrappedBuddy(string Name, int Level, string StageEmoji);

/// <summary>
/// Dựng các slide "Wrapped" kiểu story ở 360×640 (logic px); khi lưu được render lại ở 1080×1920 (P1).
/// Mỗi slide một gam màu riêng, số liệu lớn, chữ trắng.
/// </summary>
public static class WrappedSlideBuilder
{
    public const double Width = 360;
    public const double Height = 640;

    private static readonly (string Start, string End)[] Palettes =
    [
        ("#7C3AED", "#EC4899"), // Total
        ("#F97316", "#EF4444"), // TopHabit
        ("#EF4444", "#F59E0B"), // Streak
        ("#0EA5E9", "#6366F1"), // GoldenHour
        ("#10B981", "#14B8A6"), // PerfectDays
        ("#EC4899", "#8B5CF6")  // Moment
    ];

    public static FrameworkElement Build(WrappedSlideKind kind, MonthlyWrapped data, WrappedBuddy buddy, BitmapImage? photo = null, bool showWatermark = true)
    {
        var (start, end) = Palettes[(int)kind];
        var background = new LinearGradientBrush { StartPoint = new Windows.Foundation.Point(0, 0), EndPoint = new Windows.Foundation.Point(1, 1) };
        background.GradientStops.Add(new GradientStop { Color = Ui.ParseColor(start), Offset = 0 });
        background.GradientStops.Add(new GradientStop { Color = Ui.ParseColor(end), Offset = 1 });

        var monthName = new DateTime(data.Year, data.Month, 1).ToString("MMMM yyyy", Ui.CurrentCulture);
        var stack = new StackPanel { Spacing = 14, Padding = new Thickness(28, 56, 28, 40), VerticalAlignment = VerticalAlignment.Center };
        stack.Children.Add(Text(monthName.ToUpperInvariant(), 13, 0.85, bold: true));

        if (data.IsEmpty)
        {
            stack.Children.Add(Text("🌱", 90, 1));
            stack.Children.Add(Text(Ui.T("wrapped.empty"), 22, 1, display: true));
        }
        else
        {
            switch (kind)
            {
                case WrappedSlideKind.Total:
                    stack.Children.Add(Text(Ui.T("wrapped.total.lead"), 18, 0.9));
                    stack.Children.Add(Text(data.TotalCheckIns.ToString(Ui.CurrentCulture), 96, 1, display: true));
                    stack.Children.Add(Text(Ui.T("wrapped.total.unit"), 22, 1, display: true));
                    stack.Children.Add(Text(Ui.Loc.Format("wrapped.total.days", data.ActiveDays), 15, 0.9));

                    if (data.CheckInsDelta is { } delta and not 0)
                    {
                        stack.Children.Add(Text($"{(delta > 0 ? "▲" : "▼")} {Ui.Loc.Format("wrapped.total.delta", Math.Abs(delta))}", 15, 0.9));
                    }

                    break;

                case WrappedSlideKind.TopHabit when data.TopHabit is { } top:
                    stack.Children.Add(Text(Ui.T("wrapped.top.lead"), 18, 0.9));
                    stack.Children.Add(Text(Ui.IconText(top.Icon), 96, 1));
                    stack.Children.Add(Text(top.Name, 30, 1, display: true));
                    stack.Children.Add(Text(Ui.Loc.Format("wrapped.top.count", top.Count), 18, 0.9));
                    break;

                case WrappedSlideKind.Streak when data.LongestStreak is { } streak:
                    stack.Children.Add(Text(Ui.T("wrapped.streak.lead"), 18, 0.9));
                    stack.Children.Add(Text("🔥", 80, 1));
                    stack.Children.Add(Text(Ui.Loc.Format("wrapped.streak.days", streak.Count), 44, 1, display: true));
                    stack.Children.Add(Text($"{Ui.IconText(streak.Icon)} {streak.Name}", 18, 0.9));
                    break;

                case WrappedSlideKind.GoldenHour when data.GoldenHour is { } hour:
                    stack.Children.Add(Text(Ui.T("wrapped.hour.lead"), 18, 0.9));
                    stack.Children.Add(Text(hour < 12 ? "🌅" : hour < 18 ? "☀️" : "🌙", 80, 1));
                    stack.Children.Add(Text($"{hour:00}:00–{(hour + 1) % 24:00}:00", 40, 1, display: true));
                    stack.Children.Add(Text(Ui.T("wrapped.hour.note"), 16, 0.9));
                    break;

                case WrappedSlideKind.PerfectDays:
                    stack.Children.Add(Text(Ui.T("wrapped.perfect.lead"), 18, 0.9));
                    stack.Children.Add(Text("✨", 80, 1));
                    stack.Children.Add(Text(data.PerfectDays.ToString(Ui.CurrentCulture), 80, 1, display: true));
                    stack.Children.Add(Text(Ui.T("wrapped.perfect.unit"), 20, 1, display: true));
                    break;

                case WrappedSlideKind.Moment when photo is not null:
                    stack.Children.Add(Text(Ui.T("wrapped.moment.lead"), 18, 0.9));
                    stack.Children.Add(new Border
                    {
                        Height = 300,
                        CornerRadius = new CornerRadius(24),
                        Background = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                        Child = new Image { Source = photo, Stretch = Stretch.UniformToFill }
                    });
                    break;

                case WrappedSlideKind.Moment:
                    stack.Children.Add(Text(Ui.T("wrapped.buddy.lead"), 18, 0.9));
                    stack.Children.Add(Text(buddy.StageEmoji, 96, 1));
                    stack.Children.Add(Text(buddy.Name, 28, 1, display: true));
                    stack.Children.Add(Text(Ui.Loc.Format("buddy.level", buddy.Level), 18, 0.9));
                    break;

                default:
                    // Tháng có dữ liệu nhưng thiếu mục này (ví dụ chưa có chuỗi 2 ngày): nói nhẹ nhàng thay vì để trống.
                    stack.Children.Add(Text(Ui.T("wrapped.nodata." + kind), 22, 1, display: true));
                    break;
            }
        }

        var root = new Grid { Width = Width, Height = Height, Background = background, CornerRadius = new CornerRadius(24) };
        root.Children.Add(stack);

        if (showWatermark)
        {
            root.Children.Add(new TextBlock
            {
                Text = "HabitGrid · Wrapped",
                FontSize = 11,
                Foreground = new SolidColorBrush(Colors.White),
                Opacity = 0.75,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 14)
            });
        }

        AutomationProperties.SetName(root, $"{Ui.T("wrapped.title")} {monthName}");
        return root;
    }

    private static TextBlock Text(string text, double size, double opacity, bool display = false, bool bold = false)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = size,
            Foreground = new SolidColorBrush(Colors.White),
            Opacity = opacity,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };

        if (display)
        {
            block.FontFamily = (FontFamily)Application.Current.Resources["DisplayFont"];
        }

        if (bold)
        {
            block.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        }

        return block;
    }
}
