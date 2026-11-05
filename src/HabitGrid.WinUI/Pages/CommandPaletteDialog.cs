using HabitGrid.Presentation.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace HabitGrid.WinUI.Pages;

/// <summary>Command palette (Ctrl+K): ô tìm + danh sách lệnh; Enter chạy lệnh đang chọn, ↑/↓ chọn, Esc đóng.</summary>
public static class CommandPaletteDialog
{
    public static async Task<PaletteCommand?> ShowAsync(XamlRoot root, IReadOnlyList<PaletteCommand> commands)
    {
        PaletteCommand? chosen = null;
        var box = new TextBox { PlaceholderText = Ui.T("palette.placeholder"), MinWidth = 460 };
        var list = new ListView { SelectionMode = ListViewSelectionMode.Single, MaxHeight = 380, IsItemClickEnabled = true };
        var empty = new TextBlock { Text = Ui.T("palette.empty"), Visibility = Visibility.Collapsed, Margin = new Thickness(4, 8, 0, 0) };
        IReadOnlyList<PaletteCommand> shown = [];

        void Render()
        {
            shown = CommandPalette.Filter(commands, box.Text);
            list.Items.Clear();

            foreach (var command in shown)
            {
                list.Items.Add(new TextBlock { Text = $"{command.Glyph}  {command.Title}".Trim(), Margin = new Thickness(0, 4, 0, 4) });
            }

            list.SelectedIndex = shown.Count > 0 ? 0 : -1;
            empty.Visibility = shown.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(box);
        panel.Children.Add(list);
        panel.Children.Add(empty);

        var dialog = new ContentDialog
        {
            XamlRoot = root,
            Title = Ui.T("palette.title"),
            Content = panel,
            CloseButtonText = Ui.T("detail.day.cancel")
        };

        box.TextChanged += (_, _) => Render();
        box.KeyDown += (_, e) =>
        {
            switch (e.Key)
            {
                case VirtualKey.Down when shown.Count > 0:
                    list.SelectedIndex = Math.Min(list.SelectedIndex + 1, shown.Count - 1);
                    list.ScrollIntoView(list.SelectedItem);
                    e.Handled = true;
                    break;
                case VirtualKey.Up when shown.Count > 0:
                    list.SelectedIndex = Math.Max(list.SelectedIndex - 1, 0);
                    list.ScrollIntoView(list.SelectedItem);
                    e.Handled = true;
                    break;
                case VirtualKey.Enter when list.SelectedIndex >= 0:
                    chosen = shown[list.SelectedIndex];
                    e.Handled = true;
                    dialog.Hide();
                    break;
            }
        };
        list.ItemClick += (_, e) =>
        {
            var index = list.Items.IndexOf(e.ClickedItem);

            if (index >= 0 && index < shown.Count)
            {
                chosen = shown[index];
                dialog.Hide();
            }
        };
        dialog.Opened += (_, _) => box.Focus(FocusState.Programmatic);

        Render();
        await dialog.ShowAsync();
        return chosen;
    }
}
