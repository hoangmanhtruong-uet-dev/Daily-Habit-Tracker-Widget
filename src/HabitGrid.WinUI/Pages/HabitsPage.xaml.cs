using System.ComponentModel;
using HabitGrid.Presentation.Services;
using HabitGrid.WinUI.Controls;
using HabitGrid.Presentation.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace HabitGrid.WinUI.Pages;

public sealed partial class HabitsPage : Page
{
    public HabitsPage()
    {
        ViewModel = App.Host.Get<HabitsViewModel>();
        InitializeComponent();
        ViewModel.PropertyChanged += OnViewModelChanged;
        // Không dùng hiệu ứng vào/ra mặc định của GridView: nó làm thẻ chớp mờ khi tải lại danh sách.
        List.ItemContainerTransitions = new Microsoft.UI.Xaml.Media.Animation.TransitionCollection();
    }

    public HabitsViewModel ViewModel { get; }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        try
        {
            TitleText.Text = Ui.T("habits.title");
            AddButtonText.Text = Ui.T("habits.add");
            EmptyText.Text = Ui.T("habits.empty");
            UndoButton.Content = Ui.T("today.undo");
            UpdateChips();
            await ViewModel.LoadAsync();
            UpdateEmpty();
        }
        catch (Exception ex)
        {
            Log(ex, "Mở trang Thói quen lỗi");
        }
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(HabitsViewModel.IsEmpty) or nameof(HabitsViewModel.ShowArchived))
        {
            UpdateChips();
            UpdateEmpty();
        }
        else if (e.PropertyName == nameof(HabitsViewModel.ArchivedCount))
        {
            UpdateChips();
        }
        else if (e.PropertyName == nameof(HabitsViewModel.LastDeletedId))
        {
            UndoBar.Message = Ui.T("habits.delete");
            UndoBar.IsOpen = ViewModel.LastDeletedId is not null;
        }
    }

    private void UpdateEmpty()
    {
        EmptyPanel.Visibility = ViewModel.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
        List.Visibility = ViewModel.IsEmpty ? Visibility.Collapsed : Visibility.Visible;
    }

    private async void OnAddClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (await HabitEditorDialog.ShowAsync(XamlRoot) is not null)
            {
                await ViewModel.LoadAsync();
            }
        }
        catch (Exception ex)
        {
            Log(ex, "Thêm thói quen lỗi");
        }
    }

    private void UpdateChips()
    {
        ActiveChip.Content = Ui.T("habits.filter.active");
        ArchivedChip.Content = ViewModel.ArchivedCount > 0
            ? $"{Ui.T("habits.filter.archived")} · {ViewModel.ArchivedCount}"
            : Ui.T("habits.filter.archived");
        ActiveChip.IsChecked = !ViewModel.ShowArchived;
        ArchivedChip.IsChecked = ViewModel.ShowArchived;
        EmptyText.Text = Ui.T(ViewModel.ShowArchived ? "habits.empty.archived" : "habits.empty");
        List.CanReorderItems = !ViewModel.ShowArchived;
        AddButton.Visibility = ViewModel.ShowArchived ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnActiveChipClick(object sender, RoutedEventArgs e)
    {
        ViewModel.ShowArchived = false;
        UpdateChips();
    }

    private void OnArchivedChipClick(object sender, RoutedEventArgs e)
    {
        ViewModel.ShowArchived = true;
        UpdateChips();
    }

    private async void OnCardAction(object? sender, CardActionEventArgs e)
    {
        try
        {
            switch (e.Action)
            {
                case CardAction.Open:
                    Frame.Navigate(typeof(HabitDetailPage), e.Item.Id);
                    break;
                case CardAction.Edit:
                    if (await HabitEditorDialog.ShowAsync(XamlRoot, e.Item.Habit) is not null)
                    {
                        await ViewModel.LoadAsync();
                    }

                    break;
                case CardAction.MoveUp:
                    await ViewModel.MoveByAsync(e.Item, -1);
                    break;
                case CardAction.MoveDown:
                    await ViewModel.MoveByAsync(e.Item, 1);
                    break;
                case CardAction.Archive:
                    await ViewModel.ArchiveAsync(e.Item);
                    break;
                case CardAction.Unarchive:
                    await ViewModel.UnarchiveAsync(e.Item);
                    break;
                case CardAction.Delete:
                    await ViewModel.DeleteAsync(e.Item);
                    break;
            }
        }
        catch (Exception ex)
        {
            Log(ex, "Thao tác thẻ thói quen lỗi");
        }
    }

    private async void OnUndoDeleteClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await ViewModel.UndoDeleteAsync();
            UndoBar.IsOpen = false;
        }
        catch (Exception ex)
        {
            Log(ex, "Hoàn tác xóa lỗi");
        }
    }

    /// <summary>Kéo-thả xong: danh sách đã được ListView đổi chỗ; ghi vị trí mới (một dòng nhờ fractional index).</summary>
    private async void OnDragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
    {
        try
        {
            if (args.Items.FirstOrDefault() is HabitListItem item)
            {
                var index = ViewModel.Items.IndexOf(item);
                if (index >= 0)
                {
                    await ViewModel.MoveAsync(item, index);
                }
            }
        }
        catch (Exception ex)
        {
            Log(ex, "Đổi vị trí lỗi");
        }
    }

    private static void Log(Exception ex, string message) =>
        App.Host.Get<IAppLogger>().Error(message, ex);
}
