using HabitGrid.Core.Domain;
using HabitGrid.Presentation.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HabitGrid.WinUI.Pages;

/// <summary>Hộp thoại thêm thói quen nhanh (tên, emoji, loại, mục tiêu, khung giờ). Trình tạo đầy đủ làm ở gói sau.</summary>
public static class AddHabitDialog
{
    private static readonly string[] EmojiChoices = ["✨", "📚", "💧", "🏃", "🧘", "💪", "🍎", "😴", "🎸", "✍️", "💻", "🌱"];

    /// <summary>Trả về true nếu đã tạo được thói quen.</summary>
    public static async Task<bool> ShowAsync(XamlRoot xamlRoot)
    {
        var nameBox = new TextBox { PlaceholderText = Ui.T("add.name"), MaxLength = HabitValidator.MaxNameLength };

        var emojiBox = new ComboBox { Header = null, SelectedIndex = 0, MinWidth = 72 };
        foreach (var emoji in EmojiChoices)
        {
            emojiBox.Items.Add(emoji);
        }

        var kindBox = new ComboBox { Header = Ui.T("add.kind"), HorizontalAlignment = HorizontalAlignment.Stretch, SelectedIndex = 0 };
        foreach (var key in new[] { "add.kind.boolean", "add.kind.count", "add.kind.duration", "add.kind.quit" })
        {
            kindBox.Items.Add(Ui.T(key));
        }

        var targetBox = new NumberBox
        {
            Header = Ui.T("add.target"),
            Minimum = 1,
            Maximum = 10000,
            Value = 1,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            Visibility = Visibility.Collapsed
        };
        kindBox.SelectionChanged += (_, _) =>
            targetBox.Visibility = kindBox.SelectedIndex is 1 or 2 ? Visibility.Visible : Visibility.Collapsed;

        var timeBox = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, SelectedIndex = 0 };
        foreach (var key in new[] { "group.anytime", "group.morning", "group.afternoon", "group.evening" })
        {
            timeBox.Items.Add(Ui.T(key));
        }

        var errorText = new TextBlock
        {
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["WarningBrush"],
            TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed
        };

        var nameRow = new Grid { ColumnSpacing = 8, ColumnDefinitions = { new ColumnDefinition { Width = GridLength.Auto }, new ColumnDefinition() } };
        nameRow.Children.Add(emojiBox);
        Grid.SetColumn(nameBox, 1);
        nameRow.Children.Add(nameBox);

        var panel = new StackPanel { Spacing = 14, MinWidth = 360 };
        panel.Children.Add(nameRow);
        panel.Children.Add(kindBox);
        panel.Children.Add(targetBox);
        panel.Children.Add(timeBox);
        panel.Children.Add(errorText);

        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = Ui.T("add.title"),
            Content = panel,
            PrimaryButtonText = Ui.T("add.save"),
            CloseButtonText = Ui.T("add.cancel"),
            DefaultButton = ContentDialogButton.Primary
        };

        var created = false;
        var creation = App.Host.Get<HabitCreationService>();

        dialog.PrimaryButtonClick += async (_, args) =>
        {
            var deferral = args.GetDeferral();

            try
            {
                var kind = (HabitKind)kindBox.SelectedIndex;
                var draft = new HabitDraft(
                    nameBox.Text,
                    HabitIcon.Emoji((string)emojiBox.SelectedItem),
                    kind,
                    (int)Math.Max(1, targetBox.Value),
                    null,
                    (TimeOfDay)timeBox.SelectedIndex);

                var result = await creation.CreateAsync(draft);

                if (result.Succeeded)
                {
                    created = true;
                }
                else
                {
                    // Giữ hộp thoại mở và nói rõ lỗi bằng ngôn ngữ của người dùng.
                    args.Cancel = true;
                    errorText.Text = string.Join(" ", result.Errors.Select(e => ValidationText.For(Ui.Loc, e)));
                    errorText.Visibility = Visibility.Visible;
                }
            }
            finally
            {
                deferral.Complete();
            }
        };

        await dialog.ShowAsync();
        return created;
    }
}
