using HabitGrid.Core.Domain;
using HabitGrid.Presentation.ViewModels;
using HabitGrid.Presentation.Services;
using HabitGrid.WinUI.Controls;
using HabitGrid.WinUI.Plus;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace HabitGrid.WinUI.Pages;

/// <summary>Cài đặt → Màu nhấn riêng (P5, Plus): chọn màu gợi ý hoặc nhập mã hex, áp ngay, đặt lại về màu của theme.</summary>
public sealed partial class SettingsPage
{
    private void BuildAccentCard()
    {
        var plus = App.Host.Get<PlusService>();
        var card = Card(Ui.T("settings.accent"));
        card.Children.Add(new TextBlock { Text = Ui.T("settings.accent.hint"), Style = Res("BodySecondaryTextStyle"), TextWrapping = TextWrapping.Wrap });

        var status = new TextBlock { Style = Res("BodySecondaryTextStyle"), TextWrapping = TextWrapping.Wrap };
        var hexBox = new TextBox { PlaceholderText = "#RRGGBB", Width = 140, MaxLength = 7, Text = _theme.AccentColor ?? string.Empty };
        AutomationProperties.SetName(hexBox, Ui.T("settings.accent.hex"));

        void Refresh() => status.Text = _theme.AccentColor is null
            ? Ui.T("settings.accent.default")
            : Ui.Loc.Format("settings.accent.current", _theme.AccentColor) + (plus.Status.IsPlus ? string.Empty : " " + Ui.T("settings.accent.expired"));

        async Task<bool> EnsurePlusAsync()
        {
            if (plus.Status.IsPlus)
            {
                return true;
            }

            // Bấm vào tính năng Plus thì mới mở paywall.
            if (await PaywallDialog.ShowAsync(XamlRoot, PaywallSource.Settings))
            {
                _theme.AccentAllowed = true;
                return true;
            }

            return false;
        }

        async Task ChooseAsync(string? hex)
        {
            if (hex is not null && !await EnsurePlusAsync())
            {
                return;
            }

            if (!await _theme.SetAccentAsync(hex, XamlRoot.Content as FrameworkElement))
            {
                status.Text = Ui.T("settings.accent.invalid");
                return;
            }

            hexBox.Text = _theme.AccentColor ?? string.Empty;
            Refresh();
        }

        var swatches = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

        foreach (var preset in AccentColors.Presets)
        {
            var button = new Button
            {
                Width = 36,
                Height = 36,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(18),
                Background = Ui.Brush(preset),
                Content = string.Empty
            };
            AutomationProperties.SetName(button, Ui.Loc.Format("settings.accent.pick", preset));
            ToolTipService.SetToolTip(button, preset);
            button.Click += async (_, _) => await ChooseAsync(preset);
            swatches.Children.Add(button);
        }

        var apply = new Button { Content = Ui.T("settings.accent.apply"), Style = Res("GradientButtonStyle") };
        apply.Click += async (_, _) => await ChooseAsync(hexBox.Text);
        var reset = new Button { Content = Ui.T("settings.accent.reset"), Style = Res("GhostButtonStyle") };
        reset.Click += async (_, _) => await ChooseAsync(null);

        var custom = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        custom.Children.Add(hexBox);
        custom.Children.Add(apply);
        custom.Children.Add(reset);

        card.Children.Add(swatches);
        card.Children.Add(custom);
        card.Children.Add(status);
        Refresh();

        // Phông chữ: có hiệu lực ở lần mở app sau (phông được nạp trước khi giao diện dựng).
        card.Children.Add(new TextBlock { Text = Ui.T("settings.font"), Style = Res("SectionHeader"), Margin = new Thickness(0, 6, 0, 0) });
        card.Children.Add(new TextBlock { Text = Ui.T("settings.font.hint"), Style = Res("BodySecondaryTextStyle"), TextWrapping = TextWrapping.Wrap });
        var fontStatus = new TextBlock { Style = Res("BodySecondaryTextStyle"), TextWrapping = TextWrapping.Wrap };
        var fonts = new ChipGroup { MinChipWidth = 150, MaxWidth = 640, HorizontalAlignment = HorizontalAlignment.Left };

        foreach (var font in AppFonts.All)
        {
            fonts.Add(Ui.T("settings.font." + font.Id));
        }

        var savedFont = AppFonts.Resolve(App.Host.Get<ISettingsStore>().GetAsync(AppFonts.SettingKey).GetAwaiter().GetResult()).Id;
        fonts.SelectedIndex = Math.Max(0, AppFonts.All.ToList().FindIndex(f => f.Id == savedFont));
        var selectedFont = fonts.SelectedIndex;
        fonts.SelectionChanged += async (_, _) =>
        {
            var choice = AppFonts.All[fonts.SelectedIndex];

            if (choice.Id != AppFonts.DefaultId && !await EnsurePlusAsync())
            {
                fonts.SelectedIndex = selectedFont;
                return;
            }

            selectedFont = fonts.SelectedIndex;
            await App.Host.Get<ISettingsStore>().SetAsync(AppFonts.SettingKey, choice.Id);
            FontResources.WriteChoice(choice.Id);
            fontStatus.Text = Ui.T("settings.font.restart");
        };
        card.Children.Add(fonts);
        card.Children.Add(fontStatus);
        Wrap(card);
    }
}
