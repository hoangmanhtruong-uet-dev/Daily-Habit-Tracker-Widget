using HabitGrid.Core.Domain.Vacation;
using HabitGrid.Presentation.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace HabitGrid.WinUI.Pages;

/// <summary>Hộp thoại tạm nghỉ (F3): chọn nhanh 3 ngày / 1 tuần / 2 tuần hoặc chọn ngày cuối; áp cho mọi thói quen hoặc một thói quen.</summary>
public static class VacationDialog
{
    /// <summary>Trả về true nếu đã bắt đầu một kỳ tạm nghỉ.</summary>
    public static async Task<bool> ShowAsync(XamlRoot root, Guid? habitId = null, string? habitName = null)
    {
        var service = App.Host.Get<VacationService>();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var max = service.MaxDays;
        var days = Math.Min(7, max);
        var picker = new CalendarDatePicker
        {
            Date = new DateTimeOffset(today.AddDays(days - 1).ToDateTime(TimeOnly.MinValue)),
            MinDate = new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue)),
            MaxDate = new DateTimeOffset(today.AddDays(max - 1).ToDateTime(TimeOnly.MinValue)),
            Header = Ui.T("vacation.until")
        };
        var error = new TextBlock { Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["WarningBrush"], TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
        var chips = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

        foreach (var count in new[] { 3, 7, 14 }.Where(c => c <= max))
        {
            var chip = new Button { Content = Ui.Loc.Format("vacation.days", count), Style = (Style)Application.Current.Resources["GhostButtonStyle"] };
            AutomationProperties.SetName(chip, Ui.Loc.Format("vacation.days", count));
            chip.Click += (_, _) => picker.Date = new DateTimeOffset(today.AddDays(count - 1).ToDateTime(TimeOnly.MinValue));
            chips.Children.Add(chip);
        }

        var panel = new StackPanel { Spacing = 12, MaxWidth = 440 };
        panel.Children.Add(new TextBlock { Text = habitId is null ? Ui.T("vacation.body.all") : Ui.Loc.Format("vacation.body.one", habitName ?? string.Empty), TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(chips);
        panel.Children.Add(picker);
        panel.Children.Add(new TextBlock { Text = Ui.Loc.Format("vacation.limit", max), Style = (Style)Application.Current.Resources["BodySecondaryTextStyle"], TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(error);

        var dialog = new ContentDialog
        {
            XamlRoot = root,
            Title = Ui.T("vacation.title"),
            Content = panel,
            PrimaryButtonText = Ui.T("vacation.start"),
            CloseButtonText = Ui.T("plus.close"),
            DefaultButton = ContentDialogButton.Primary
        };
        var started = false;

        dialog.PrimaryButtonClick += async (_, args) =>
        {
            var deferral = args.GetDeferral();

            try
            {
                var to = DateOnly.FromDateTime((picker.Date ?? DateTimeOffset.Now).DateTime);
                var (check, _) = await service.StartAsync(today, to, habitId is { } id ? [id] : null);

                if (check.IsValid)
                {
                    started = true;
                }
                else
                {
                    args.Cancel = true;
                    error.Text = check.Error == VacationError.TooLong ? Ui.Loc.Format("vacation.err.TooLong", check.MaxDays) : Ui.T("vacation.err." + check.Error);
                    error.Visibility = Visibility.Visible;
                }
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
