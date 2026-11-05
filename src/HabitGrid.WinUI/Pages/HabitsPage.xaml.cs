using System.ComponentModel;
using HabitGrid.Presentation.Services;
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
        if (e.PropertyName == nameof(HabitsViewModel.IsEmpty))
        {
            UpdateEmpty();
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

    private async void OnEditClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is FrameworkElement { Tag: HabitListItem item } &&
                await HabitEditorDialog.ShowAsync(XamlRoot, item.Habit) is not null)
            {
                await ViewModel.LoadAsync();
            }
        }
        catch (Exception ex)
        {
            Log(ex, "Sửa thói quen lỗi");
        }
    }

    private async void OnArchiveClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is FrameworkElement { Tag: HabitListItem item })
            {
                await ViewModel.ArchiveAsync(item);
            }
        }
        catch (Exception ex)
        {
            Log(ex, "Lưu trữ lỗi");
        }
    }

    private async void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is FrameworkElement { Tag: HabitListItem item })
            {
                await ViewModel.DeleteAsync(item);
            }
        }
        catch (Exception ex)
        {
            Log(ex, "Xóa lỗi");
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
