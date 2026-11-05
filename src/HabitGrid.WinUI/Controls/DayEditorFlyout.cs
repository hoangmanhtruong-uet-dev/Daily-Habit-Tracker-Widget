using HabitGrid.Core.Domain;
using HabitGrid.Presentation.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace HabitGrid.WinUI.Controls;

/// <summary>Flyout sửa một ngày trong lịch: giá trị, bỏ qua, ghi chú, tâm trạng (03-UI-REBUILD-GLOW2 §5.3).</summary>
public static class DayEditorFlyout
{
    private static readonly string[] MoodEmoji = ["😞", "🙁", "😐", "🙂", "😄"];

    /// <summary>Mở flyout cạnh <paramref name="anchor"/>; <paramref name="onSaved"/> chạy sau khi lưu thành công.</summary>
    public static async Task ShowAsync(FrameworkElement anchor, HabitDetailViewModel vm, DateOnly day, Action onSaved)
    {
        if (vm.Habit is not { } habit)
        {
            return;
        }

        var existing = await vm.GetDayAsync(day);
        var isBoolean = habit.Kind == HabitKind.Boolean;

        var title = new TextBlock { Text = Ui.LongDate(day), Style = (Style)Application.Current.Resources["CardTitleTextStyle"] };

        var done = new ToggleSwitch
        {
            Header = Ui.T("detail.day.done"),
            OnContent = string.Empty,
            OffContent = string.Empty,
            IsOn = existing is { Status: DayStatus.Done },
            Visibility = isBoolean ? Visibility.Visible : Visibility.Collapsed
        };

        var number = new NumberBox
        {
            Header = Ui.T("detail.day.value"),
            Minimum = 0,
            Maximum = 100000,
            Value = existing?.Value ?? 0,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
            SmallChange = 1,
            LargeChange = 5,
            Visibility = isBoolean ? Visibility.Collapsed : Visibility.Visible
        };

        var skip = new CheckBox { Content = Ui.T("detail.day.skip"), IsChecked = existing is { Status: DayStatus.Skipped } };
        var note = new TextBox
        {
            Header = Ui.T("detail.day.note"),
            Text = existing?.Note ?? string.Empty,
            MaxLength = DayLog.MaxNoteLength,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 76,
            MaxHeight = 140
        };

        int? mood = existing?.Mood;
        var moodButtons = new List<ToggleButton>();
        var moodRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };

        for (var i = 0; i < MoodEmoji.Length; i++)
        {
            var value = i + 1;
            var button = new ToggleButton { Content = MoodEmoji[i], FontSize = 20, Padding = new Thickness(8, 4, 8, 4), IsChecked = mood == value };
            button.Click += (_, _) =>
            {
                mood = mood == value ? null : value;

                for (var j = 0; j < moodButtons.Count; j++)
                {
                    moodButtons[j].IsChecked = mood == j + 1;
                }
            };
            moodButtons.Add(button);
            moodRow.Children.Add(button);
        }

        var error = new TextBlock { Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["WarningBrush"], TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
        var flyout = new Flyout { Placement = FlyoutPlacementMode.Bottom };

        var save = new Button { Content = Ui.T("detail.day.save"), Style = (Style)Application.Current.Resources["GradientButtonStyle"] };
        var cancel = new Button { Content = Ui.T("detail.day.cancel"), Style = (Style)Application.Current.Resources["GhostButtonStyle"] };
        cancel.Click += (_, _) => flyout.Hide();
        save.Click += async (_, _) =>
        {
            var value = isBoolean ? (done.IsOn ? 1 : 0) : (int)Math.Max(0, double.IsNaN(number.Value) ? 0 : number.Value);

            if (await vm.SaveDayAsync(day, value, skip.IsChecked == true, note.Text, mood))
            {
                flyout.Hide();
                onSaved();
            }
            else
            {
                var text = vm.ErrorMessage is { } key ? Ui.T(key) : string.Empty;
                error.Text = text.Length == 0 || text.StartsWith('[') ? Ui.T("error.generic") : text;
                error.Visibility = Visibility.Visible;
            }
        };

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(cancel);
        buttons.Children.Add(save);

        var panel = new StackPanel { Spacing = 12, Width = 300 };
        foreach (UIElement child in new UIElement[] { title, done, number, skip, note, new TextBlock { Text = Ui.T("detail.day.mood"), Style = (Style)Application.Current.Resources["BodySecondaryTextStyle"] }, moodRow, error, buttons })
        {
            panel.Children.Add(child);
        }

        flyout.Content = panel;
        flyout.ShowAt(anchor);
    }
}
