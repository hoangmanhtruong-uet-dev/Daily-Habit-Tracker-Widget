using HabitGrid.Core.Domain;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace HabitGrid.WinUI.Controls;

/// <summary>Bảy ô nhỏ cho bảy ngày gần nhất (cũ → mới), màu theo trạng thái. Là mini heatmap trên mỗi dòng thói quen.</summary>
public sealed class MiniWeek : UserControl
{
    public static readonly DependencyProperty StatusesProperty =
        DependencyProperty.Register(nameof(Statuses), typeof(IReadOnlyList<DayStatus>), typeof(MiniWeek), new PropertyMetadata(null, OnChanged));

    public static readonly DependencyProperty ColorHueProperty =
        DependencyProperty.Register(nameof(ColorHue), typeof(string), typeof(MiniWeek), new PropertyMetadata("#8B5CF6", OnChanged));

    private const double Cell = 10;
    private readonly StackPanel _panel = new() { Orientation = Orientation.Horizontal, Spacing = 3, VerticalAlignment = VerticalAlignment.Center };

    public MiniWeek()
    {
        Content = _panel;
        IsTabStop = false;
        Rebuild();
    }

    public IReadOnlyList<DayStatus>? Statuses
    {
        get => (IReadOnlyList<DayStatus>?)GetValue(StatusesProperty);
        set => SetValue(StatusesProperty, value);
    }

    public string ColorHue
    {
        get => (string)GetValue(ColorHueProperty);
        set => SetValue(ColorHueProperty, value);
    }

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((MiniWeek)d).Rebuild();

    private void Rebuild()
    {
        _panel.Children.Clear();
        var statuses = Statuses ?? [];
        var color = Ui.ParseColor(ColorHue, Microsoft.UI.Colors.Gray);

        for (var i = 0; i < 7; i++)
        {
            var status = i < statuses.Count ? statuses[i] : DayStatus.None;
            var cell = new Rectangle
            {
                Width = Cell,
                Height = Cell,
                RadiusX = 3,
                RadiusY = 3,
                Fill = FillFor(status, color),
                // Hôm nay (ô cuối) có viền để dễ nhận.
                Stroke = i == 6 ? new SolidColorBrush(color) : null,
                StrokeThickness = i == 6 ? 1.5 : 0
            };
            _panel.Children.Add(cell);
        }
    }

    private static Brush FillFor(DayStatus status, Windows.UI.Color color) => status switch
    {
        DayStatus.Done => new SolidColorBrush(color),
        DayStatus.Partial => new SolidColorBrush(Windows.UI.Color.FromArgb(120, color.R, color.G, color.B)),
        DayStatus.Frozen => (Brush)Application.Current.Resources["FreezeBrush"],
        DayStatus.Skipped => new SolidColorBrush(Windows.UI.Color.FromArgb(70, 128, 128, 128)),
        DayStatus.Failed => (Brush)Application.Current.Resources["WarningBrush"],
        _ => (Brush)Application.Current.Resources["HeatEmptyBrush"]
    };
}
