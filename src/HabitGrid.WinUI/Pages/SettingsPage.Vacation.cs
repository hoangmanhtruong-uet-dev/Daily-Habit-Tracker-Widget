using HabitGrid.Presentation.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HabitGrid.WinUI.Pages;

/// <summary>Cài đặt → Tạm nghỉ (F3): trạng thái hiện tại, bắt đầu (mọi thói quen), kết thúc sớm.</summary>
public sealed partial class SettingsPage
{
    private void BuildVacationCard()
    {
        var service = App.Host.Get<VacationService>();
        var card = Card(Ui.T("settings.vacation"));
        card.Children.Add(new TextBlock { Text = Ui.T("settings.vacation.hint"), Style = Res("BodySecondaryTextStyle"), TextWrapping = TextWrapping.Wrap });

        var status = new TextBlock { TextWrapping = TextWrapping.Wrap };
        var start = new Button { Content = Ui.T("vacation.start"), Style = Res("GradientButtonStyle") };
        var end = new Button { Content = Ui.T("vacation.end"), Style = Res("GhostButtonStyle"), Visibility = Visibility.Collapsed };

        async Task RefreshAsync()
        {
            var info = await service.CurrentAsync();
            status.Text = info is null ? Ui.T("settings.vacation.none") : Ui.Loc.Format("settings.vacation.active", Ui.LongDate(info.To));
            start.Visibility = info is null ? Visibility.Visible : Visibility.Collapsed;
            end.Visibility = info is null ? Visibility.Collapsed : Visibility.Visible;
        }

        start.Click += async (_, _) =>
        {
            if (await VacationDialog.ShowAsync(XamlRoot))
            {
                await App.Host.Get<HabitGrid.Presentation.ViewModels.TodayViewModel>().LoadAsync();
                await RefreshAsync();
            }
        };
        end.Click += async (_, _) =>
        {
            await service.EndAsync();
            await App.Host.Get<HabitGrid.Presentation.ViewModels.TodayViewModel>().LoadAsync();
            await RefreshAsync();
        };

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        buttons.Children.Add(start);
        buttons.Children.Add(end);
        card.Children.Add(status);
        card.Children.Add(buttons);
        _ = RefreshAsync();
        Wrap(card);
    }
}
