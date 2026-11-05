using HabitGrid.Core.Domain;
using HabitGrid.Core.Domain.Routines;
using HabitGrid.Presentation.Services;
using HabitGrid.WinUI.Controls;
using HabitGrid.WinUI.Plus;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace HabitGrid.WinUI.Pages;

/// <summary>Quản lý routine (F1): danh sách routine, tạo/sửa/xóa. Free tối đa 2 routine; muốn thêm thì mở paywall (vì người dùng bấm Tạo).</summary>
public static class RoutineManagerDialog
{
    /// <summary>Trả về true nếu có thay đổi (để trang nạp lại).</summary>
    public static async Task<bool> ShowAsync(XamlRoot root)
    {
        var service = App.Host.Get<RoutineService>();
        var changed = false;
        var list = new StackPanel { Spacing = 8 };
        ContentDialog? dialog = null;

        async Task RenderAsync()
        {
            list.Children.Clear();
            var routines = await service.ListAsync();

            if (routines.Count == 0)
            {
                list.Children.Add(new TextBlock { Text = Ui.T("routine.empty"), TextWrapping = TextWrapping.Wrap, Style = (Style)Application.Current.Resources["BodySecondaryTextStyle"] });
            }

            foreach (var routine in routines)
            {
                var row = new Grid { ColumnSpacing = 8 };
                row.ColumnDefinitions.Add(new ColumnDefinition());
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.Children.Add(new TextBlock
                {
                    Text = $"{Ui.IconText(routine.Icon.Value)}  {routine.Name} · {Ui.Loc.Format("routine.steps", routine.HabitIds.Count)}",
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis
                });

                var edit = new Button { Content = Ui.T("habits.edit"), Style = (Style)Application.Current.Resources["GhostButtonStyle"] };
                AutomationProperties.SetName(edit, $"{Ui.T("habits.edit")}: {routine.Name}");
                edit.Click += async (_, _) =>
                {
                    dialog?.Hide();
                    changed |= await RoutineEditorDialog.ShowAsync(root, routine);
                    await ReopenAsync();
                };
                Grid.SetColumn(edit, 1);
                row.Children.Add(edit);

                var delete = new Button { Content = Ui.T("habits.delete"), Style = (Style)Application.Current.Resources["GhostButtonStyle"] };
                AutomationProperties.SetName(delete, $"{Ui.T("habits.delete")}: {routine.Name}");
                delete.Click += async (_, _) =>
                {
                    await service.DeleteAsync(routine.Id);
                    changed = true;
                    await RenderAsync();
                };
                Grid.SetColumn(delete, 2);
                row.Children.Add(delete);
                list.Children.Add(row);
            }
        }

        async Task ReopenAsync() => await ShowInnerAsync();

        async Task ShowInnerAsync()
        {
            await RenderAsync();
            var create = new Button { Content = "＋ " + Ui.T("routine.create"), Style = (Style)Application.Current.Resources["GradientButtonStyle"] };
            var limit = new TextBlock { Text = Ui.Loc.Format("routine.limit", service.MaxRoutines == int.MaxValue ? "∞" : service.MaxRoutines.ToString()), Style = (Style)Application.Current.Resources["BodySecondaryTextStyle"] };
            var panel = new StackPanel { Spacing = 12, MinWidth = 420 };
            panel.Children.Add(new TextBlock { Text = Ui.T("routine.hint"), TextWrapping = TextWrapping.Wrap });
            panel.Children.Add(list);
            panel.Children.Add(create);
            panel.Children.Add(limit);

            dialog = new ContentDialog { XamlRoot = root, Title = Ui.T("routine.title"), Content = panel, CloseButtonText = Ui.T("plus.close") };
            create.Click += async (_, _) =>
            {
                var routines = await service.ListAsync();

                if (routines.Count >= service.MaxRoutines)
                {
                    dialog.Hide();
                    await PaywallDialog.ShowAsync(root, HabitGrid.Presentation.ViewModels.PaywallSource.Menu);

                    if (routines.Count >= service.MaxRoutines)
                    {
                        await ReopenAsync();
                        return;
                    }
                }

                dialog.Hide();
                changed |= await RoutineEditorDialog.ShowAsync(root, null);
                await ReopenAsync();
            };

            await dialog.ShowAsync();
        }

        await ShowInnerAsync();
        return changed;
    }
}

/// <summary>Soạn một routine: tên, biểu tượng, khung giờ, chọn thói quen và thứ tự các bước.</summary>
public static class RoutineEditorDialog
{
    private static readonly string[] Icons = ["🌅", "🌙", "☀️", "🏃", "🧘", "💼", "🏠", "🍳"];
    private static readonly string[] TimeKeys = ["group.anytime", "group.morning", "group.afternoon", "group.evening"];

