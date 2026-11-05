using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HabitGrid.UI.Theme;

namespace HabitGrid.UI.Controls;

/// <summary>
/// Bảng chọn biểu tượng Fluent cho thói quen (60 icon).
/// </summary>
public partial class IconPicker : UserControl
{
    public IconPicker()
    {
        InitializeComponent();

        foreach (var icon in IconCatalog.All)
        {
            var button = new Button
            {
                Content = new TextBlock
                {
                    Text = icon.Glyph,
                    FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
                    FontSize = 16,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                },
                Tag = icon.Key,
                Width = 34,
                Height = 34,
                Margin = new Thickness(2),
                Padding = new Thickness(0),
                ToolTip = icon.DisplayName,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(1),
                BorderBrush = Brushes.Transparent
            };

            button.Click += OnIconClick;
            IconPanel.Children.Add(button);
        }

        SelectedIconKey = IconCatalog.DefaultKey;
    }

    /// <summary>Khóa icon đang chọn.</summary>
    public static readonly DependencyProperty SelectedIconKeyProperty = DependencyProperty.Register(
        nameof(SelectedIconKey),
        typeof(string),
        typeof(IconPicker),
        new PropertyMetadata(IconCatalog.DefaultKey, OnSelectedIconKeyChanged));

    public string SelectedIconKey
    {
        get => (string)GetValue(SelectedIconKeyProperty);
        set => SetValue(SelectedIconKeyProperty, value);
    }

    /// <summary>Được gọi khi người dùng chọn một biểu tượng.</summary>
    public event EventHandler<string>? IconSelected;

    private void OnIconClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string key })
        {
            SelectedIconKey = key;
            IconSelected?.Invoke(this, key);
        }
    }

    private static void OnSelectedIconKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var picker = (IconPicker)d;
        picker.HighlightSelection(e.NewValue as string);
    }

    private void HighlightSelection(string? key)
    {
        foreach (var child in IconPanel.Children)
        {
            if (child is not Button button)
            {
                continue;
            }

            var isSelected = string.Equals(button.Tag as string, key, StringComparison.OrdinalIgnoreCase);

            button.Background = isSelected
                ? new SolidColorBrush(DesignTokens.Palette.Accent) { Opacity = 0.18 }
                : Brushes.Transparent;

            button.BorderBrush = isSelected
                ? new SolidColorBrush(DesignTokens.Palette.Accent)
                : Brushes.Transparent;
        }
    }
}
