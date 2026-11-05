using HabitGrid.Core.Domain;
using HabitGrid.Presentation.ViewModels;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace HabitGrid.WinUI.Controls;

public enum CardAction
{
    Open,
    Edit,
    MoveUp,
    MoveDown,
    Archive,
    Unarchive,
    Delete
}

public sealed class CardActionEventArgs(HabitListItem item, CardAction action) : EventArgs
{
    public HabitListItem Item { get; } = item;

    public CardAction Action { get; } = action;
}

/// <summary>
/// Thẻ "sưu tầm" trên trang Thói quen (03-UI-REBUILD-GLOW2 §5.3): viền gradient của thói quen, emoji, chuỗi,
/// tỷ lệ 30 ngày và heatmap 12 tuần. Bấm mở trang chi tiết; nút ⋯ và chuột phải mở menu thao tác.
/// </summary>
public sealed class HabitCollectionCard : UserControl
{
    public static readonly DependencyProperty ItemProperty =
        DependencyProperty.Register(nameof(Item), typeof(HabitListItem), typeof(HabitCollectionCard), new PropertyMetadata(null, OnItemChanged));

    private const double Radius = 28;

    private readonly Grid _root = new() { MinHeight = 176, CornerRadius = new CornerRadius(Radius) };
    private readonly Border _frame = new() { CornerRadius = new CornerRadius(Radius), BorderThickness = new Thickness(1.5) };
    private readonly TextBlock _emoji = new() { FontSize = 34 };
    private readonly TextBlock _name = new();
    private readonly TextBlock _sub = new();
    private readonly Border _badge = new() { CornerRadius = new CornerRadius(12), Padding = new Thickness(10, 4, 10, 4), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top };
    private readonly TextBlock _badgeText = new() { FontFamily = (FontFamily)Application.Current.Resources["DisplayBoldFont"], FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.Bold };
    private readonly HeatmapGrid _heatmap = new() { CellSize = 11 };
    private readonly Button _more = new();

    private HabitListItem? _item;

    public HabitCollectionCard()
    {
        IsTabStop = true;
        UseSystemFocusVisuals = true;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);

        _name.Style = (Style)Application.Current.Resources["CardTitleTextStyle"];
        _sub.Style = (Style)Application.Current.Resources["BodySecondaryTextStyle"];
        _sub.TextTrimming = TextTrimming.CharacterEllipsis;
        _badge.Child = _badgeText;

        _more.Content = new FontIcon { Glyph = "", FontSize = 15 };
        _more.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        _more.BorderThickness = new Thickness(0);
        _more.Padding = new Thickness(8, 6, 8, 6);
        _more.HorizontalAlignment = HorizontalAlignment.Right;
        _more.VerticalAlignment = VerticalAlignment.Top;
        _more.Flyout = new MenuFlyout();
        ((MenuFlyout)_more.Flyout).Opening += (_, _) => BuildMenu((MenuFlyout)_more.Flyout);
        ContextFlyout = new MenuFlyout();
        ((MenuFlyout)ContextFlyout).Opening += (_, _) => BuildMenu((MenuFlyout)ContextFlyout);

