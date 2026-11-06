using HabitGrid.Core.Domain;
using HabitGrid.Core.Domain.Insights;
using HabitGrid.Presentation.Services;
using HabitGrid.Presentation.ViewModels;
using HabitGrid.WinUI.Controls;
using HabitGrid.WinUI.Plus;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace HabitGrid.WinUI.Moments;

/// <summary>
/// Xem "Wrapped" tháng như story: 6 slide, chuyển bằng nút. Free chỉ xem slide đầu (các slide sau hiện khóa, bấm mở khóa thì mở paywall);
/// Plus xem đủ và lưu từng slide thành PNG 1080×1920. App không đăng lên mạng xã hội: người dùng tự đăng ảnh đã lưu.
/// </summary>
public static class WrappedDialog
{
    public static async Task ShowAsync(XamlRoot root)
    {
        var service = App.Host.Get<WrappedService>();
        var monthsAgo = 1;
        var index = 0;
        MonthlyWrapped data = await service.BuildAsync(monthsAgo);
        var buddy = await LoadBuddyAsync();
        var holder = new Border { Height = 520, HorizontalAlignment = HorizontalAlignment.Center };
        var dots = new TextBlock { HorizontalAlignment = HorizontalAlignment.Center, FontSize = 14 };
        var status = new TextBlock { Style = (Style)Application.Current.Resources["BodySecondaryTextStyle"], TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
        FrameworkElement? current = null;

        var months = new ChipGroup { MinChipWidth = 130, MaxWidth = 300, HorizontalAlignment = HorizontalAlignment.Center };
        months.Add(Ui.T("wrapped.lastMonth"));
        months.Add(Ui.T("wrapped.thisMonth"));
        months.SelectedIndex = 0;

        var prev = new Button { Content = "‹", Width = 48, Style = (Style)Application.Current.Resources["GhostButtonStyle"] };
        var next = new Button { Content = "›", Width = 48, Style = (Style)Application.Current.Resources["GhostButtonStyle"] };
        AutomationProperties.SetName(prev, Ui.T("wrapped.prev"));
        AutomationProperties.SetName(next, Ui.T("wrapped.next"));

        async Task RenderAsync()
        {
            var kind = WrappedSlides.All[index];
            dots.Text = string.Join(" ", WrappedSlides.All.Select((_, i) => i == index ? "●" : "○"));
            prev.IsEnabled = index > 0;
            next.IsEnabled = index < WrappedSlides.All.Count - 1;

            if (!WrappedSlides.CanView(index, service.IsPlus))
            {
                current = null;
                holder.Child = BuildLocked(root, async () =>
                {
                    await PaywallDialog.ShowAsync(root, PaywallSource.Menu);
                    await RenderAsync();
                });
                return;
            }

            BitmapImage? photo = null;

            if (kind == WrappedSlideKind.Moment)
            {
                photo = await TryLoadMonthPhotoAsync(data);
            }

            current = WrappedSlideBuilder.Build(kind, data, buddy, photo);
            holder.Child = new Viewbox { Child = current, Stretch = Stretch.Uniform };
        }

        prev.Click += async (_, _) => { index = Math.Max(0, index - 1); await RenderAsync(); };
        next.Click += async (_, _) => { index = Math.Min(WrappedSlides.All.Count - 1, index + 1); await RenderAsync(); };
        months.SelectionChanged += async (_, _) =>
        {
            monthsAgo = months.SelectedIndex == 0 ? 1 : 0;
            data = await service.BuildAsync(monthsAgo);
            index = 0;
            await RenderAsync();
        };

        var nav = new Grid { ColumnSpacing = 8 };
        nav.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        nav.ColumnDefinitions.Add(new ColumnDefinition());
        nav.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        nav.Children.Add(prev);
        Grid.SetColumn(dots, 1);
        dots.VerticalAlignment = VerticalAlignment.Center;
        nav.Children.Add(dots);
        Grid.SetColumn(next, 2);
        nav.Children.Add(next);

        var panel = new StackPanel { Spacing = 10, MinWidth = 340 };
        panel.Children.Add(months);
        panel.Children.Add(holder);
        panel.Children.Add(nav);
        panel.Children.Add(status);

        var dialog = new ContentDialog
        {
            XamlRoot = root,
            Title = Ui.T("wrapped.title"),
            Content = new ScrollViewer { Content = panel, MaxHeight = 760 },
            PrimaryButtonText = Ui.T("share.save"),
            CloseButtonText = Ui.T("plus.close")
        };

        dialog.PrimaryButtonClick += async (_, args) =>
        {
            args.Cancel = true;

            if (!service.IsPlus)
            {
                await PaywallDialog.ShowAsync(root, PaywallSource.Menu);
                return;
            }

            if (current is null)
            {
                return;
            }

            var deferral = args.GetDeferral();

            try
            {
                var path = await SaveAsync(current, data, index);
                status.Text = Ui.Loc.Format("share.saved", path);
                status.Visibility = Visibility.Visible;
                await App.Host.Get<GamificationService>().SetFlagAsync("shared_card");
            }
            catch (Exception ex)
            {
                App.Host.Get<IAppLogger>().Error("Lưu Wrapped lỗi", ex);
                status.Text = Ui.T("share.failed");
                status.Visibility = Visibility.Visible;
            }
            finally
            {
                deferral.Complete();
            }
        };

        await RenderAsync();
        await dialog.ShowAsync();
    }

    /// <summary>Render một slide thành PNG 1080×1920 (smoke test dùng để kiểm xuất ảnh mà không cần mở hộp thoại).</summary>
    public static async Task<byte[]> RenderSlidePngForTestAsync(WrappedSlideKind kind, MonthlyWrapped data, bool withPhoto)
    {
        var buddy = await LoadBuddyAsync();
        var photo = withPhoto ? await TryLoadMonthPhotoAsync(data) : null;
        return await MomentCapture.RenderPngAsync(WrappedSlideBuilder.Build(kind, data, buddy, photo), 1080, 1920);
    }

    private static async Task<string> SaveAsync(FrameworkElement slide, MonthlyWrapped data, int index)
    {
        var bytes = await MomentCapture.RenderPngAsync(slide, 1080, 1920);
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "HabitGrid");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"habitgrid-wrapped-{data.Year}-{data.Month:00}-{index + 1}.png");
        await File.WriteAllBytesAsync(path, bytes);
        return path;
    }

    private static UIElement BuildLocked(XamlRoot root, Func<Task> unlock)
    {
        var stack = new StackPanel { Spacing = 12, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, MaxWidth = 280 };
        stack.Children.Add(new TextBlock { Text = "🔒", FontSize = 64, HorizontalAlignment = HorizontalAlignment.Center });
        stack.Children.Add(new TextBlock { Text = Ui.T("wrapped.locked"), TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });
        var button = new Button { Content = Ui.T("wrapped.unlock"), Style = (Style)Application.Current.Resources["GradientButtonStyle"], HorizontalAlignment = HorizontalAlignment.Center };
        AutomationProperties.SetName(button, Ui.T("wrapped.unlock"));
        button.Click += async (_, _) => await unlock();
        stack.Children.Add(button);
        return new Border { CornerRadius = new CornerRadius(24), Background = (Brush)Application.Current.Resources["CardIdleBrush"], Width = 292, Height = 520, Child = stack };
    }

    private static async Task<WrappedBuddy> LoadBuddyAsync()
    {
        var profile = await App.Host.Get<IProfileRepository>().GetAsync();
        var emoji = BuddyViewModel.StageFor(profile.Level) switch
        {
            BuddyStage.Egg => "🥚",
            BuddyStage.Baby => "🐣",
            BuddyStage.Teen => "🐥",
            BuddyStage.Adult => "🦉",
            _ => "🐉"
        };
        return new WrappedBuddy(profile.BuddyName, profile.Level, emoji);
    }

    /// <summary>Ảnh khoảnh khắc mới nhất trong tháng (nếu có và đọc được).</summary>
    private static async Task<BitmapImage?> TryLoadMonthPhotoAsync(MonthlyWrapped data)
    {
        try
        {
            var photo = App.Host.Get<MomentService>().Store.All()
                .Where(m => m.Day >= data.From && m.Day <= data.To && File.Exists(m.Path))
                .OrderByDescending(m => m.Day)
                .FirstOrDefault();
            return photo is null ? null : await MomentCapture.LoadNowAsync(photo.Path, 720);
        }
        catch (Exception ex)
        {
            App.Host.Get<IAppLogger>().Error("Đọc ảnh khoảnh khắc cho Wrapped lỗi", ex);
            return null;
        }
    }
}
