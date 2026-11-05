using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HabitGrid.Core.Models;
using HabitGrid.UI.Services;
using HabitGrid.UI.Theme;

namespace HabitGrid.UI.Views;

/// <summary>
/// Luồng làm quen ban đầu (mục tiêu: hoàn tất dưới 30 giây).
/// Bước 1 chào đón → bước 2 chọn 1 trong 5 mẫu → bước 3 tạo widget ngay trên desktop.
/// </summary>
public partial class OnboardingWindow : Window
{
    /// <summary>Một thói quen mẫu được đề xuất trong luồng làm quen.</summary>
    private sealed record SampleHabit(string Name, string IconKey, HabitKind Kind, int Target);

    private static readonly SampleHabit[] SampleItems =
    {
        new("Uống đủ 2L nước", "WaterDrop24Regular", HabitKind.Count, 8),
        new("Đọc sách 20 phút", "Book24Regular", HabitKind.Boolean, 1),
        new("Tập thể dục / Đi bộ", "Bicycle24Regular", HabitKind.Boolean, 1),
        new("Học lập trình / Kỹ năng mới", "Code24Regular", HabitKind.Boolean, 1),
        new("Ngồi thiền 10 phút", "WeatherMoon24Regular", HabitKind.Boolean, 1)
    };

    /// <summary>Danh sách 5 thói quen mẫu được đề xuất (theo đặc tả SPEC-04 mục 5).</summary>
    public static IReadOnlyList<OnboardingChoice> Samples { get; } = SampleItems
        .Select(s => new OnboardingChoice(s.Name, s.IconKey, s.Kind, s.Target))
        .ToArray();

    public OnboardingWindow()
    {
        InitializeComponent();

        foreach (var sample in SampleItems)
        {
            var card = CreateSampleCard(sample);
            SamplePanel.Children.Add(card);
        }
    }

    /// <summary>Thói quen người dùng chọn (null nếu bỏ qua).</summary>
    public OnboardingChoice? Choice { get; private set; }

    /// <summary>True nếu người dùng muốn tự tạo thói quen.</summary>
    public bool WantsCustomHabit { get; private set; }

    private Button CreateSampleCard(SampleHabit sample)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 12, 10, 12) };

        content.Children.Add(new TextBlock
        {
            Text = IconCatalog.Get(sample.IconKey).Glyph,
            FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
            FontSize = 20,
            Width = 34,
            VerticalAlignment = VerticalAlignment.Center
        });

        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock { Text = sample.Name, FontSize = 13, FontWeight = FontWeights.SemiBold });

        var subtitle = sample.Kind == HabitKind.Count
            ? $"Mục tiêu {sample.Target} ly mỗi ngày"
            : "Đánh dấu mỗi ngày";

        text.Children.Add(new TextBlock { Text = subtitle, FontSize = 11, Foreground = new SolidColorBrush(DesignTokens.Palette.LightSecondaryText) });

        content.Children.Add(text);

        var button = new Button
        {
            Content = content,
            Tag = sample,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(4),
            Padding = new Thickness(4),
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(DesignTokens.Palette.LightBorder),
            BorderThickness = new Thickness(1)
        };

        button.Click += OnSampleClick;

        return button;
    }

    private void OnSampleClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: SampleHabit sample })
        {
            Choice = new OnboardingChoice(sample.Name, sample.IconKey, sample.Kind, sample.Target);
            DialogResult = true;
            Close();
        }
    }

    private void OnCustomClick(object sender, RoutedEventArgs e)
    {
        WantsCustomHabit = true;
        DialogResult = true;
        Close();
    }

    private void OnSkipClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