        var top = new Grid { ColumnDefinitions = { new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }, new ColumnDefinition { Width = GridLength.Auto }, new ColumnDefinition { Width = GridLength.Auto } }, ColumnSpacing = 4 };
        top.Children.Add(_emoji);
        Grid.SetColumn(_badge, 1);
        top.Children.Add(_badge);
        Grid.SetColumn(_more, 2);
        top.Children.Add(_more);

        var middle = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 8, 0, 12) };
        middle.Children.Add(_name);
        middle.Children.Add(_sub);

        var content = new Grid
        {
            Padding = new Thickness(18, 14, 14, 18),
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                new RowDefinition { Height = GridLength.Auto }
            }
        };
        content.Children.Add(top);
        Grid.SetRow(middle, 1);
        content.Children.Add(middle);
        Grid.SetRow(_heatmap, 2);
        content.Children.Add(_heatmap);

        _root.Children.Add(_frame);
        _root.Children.Add(content);
        Content = _root;

        Tapped += (_, e) =>
        {
            if (!IsFromMenuButton(e.OriginalSource))
            {
                Raise(CardAction.Open);
            }
        };
        KeyDown += (_, e) =>
        {
            if (e.Key is VirtualKey.Enter or VirtualKey.Space)
            {
                Raise(CardAction.Open);
                e.Handled = true;
            }
        };
    }

    public event EventHandler<CardActionEventArgs>? ActionRequested;

    /// <summary>Mở chi tiết như khi người dùng bấm vào thẻ (smoke test).</summary>
    public void Open() => Raise(CardAction.Open);

    public HabitListItem? Item
    {
        get => (HabitListItem?)GetValue(ItemProperty);
        set => SetValue(ItemProperty, value);
    }

    private static void OnItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((HabitCollectionCard)d).Bind(e.NewValue as HabitListItem);

    private bool IsFromMenuButton(object source) =>
        source is DependencyObject node && IsDescendant(node, _more);

    private static bool IsDescendant(DependencyObject node, DependencyObject ancestor)
    {
        for (var current = node; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    private void Bind(HabitListItem? item)
    {
        _item = item;

        if (item is null)
        {
            return;
        }

        var stops = HabitGradient.From(item.ColorHue);
        _emoji.Text = Ui.IconText(item.Icon);
        _name.Text = item.Name;
        _sub.Text = Ui.HabitSubText(0, item.StreakUnit, item.Rate30);
        _frame.Background = (Brush)Application.Current.Resources["CardIdleBrush"];
        _frame.BorderBrush = Ui.GradientBrush(stops, 0.7);
        _heatmap.ColorHue = item.ColorHue;
        _heatmap.Data = item.Heatmap;
        _root.Opacity = item.IsArchived ? 0.65 : 1;

        if (item.Streak > 0 && item.Habit.Kind != HabitKind.Quit)
        {
            _badgeText.Text = $"🔥 {item.Streak}";
            _badgeText.Foreground = new SolidColorBrush(Ui.ParseColor("#FFB020"));
            _badge.Background = (Brush)Application.Current.Resources["Surface2Brush"];
            _badge.Visibility = Visibility.Visible;
        }
        else
        {
            _badge.Visibility = Visibility.Collapsed;
        }

        var label = $"{item.Name}. {_sub.Text}";
        AutomationProperties.SetName(this, label);
        AutomationProperties.SetName(_more, Ui.T("habits.open"));
        ToolTipService.SetToolTip(_more, null);
    }

    private void BuildMenu(MenuFlyout menu)
    {
        menu.Items.Clear();

        if (_item is not { } item)
        {
            return;
        }

        Add(menu, "habits.open", "", CardAction.Open);
        Add(menu, "habits.edit", "", CardAction.Edit);

        if (!item.IsArchived)
        {
            Add(menu, "habits.moveup", "", CardAction.MoveUp);
            Add(menu, "habits.movedown", "", CardAction.MoveDown);
            Add(menu, "habits.archive", "", CardAction.Archive);
        }
        else
        {
            Add(menu, "habits.unarchive", "", CardAction.Unarchive);
        }

        Add(menu, "habits.delete", "", CardAction.Delete);
    }

    private void Add(MenuFlyout menu, string key, string glyph, CardAction action)
    {
        var entry = new MenuFlyoutItem { Text = Ui.T(key), Icon = new FontIcon { Glyph = glyph } };
        entry.Click += (_, _) => Raise(action);
        menu.Items.Add(entry);
    }

    private void Raise(CardAction action)
    {
        if (_item is { } item)
        {
            ActionRequested?.Invoke(this, new CardActionEventArgs(item, action));
        }
    }
}
