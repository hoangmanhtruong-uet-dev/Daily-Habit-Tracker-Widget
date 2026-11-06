using HabitGrid.Core.Domain;
using HabitGrid.Core.Domain.Insights;
using HabitGrid.Presentation.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace HabitGrid.WinUI.Controls;

/// <summary>Thẻ tổng kết tuần (đầu tuần) và thẻ "ý định tuần này" trên trang Hôm nay (F6).</summary>
public sealed class WeeklyCardStrip : UserControl
{
    private readonly StackPanel _panel = new() { Spacing = 12 };

    public WeeklyCardStrip()
    {
        IsTabStop = false;
        Content = _panel;
    }

    public bool HasItems => _panel.Children.Count > 0;

    public async Task ReloadAsync() => Render(await App.Host.Get<WeeklyRecapService>().GetAsync());

    /// <summary>Dựng thẻ từ dữ liệu cho sẵn (smoke chụp ảnh thẻ tổng kết dù hôm nay không phải đầu tuần).</summary>
    public void Render(WeeklyCard card)
    {
        _panel.Children.Clear();

        if (card.Recap is { } recap)
        {
            _panel.Children.Add(BuildRecap(recap, card.Choices));
        }

        if (card.Intent is { } intent)
        {
            _panel.Children.Add(BuildIntent(intent));
        }

        Visibility = _panel.Children.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private UIElement BuildRecap(WeeklyRecap recap, IReadOnlyList<Habit> choices)
    {
        var culture = Ui.CurrentCulture;
        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(new TextBlock
        {
            Text = $"{Ui.T("weekly.title")} · {recap.WeekStart.ToString("dd/MM", culture)}–{recap.WeekEnd.ToString("dd/MM", culture)}",
            Style = (Style)Application.Current.Resources["CardTitleTextStyle"]
        });

        var rate = recap.Rate is { } r ? $"{Math.Round(r * 100)}%" : "—";
        var delta = recap.Points is { } p and not 0 ? $"  {(p > 0 ? "▲" : "▼")} {Math.Abs(p)}" : string.Empty;
        stack.Children.Add(new TextBlock { Text = Ui.Loc.Format("weekly.rate", rate) + delta, FontSize = 20, TextWrapping = TextWrapping.Wrap });

        var facts = new List<string> { Ui.Loc.Format("weekly.checkins", recap.CheckIns), Ui.Loc.Format("weekly.perfect", recap.PerfectDays) };

        if (recap.BestDay is { } day)
        {
            facts.Add(Ui.Loc.Format("weekly.bestday", day.ToString("dddd", culture)));
        }

        if (recap.BestHabit is { } best)
        {
            facts.Add(Ui.Loc.Format("weekly.besthabit", $"{Ui.IconText(best.Icon)} {best.Name}"));
        }

        stack.Children.Add(new TextBlock { Text = string.Join(" · ", facts), Style = (Style)Application.Current.Resources["BodySecondaryTextStyle"], TextWrapping = TextWrapping.Wrap });

        var tip = recap.Tip == RecapTipKind.FocusWeakest && recap.WeakestHabit is { } weak
            ? Ui.Loc.Format("weekly.tip.FocusWeakest", weak.Name, Math.Round(weak.Rate * 100))
            : Ui.T("weekly.tip." + recap.Tip);
        stack.Children.Add(new TextBlock { Text = "💡 " + tip, TextWrapping = TextWrapping.Wrap });

        if (choices.Count > 0)
        {
            stack.Children.Add(new TextBlock { Text = Ui.T("weekly.intent.pick"), Style = (Style)Application.Current.Resources["SectionHeader"] });
            var chips = new ChipGroup { MinChipWidth = 140, HorizontalAlignment = HorizontalAlignment.Left };

            foreach (var habit in choices)
            {
                chips.Add($"{Ui.IconText(habit.Icon.Value)} {habit.Name}");
            }

            chips.SelectedIndex = -1;
            chips.SelectionChanged += async (_, _) =>
            {
                if (chips.SelectedIndex is >= 0 and var index && index < choices.Count)
                {
                    await App.Host.Get<WeeklyRecapService>().SetIntentAsync(choices[index].Id);
                    await ReloadAsync();
                }
            };
            stack.Children.Add(chips);
        }

        var seen = new Button { Content = Ui.T("weekly.seen"), Style = (Style)Application.Current.Resources["GhostButtonStyle"], HorizontalAlignment = HorizontalAlignment.Left };
        AutomationProperties.SetName(seen, Ui.T("weekly.seen"));
        seen.Click += async (_, _) =>
        {
            await App.Host.Get<WeeklyRecapService>().DismissAsync();
            await ReloadAsync();
        };
        stack.Children.Add(seen);
        return new Border { Style = (Style)Application.Current.Resources["CardBorder"], Child = stack };
    }

    private UIElement BuildIntent(IntentProgress intent)
    {
        var head = new Grid { ColumnSpacing = 10 };
        head.ColumnDefinitions.Add(new ColumnDefinition());
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titles = new StackPanel();
        titles.Children.Add(new TextBlock { Text = "🎯 " + Ui.T("weekly.intent.title"), Style = (Style)Application.Current.Resources["CardTitleTextStyle"] });
        titles.Children.Add(new TextBlock
        {
            Text = Ui.Loc.Format("weekly.intent.progress", $"{Ui.IconText(intent.Icon)} {intent.Name}", intent.Done, intent.Due),
            Style = (Style)Application.Current.Resources["BodySecondaryTextStyle"],
            TextWrapping = TextWrapping.Wrap
        });
        head.Children.Add(titles);

        var clear = new Button { Content = Ui.T("weekly.intent.clear"), Style = (Style)Application.Current.Resources["GhostButtonStyle"] };
        AutomationProperties.SetName(clear, Ui.T("weekly.intent.clear"));
        clear.Click += async (_, _) =>
        {
            await App.Host.Get<WeeklyRecapService>().SetIntentAsync(null);
            await ReloadAsync();
        };
        Grid.SetColumn(clear, 1);
        head.Children.Add(clear);

        var track = new Grid { Height = 8, CornerRadius = new CornerRadius(4), Background = (Brush)Application.Current.Resources["HeatEmptyBrush"] };
        var fill = new Border { CornerRadius = new CornerRadius(4), HorizontalAlignment = HorizontalAlignment.Left, Background = Ui.GradientBrush(HabitGradient.From(intent.ColorHue)) };
        track.Children.Add(fill);
        track.SizeChanged += (_, e) => fill.Width = e.NewSize.Width * intent.Fraction;
        AutomationProperties.SetName(track, $"{intent.Name}: {intent.Done}/{intent.Due}");

        var stack = new StackPanel { Spacing = 10 };
        stack.Children.Add(head);
        stack.Children.Add(track);
        return new Border { Style = (Style)Application.Current.Resources["CardBorder"], Child = stack };
    }
}
