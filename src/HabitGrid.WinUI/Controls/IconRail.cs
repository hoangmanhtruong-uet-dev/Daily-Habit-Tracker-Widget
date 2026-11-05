using HabitGrid.Presentation.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace HabitGrid.WinUI.Controls;

/// <summary>
/// Thanh điều hướng dạng "dock" icon (03-UI-REBUILD-GLOW2 §5.1), thay <c>NavigationView</c> danh sách chữ:
/// icon trong pill, mục đang chọn nền gradient; nút ＋ tạo thói quen luôn trong tầm tay; Cài đặt ở dưới cùng.
/// </summary>
public sealed class IconRail : UserControl
{
    private const double ItemSize = 52;
    private readonly Dictionary<string, Button> _buttons = [];
    private readonly Dictionary<string, Border> _fills = [];
    private readonly Dictionary<string, FontIcon> _icons = [];
    private string _selected = string.Empty;

    public IconRail()
    {
        Width = 84;
        IsTabStop = false;
    }

    /// <summary>Người dùng chọn một trang.</summary>
    public event EventHandler<string>? Navigated;

    /// <summary>Bấm nút ＋.</summary>
    public event EventHandler? AddRequested;

    public string Selected => _selected;

    public void Build(IEnumerable<NavItem> main, NavItem settings)
    {
        _fills.Clear();
        var grid = new Grid
        {
            Padding = new Thickness(16, 8, 16, 20),
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition(),
                new RowDefinition { Height = GridLength.Auto }
            }
        };

        var top = new StackPanel { Spacing = 10 };
        foreach (var item in main)
        {
            top.Children.Add(CreateItem(item));
        }

        var bottom = new StackPanel { Spacing = 12 };
        bottom.Children.Add(CreateAddButton());
        bottom.Children.Add(CreateItem(settings));
        Grid.SetRow(bottom, 2);

        grid.Children.Add(top);
        grid.Children.Add(bottom);
        Content = grid;
    }

    public void Select(string key)
    {
        _selected = key;

        foreach (var (k, fill) in _fills)
        {
            var on = k == key;
            fill.Opacity = on ? 1 : 0;
            _icons[k].Foreground = (Brush)Application.Current.Resources[on ? "RailSelectedTextBrush" : "TextSecondaryBrush"];
            AutomationProperties.SetItemStatus(_buttons[k], on ? "selected" : string.Empty);
        }
    }

    private UIElement CreateItem(NavItem item)
    {
        var fill = new Border
        {
            CornerRadius = new CornerRadius(18),
            Background = (Brush)Application.Current.Resources["AccentGradientBrush"],
            Opacity = 0
        };

        var icon = new FontIcon
        {
            Glyph = item.Glyph,
            FontSize = 20,
            Foreground = (Brush)Application.Current.Resources["TextSecondaryBrush"],
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var content = new Grid { Width = ItemSize, Height = ItemSize };
        content.Children.Add(fill);
        content.Children.Add(icon);

        var button = new Button
        {
            Content = content,
            Padding = new Thickness(0),
            Width = ItemSize,
            Height = ItemSize,
            CornerRadius = new CornerRadius(18),
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Center,
            Tag = item.Key
        };

        var title = Ui.T(item.TitleKey);
        ToolTipService.SetToolTip(button, title);
        ToolTipService.SetPlacement(button, Microsoft.UI.Xaml.Controls.Primitives.PlacementMode.Right);
        AutomationProperties.SetName(button, title);
        button.Click += (_, _) =>
        {
            Select(item.Key);
            Navigated?.Invoke(this, item.Key);
        };

        _buttons[item.Key] = button;
        _fills[item.Key] = fill;
        _icons[item.Key] = icon;
        return button;
    }

    private UIElement CreateAddButton()
    {
        var button = new Button
        {
            Style = (Style)Application.Current.Resources["GradientButtonStyle"],
            Width = ItemSize,
            Height = ItemSize,
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Center,
            Content = new FontIcon { Glyph = "", FontSize = 20 }
        };

        var title = Ui.T("habits.add");
        ToolTipService.SetToolTip(button, title);
        ToolTipService.SetPlacement(button, Microsoft.UI.Xaml.Controls.Primitives.PlacementMode.Right);
        AutomationProperties.SetName(button, title);
        button.Click += (_, _) => AddRequested?.Invoke(this, EventArgs.Empty);
        return button;
    }
}