    public static async Task<bool> ShowAsync(XamlRoot root, Routine? editing)
    {
        var service = App.Host.Get<RoutineService>();
        var available = (await service.AvailableHabitsAsync(editing?.Id)).ToDictionary(h => h.Id);
        var order = new List<Guid>(editing?.HabitIds.Where(available.ContainsKey) ?? []);
        var icon = editing?.Icon.Value is { } current && current.StartsWith("emoji:") ? current["emoji:".Length..] : Icons[0];
        var saved = false;

        var name = new TextBox { Header = Ui.T("routine.name"), Text = editing?.Name ?? string.Empty, MaxLength = RoutineRules.MaxNameLength, PlaceholderText = Ui.T("routine.name.placeholder") };
        var iconRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        var iconButtons = new List<ToggleButton>();

        foreach (var emoji in Icons)
        {
            var button = new ToggleButton { Content = emoji, FontSize = 20, IsChecked = emoji == icon, Padding = new Thickness(8, 4, 8, 4) };
            AutomationProperties.SetName(button, emoji);
            button.Click += (_, _) =>
            {
                icon = emoji;
                iconButtons.ForEach(b => b.IsChecked = ReferenceEquals(b, button));
            };
            iconButtons.Add(button);
            iconRow.Children.Add(button);
        }

        var time = new ChipGroup { MinChipWidth = 100 };
        foreach (var key in TimeKeys)
        {
            time.Add(Ui.T(key));
        }

        time.SelectedIndex = (int)(editing?.TimeOfDay ?? TimeOfDay.Morning);

        var picker = new StackPanel { Spacing = 2 };
        var stepsHost = new StackPanel { Spacing = 4 };
        var error = new TextBlock { Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["WarningBrush"], TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
        var count = new TextBlock { Style = (Style)Application.Current.Resources["BodySecondaryTextStyle"] };

        void RenderSteps()
        {
            stepsHost.Children.Clear();

            for (var i = 0; i < order.Count; i++)
            {
                var index = i;
                var row = new Grid { ColumnSpacing = 6 };
                row.ColumnDefinitions.Add(new ColumnDefinition());
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.Children.Add(new TextBlock { Text = $"{i + 1}. {Ui.IconText(available[order[i]].Icon.Value)} {available[order[i]].Name}", VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });

                var up = new Button { Content = "↑", IsEnabled = i > 0, Padding = new Thickness(8, 2, 8, 2) };
                AutomationProperties.SetName(up, $"{Ui.T("habits.moveup")}: {available[order[i]].Name}");
                up.Click += (_, _) => { (order[index - 1], order[index]) = (order[index], order[index - 1]); RenderSteps(); };
                Grid.SetColumn(up, 1);
                row.Children.Add(up);

                var down = new Button { Content = "↓", IsEnabled = i < order.Count - 1, Padding = new Thickness(8, 2, 8, 2) };
                AutomationProperties.SetName(down, $"{Ui.T("habits.movedown")}: {available[order[i]].Name}");
                down.Click += (_, _) => { (order[index + 1], order[index]) = (order[index], order[index + 1]); RenderSteps(); };
                Grid.SetColumn(down, 2);
                row.Children.Add(down);
                stepsHost.Children.Add(row);
            }

            count.Text = Ui.Loc.Format("routine.selected", order.Count, RoutineRules.MaxHabits);
        }

        foreach (var habit in available.Values.OrderBy(h => h.SortKey, StringComparer.Ordinal))
        {
            var box = new CheckBox { Content = $"{Ui.IconText(habit.Icon.Value)}  {habit.Name}", IsChecked = order.Contains(habit.Id) };
            box.Click += (_, _) =>
            {
                if (box.IsChecked == true && !order.Contains(habit.Id))
                {
                    order.Add(habit.Id);
                }
                else
                {
                    order.Remove(habit.Id);
                }

                RenderSteps();
            };
            picker.Children.Add(box);
        }

        RenderSteps();

        var panel = new StackPanel { Spacing = 12, MinWidth = 440 };
        panel.Children.Add(name);
        panel.Children.Add(iconRow);
        panel.Children.Add(time);
        panel.Children.Add(new TextBlock { Text = Ui.T("routine.pick"), Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"] });
        panel.Children.Add(available.Count == 0
            ? new TextBlock { Text = Ui.T("routine.nohabits"), TextWrapping = TextWrapping.Wrap, Style = (Style)Application.Current.Resources["BodySecondaryTextStyle"] }
            : new ScrollViewer { Content = picker, MaxHeight = 170 });
        panel.Children.Add(new TextBlock { Text = Ui.T("routine.order"), Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"] });
        panel.Children.Add(stepsHost);
        panel.Children.Add(count);
        panel.Children.Add(error);

        var dialog = new ContentDialog
        {
            XamlRoot = root,
            Title = Ui.T(editing is null ? "routine.create" : "routine.edit"),
            Content = new ScrollViewer { Content = panel, MaxHeight = 620 },
            PrimaryButtonText = Ui.T("editor.save"),
            CloseButtonText = Ui.T("editor.cancel"),
            DefaultButton = ContentDialogButton.Primary
        };

        dialog.PrimaryButtonClick += async (_, args) =>
        {
            var deferral = args.GetDeferral();

            try
            {
                var draft = new RoutineDraft(name.Text, HabitIcon.Emoji(icon), (TimeOfDay)Math.Max(0, time.SelectedIndex), order);
                var result = editing is null ? await service.CreateAsync(draft) : await service.UpdateAsync(editing.Id, draft);

                if (result.Succeeded)
                {
                    saved = true;
                }
                else
                {
                    args.Cancel = true;
                    error.Text = Ui.T("routine.err." + result.Error);
                    error.Visibility = Visibility.Visible;
                }
            }
            finally
            {
                deferral.Complete();
            }
        };

        await dialog.ShowAsync();
        return saved;
    }
}
