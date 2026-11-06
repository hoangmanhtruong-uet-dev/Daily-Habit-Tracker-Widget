using HabitGrid.Core.Domain.Challenges;
using HabitGrid.Presentation.Services;
using HabitGrid.WinUI.Controls;
using HabitGrid.WinUI.Plus;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace HabitGrid.WinUI.Pages;

/// <summary>Chọn thử thách mẫu và số ngày (F2). Quá giới hạn Free thì mở paywall vì người dùng vừa bấm Bắt đầu.</summary>
public static class ChallengeDialog
{
    /// <summary>Trả về true nếu đã bắt đầu một thử thách (để trang nạp lại).</summary>
    public static async Task<bool> ShowAsync(XamlRoot root)
    {
        var service = App.Host.Get<ChallengeService>();
        var started = false;
        var selected = ChallengeCatalog.Templates[0];
        var days = selected.DefaultDays;
        var message = new TextBlock { TextWrapping = TextWrapping.Wrap, Style = (Style)Application.Current.Resources["BodySecondaryTextStyle"] };

        var templates = new ChipGroup { MinChipWidth = 200, MaxWidth = 520, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var t in ChallengeCatalog.Templates)
        {
            templates.Add($"{t.Emoji} {Ui.T("challenge." + t.Id)}");
        }

        templates.SelectedIndex = 0;

        var dayChips = new ChipGroup { MinChipWidth = 80, MaxWidth = 520, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var d in ChallengeCatalog.DayChoices)
        {
            dayChips.Add(Ui.Loc.Format("challenge.days", d));
        }

        void SyncDays() => dayChips.SelectedIndex = Math.Max(0, ChallengeCatalog.DayChoices.ToList().IndexOf(days));

        templates.SelectionChanged += (_, _) =>
        {
            if (templates.SelectedIndex >= 0)
            {
                selected = ChallengeCatalog.Templates[templates.SelectedIndex];
                days = selected.DefaultDays;
                SyncDays();
            }
        };
        dayChips.SelectionChanged += (_, _) =>
        {
            if (dayChips.SelectedIndex >= 0)
            {
                days = ChallengeCatalog.DayChoices[dayChips.SelectedIndex];
            }
        };
        SyncDays();

        ContentDialog? dialog = null;
        var badges = await service.CompletedAsync();
        var panel = new StackPanel { Spacing = 12, MinWidth = 440 };
        panel.Children.Add(new TextBlock { Text = Ui.T("challenge.hint"), TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(templates);
        panel.Children.Add(new TextBlock { Text = Ui.T("challenge.length"), Style = (Style)Application.Current.Resources["SectionHeader"] });
        panel.Children.Add(dayChips);
        panel.Children.Add(message);
        var unlock = new Button { Content = Ui.T("challenge.unlock"), Style = (Style)Application.Current.Resources["GhostButtonStyle"], Visibility = Visibility.Collapsed };
        unlock.Click += async (_, _) =>
        {
            dialog?.Hide();
            await PaywallDialog.ShowAsync(root, HabitGrid.Presentation.ViewModels.PaywallSource.Menu);
        };
        panel.Children.Add(unlock);

        if (badges.Count > 0)
        {
            panel.Children.Add(new TextBlock { Text = Ui.T("challenge.badges"), Style = (Style)Application.Current.Resources["SectionHeader"] });
            panel.Children.Add(new TextBlock
            {
                Text = string.Join("   ", badges.Take(6).Select(b => $"🏅 {b.Title} · {b.Days}")),
                TextWrapping = TextWrapping.Wrap
            });
        }

        dialog = new ContentDialog
        {
            XamlRoot = root,
            Title = Ui.T("challenge.title"),
            Content = panel,
            PrimaryButtonText = Ui.T("challenge.start"),
            CloseButtonText = Ui.T("plus.close"),
            DefaultButton = ContentDialogButton.Primary
        };
        AutomationProperties.SetName(dialog, Ui.T("challenge.title"));

        dialog.PrimaryButtonClick += async (_, args) =>
        {
            var deferral = args.GetDeferral();

            try
            {
                var result = await service.StartAsync(selected.Id, Ui.T("challenge." + selected.Id), days);

                unlock.Visibility = result.Error == ChallengeError.ActiveLimit ? Visibility.Visible : Visibility.Collapsed;

                if (result.Succeeded)
                {
                    started = true;
                    return;
                }

                args.Cancel = true;
                message.Text = Ui.T("challenge.err." + result.Error);
            }
            finally
            {
                deferral.Complete();
            }
        };

        await dialog.ShowAsync();
        return started;
    }
}
