using HabitGrid.Core.Domain;
using HabitGrid.Presentation.Services;
using HabitGrid.Presentation.ViewModels;
using HabitGrid.WinUI.Moments;
using HabitGrid.WinUI.Plus;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace HabitGrid.WinUI.Pages;

/// <summary>Khu "Khoảnh khắc" trên trang chi tiết: chụp/chọn ảnh cho hôm nay, xem ảnh cũ, tạo thẻ chia sẻ photobooth.</summary>
public sealed partial class HabitDetailPage
{
    private const int ThumbWidth = 132;

    /// <summary>Thêm ảnh cho hôm nay từ mảng byte (smoke test dùng; giao diện đi qua camera/file).</summary>
    public MomentAddResult AddMomentForTest(byte[] jpeg)
    {
        var result = ViewModel.Habit is { } habit
            ? App.Host.Get<MomentService>().Add(habit, ViewModel.Today, jpeg, ".jpg").Result
            : MomentAddResult.NotAllowed;
        BuildMoments();
        return result;
    }

    public Task OpenShareForTestAsync() => ViewModel.Habit is { } habit ? ShareCardDialog.ShowAsync(XamlRoot, habit.Id) : Task.CompletedTask;

    private void BuildMoments()
    {
        MomentsHost.Children.Clear();

        if (ViewModel.Habit is not { } habit)
        {
            return;
        }

        var service = App.Host.Get<MomentService>();
        var moments = service.Store.ForHabit(habit.Id);

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(new TextBlock { Text = Ui.T("moments.title").ToUpperInvariant(), Style = (Style)Application.Current.Resources["SectionHeader"], Margin = new Thickness(0), VerticalAlignment = VerticalAlignment.Center });

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var capture = Action("📷 " + Ui.T("moments.capture"), Ui.T("moments.capture"), async () => await AddFromAsync(MomentCapture.CaptureAsync));
        var pick = Action("🖼 " + Ui.T("moments.pick"), Ui.T("moments.pick"), async () => await AddFromAsync(MomentCapture.PickAsync));
        var share = Action("🎞 " + Ui.T("moments.share"), Ui.T("moments.share"), async () => await ShareCardDialog.ShowAsync(XamlRoot, habit.Id));
        buttons.Children.Add(capture);
        buttons.Children.Add(pick);
        buttons.Children.Add(share);
        Grid.SetColumn(buttons, 1);
        header.Children.Add(buttons);

        var stack = new StackPanel { Spacing = 12 };
        stack.Children.Add(header);

        if (!service.CanAdd(habit.Id, ViewModel.Today))
        {
            stack.Children.Add(new TextBlock { Text = Ui.T("moments.limit"), Style = (Style)Application.Current.Resources["BodySecondaryTextStyle"], TextWrapping = TextWrapping.Wrap });
        }

        if (moments.Count == 0)
        {
            stack.Children.Add(new TextBlock { Text = Ui.T("moments.empty"), Style = (Style)Application.Current.Resources["BodySecondaryTextStyle"], TextWrapping = TextWrapping.Wrap });
        }
        else
        {
            var strip = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

            foreach (var moment in moments.Take(30))
            {
                var image = new Image { Source = MomentCapture.Thumbnail(moment.Path, ThumbWidth), Width = ThumbWidth, Height = ThumbWidth, Stretch = Microsoft.UI.Xaml.Media.Stretch.UniformToFill };
                var label = Ui.LongDate(moment.Day);
                var tile = new Button { Padding = new Thickness(0), BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(14), Content = new Grid { CornerRadius = new CornerRadius(14), Children = { image } } };
                AutomationProperties.SetName(tile, Ui.Loc.Format("moments.photo", label));
                ToolTipService.SetToolTip(tile, label);
                tile.Click += (_, _) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(moment.Path) { UseShellExecute = true });

                var delete = new MenuFlyoutItem { Text = Ui.T("moments.delete"), Icon = new FontIcon { Glyph = "" } };
                delete.Click += (_, _) =>
                {
                    service.Store.Delete(moment);
                    BuildMoments();
                };
                tile.ContextFlyout = new MenuFlyout { Items = { delete } };
                strip.Children.Add(tile);
            }

            stack.Children.Add(new ScrollViewer { Content = strip, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, HorizontalScrollMode = ScrollMode.Enabled });
        }

        MomentsHost.Children.Add(new Border { Style = (Style)Application.Current.Resources["CardBorder"], Child = stack });
    }

    private static Button Action(string text, string name, Func<Task> run)
    {
        var button = new Button { Content = text, Style = (Style)Application.Current.Resources["GhostButtonStyle"] };
        AutomationProperties.SetName(button, name);
        button.Click += async (_, _) =>
        {
            try
            {
                await run();
            }
            catch (Exception ex)
            {
                App.Host.Get<IAppLogger>().Error($"Khoảnh khắc: {name} lỗi", ex);
            }
        };
        return button;
    }

    /// <summary>Lấy ảnh (camera hoặc file), thu nhỏ, lưu theo quyền. Hết lượt Free thì mở paywall vì người dùng vừa bấm thêm ảnh.</summary>
    private async Task AddFromAsync(Func<Task<Windows.Storage.StorageFile?>> source)
    {
        if (ViewModel.Habit is not { } habit)
        {
            return;
        }

        var service = App.Host.Get<MomentService>();

        if (!service.CanAdd(habit.Id, ViewModel.Today) && !await PaywallDialog.ShowAsync(XamlRoot, PaywallSource.Menu))
        {
            return;
        }

        if (await source() is not { } file)
        {
            return;
        }

        var bytes = await MomentCapture.NormalizeAsync(file);
        var (result, _) = service.Add(habit, ViewModel.Today, bytes, ".jpg");

        if (result != MomentAddResult.Added)
        {
            await new ContentDialog { XamlRoot = XamlRoot, Content = Ui.T("moments.failed." + result), CloseButtonText = Ui.T("celebrate.close") }.ShowAsync();
        }

        BuildMoments();
    }
}
