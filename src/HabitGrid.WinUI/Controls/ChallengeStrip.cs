using HabitGrid.Core.Domain;
using HabitGrid.Core.Domain.Challenges;
using HabitGrid.Presentation.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace HabitGrid.WinUI.Controls;

/// <summary>Dải thử thách trên trang Hôm nay (F2): thanh tiến độ "Ngày 12/30", chúc mừng khi xong, nút làm lại khi lỡ.</summary>
public sealed class ChallengeStrip : UserControl
{
    private readonly StackPanel _panel = new() { Spacing = 12 };

    public ChallengeStrip()
    {
        IsTabStop = false;
        Content = _panel;
    }

    public bool HasItems => _panel.Children.Count > 0;

    /// <summary>Số thử thách vừa hoàn thành ở lần nạp gần nhất (smoke dùng để kiểm).</summary>
    public int LastNewlyCompleted { get; private set; }

    public event EventHandler? Changed;

    public async Task ReloadAsync()
    {
        var board = await App.Host.Get<ChallengeService>().RefreshAsync();
        LastNewlyCompleted = board.NewlyCompleted.Count;
        _panel.Children.Clear();

        foreach (var item in board.Items)
        {
            _panel.Children.Add(BuildCard(item, board.NewlyCompleted.Any(n => n.Challenge.Id == item.Challenge.Id)));
        }

        Visibility = _panel.Children.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private UIElement BuildCard(ChallengeProgress p, bool justCompleted)
    {
        var service = App.Host.Get<ChallengeService>();
        var c = p.Challenge;
        var template = ChallengeCatalog.Find(c.TemplateId);
        var head = new Grid { ColumnSpacing = 10 };
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.ColumnDefinitions.Add(new ColumnDefinition());
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.Children.Add(new TextBlock { Text = template?.Emoji ?? "🏁", FontSize = 24, VerticalAlignment = VerticalAlignment.Center });

        var status = p.State switch
        {
            ChallengeState.Completed => (justCompleted ? "🎉 " : "🏅 ") + Ui.Loc.Format("challenge.completed", c.Days),
            ChallengeState.Failed => p.FailedOn is { } on
                ? Ui.Loc.Format("challenge.failed", on.ToString("dd/MM", Ui.CurrentCulture))
                : Ui.T("challenge.closed"),
            _ => Ui.Loc.Format("challenge.day", p.DayNumber, c.Days) + " · " + Ui.Loc.Format("challenge.left", p.DaysLeft)
        };

        var titles = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        titles.Children.Add(new TextBlock { Text = c.Title, Style = (Style)Application.Current.Resources["CardTitleTextStyle"], TextTrimming = TextTrimming.CharacterEllipsis });
        titles.Children.Add(new TextBlock { Text = status, Style = (Style)Application.Current.Resources["BodySecondaryTextStyle"], TextWrapping = TextWrapping.Wrap });
        Grid.SetColumn(titles, 1);
        head.Children.Add(titles);

        if (p.State != ChallengeState.Active)
        {
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

            if (p.State == ChallengeState.Failed && p.FailedOn is not null)
            {
                var again = new Button { Content = Ui.T("challenge.restart"), Style = (Style)Application.Current.Resources["GradientButtonStyle"] };
                AutomationProperties.SetName(again, $"{Ui.T("challenge.restart")}: {c.Title}");
                again.Click += async (_, _) =>
                {
                    await service.RestartAsync(c.Id);
                    await ReloadAsync();
                    Changed?.Invoke(this, EventArgs.Empty);
                };
                actions.Children.Add(again);
            }

            var dismiss = new Button { Content = Ui.T("challenge.dismiss"), Style = (Style)Application.Current.Resources["GhostButtonStyle"] };
            AutomationProperties.SetName(dismiss, $"{Ui.T("challenge.dismiss")}: {c.Title}");
            dismiss.Click += async (_, _) =>
            {
                await service.DismissAsync(c.Id);
                await ReloadAsync();
            };
            actions.Children.Add(dismiss);
            Grid.SetColumn(actions, 2);
            head.Children.Add(actions);
        }

        var track = new Grid { Height = 8, CornerRadius = new CornerRadius(4), Background = (Brush)Application.Current.Resources["HeatEmptyBrush"] };
        var fill = new Border { CornerRadius = new CornerRadius(4), HorizontalAlignment = HorizontalAlignment.Left, Background = (Brush)Application.Current.Resources["AccentGradientBrush"] };
        track.Children.Add(fill);
        track.SizeChanged += (_, e) => fill.Width = e.NewSize.Width * p.Fraction;
        AutomationProperties.SetName(track, $"{c.Title}: {Math.Round(p.Fraction * 100)}%");

        var stack = new StackPanel { Spacing = 10 };
        stack.Children.Add(head);
        stack.Children.Add(track);
        return new Border { Style = (Style)Application.Current.Resources["CardBorder"], Child = stack };
    }
}
