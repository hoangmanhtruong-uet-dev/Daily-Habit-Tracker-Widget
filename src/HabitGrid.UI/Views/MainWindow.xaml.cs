using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Interop;
using System.Windows.Media;
using HabitGrid.Platform.Desktop;
using HabitGrid.UI.Controls;
using HabitGrid.UI.Theme;
using HabitGrid.UI.ViewModels;

namespace HabitGrid.UI.Views;

/// <summary>
/// Cửa sổ quản lý chính: danh sách thói quen (kéo-thả sắp xếp) + trang chi tiết.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    private HabitItemViewModel? _draggedItem;
    private int _fromIndex = -1;
    private bool _deleteArmed;
    private DateTime _deleteArmedAt;

    public MainWindow(MainViewModel viewModel, SettingsViewModel? settingsViewModel = null)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();

        _viewModel = viewModel;
        DataContext = _viewModel;

        if (settingsViewModel is not null)
        {
            SettingsPane.DataContext = settingsViewModel;
        }

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        UpdateDetail();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.Detail) or nameof(MainViewModel.SelectedItem)
            or nameof(MainViewModel.IsEmpty) or nameof(MainViewModel.Items))
        {
            UpdateDetail();
        }
    }

    private void UpdateDetail()
    {
        var detail = _viewModel.Detail;

        if (detail is null)
        {
            DetailName.Text = "Chưa chọn thói quen";
            DetailIcon.Text = string.Empty;
            MetricStreak.Text = "—";
            MetricLongest.Text = "—";
            MetricRate.Text = "—";
            MetricTotal.Text = "—";
            DetailGrid.Model = null;
            MonthChart.Bars = Array.Empty<MonthBar>();
            EmptyStateText.Visibility = _viewModel.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
            return;
        }

        DetailName.Text = detail.Name;
        DetailIcon.Text = detail.Habit.Icon.Length > 0 ? IconCatalog.Get(detail.Habit.Icon).Glyph : string.Empty;

        MetricStreak.Text = detail.CurrentStreakText;
        MetricLongest.Text = detail.LongestStreakText;
        MetricRate.Text = detail.CompletionRateText;
        MetricTotal.Text = detail.TotalCompletedText;

        DetailGrid.Model = detail.GridModel;
        MonthChart.Bars = detail.MonthBars;

        EmptyStateText.Visibility = _viewModel.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
    }

    #region Kéo thả sắp xếp

    private void OnDragHandleMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not HabitItemViewModel item)
        {
            return;
        }

        _draggedItem = item;
        _fromIndex = _viewModel.Items.IndexOf(item);

        DragDrop.DoDragDrop(element, new DataObject(typeof(HabitItemViewModel), item), DragDropEffects.Move);
    }

    protected override void OnDragOver(DragEventArgs e)
    {
        base.OnDragOver(e);

        e.Effects = e.Data.GetDataPresent(typeof(HabitItemViewModel))
            ? DragDropEffects.Move
            : DragDropEffects.None;

        e.Handled = true;
    }

    protected override async void OnDrop(DragEventArgs e)
    {
        base.OnDrop(e);

        if (!e.Data.GetDataPresent(typeof(HabitItemViewModel)) || _draggedItem is null)
        {
            return;
        }

        var point = e.GetPosition(HabitList);
        var targetIndex = GetDropIndex(point);

        if (_fromIndex >= 0 && targetIndex != _fromIndex)
        {
            await _viewModel.MoveAsync(_fromIndex, targetIndex);
        }

        _draggedItem = null;
        _fromIndex = -1;
    }

    /// <summary>Tính chỉ số chèn dựa trên vị trí chuột trong danh sách.</summary>
    private int GetDropIndex(Point point)
    {
        var index = 0;

        foreach (var container in HabitList.ItemContainerGenerator.Items.OfType<FrameworkElement>())
        {
            if (container.ActualHeight <= 0)
            {
                continue;
            }

            var bounds = container.TranslatePoint(new Point(0, 0), HabitList);
            var top = bounds.Y;
            var height = container.ActualHeight;

            if (point.Y < top + (height / 2))
            {
                return index;
            }

            index++;
        }

        return index;
    }

    #endregion

    #region Thao tác

    private async void OnAddHabitClick(object sender, RoutedEventArgs e) => await _viewModel.AddHabitCommand.ExecuteAsync(null);

    private async void OnEditHabitClick(object sender, RoutedEventArgs e) =>
        await _viewModel.EditHabitCommand.ExecuteAsync(_viewModel.SelectedItem);

    private async void OnArchiveClick(object sender, RoutedEventArgs e) =>
        await _viewModel.ArchiveHabitCommand.ExecuteAsync(_viewModel.SelectedItem);

    private async void OnToggleWidgetClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: Guid habitId })
        {
            var item = _viewModel.Items.FirstOrDefault(i => i.Id == habitId);
            await _viewModel.ToggleWidgetCommand.ExecuteAsync(item);
        }
    }

    /// <summary>Xóa an toàn 2 bước: bấm lần 1 để cảnh báo, bấm lần 2 trong 5 giây để xác nhận.</summary>
    private async void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        var item = _viewModel.SelectedItem;

        if (item is null)
        {
            return;
        }

        if (sender is Button button)
        {
            if (!_deleteArmed || (DateTime.UtcNow - _deleteArmedAt).TotalSeconds > 5)
            {
                _deleteArmed = true;
                _deleteArmedAt = DateTime.UtcNow;
                button.Content = "Nhấn lại để xác nhận";
                return;
            }

            _deleteArmed = false;
            button.Content = "Xóa";
        }

        await _viewModel.DeleteHabitCommand.ExecuteAsync(item);
    }

    private async void OnDetailCellClicked(object? sender, HeatmapCellEventArgs e)
    {
        if (_viewModel.Detail is null)
        {
            return;
        }

        await _viewModel.ToggleEntryAsync(_viewModel.Detail.Id, e.Cell.Date);
    }

    private void OnHabitCardClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: HabitItemViewModel item })
        {
            _viewModel.SelectedItem = item;
            ShowSettings(false);
        }
    }

    /// <summary>Mở trang cài đặt (dùng khi khởi động bằng tham số --settings).</summary>
    public void ShowSettingsPage() => ShowSettings(true);

    private void OnSettingsClick(object sender, RoutedEventArgs e) => ShowSettings(true);

    private void ShowSettings(bool show)
    {
        SettingsPane.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        DetailScroller.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnUpgradeClick(object sender, RoutedEventArgs e) =>
        MessageBox.Show(
            "Gói Pro: không giới hạn thói quen, bảng màu heatmap tùy chỉnh, xuất/nhập sao lưu.\n\n" +
            "Màn hình nâng cấp sẽ được hoàn thiện ở SPEC-07.",
            "Nâng cấp Pro",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

    private void OnRestoreClick(object sender, RoutedEventArgs e) =>
        MessageBox.Show("Khôi phục thói quen đã lưu trữ sẽ được bổ sung ở gói sau.", "HabitGrid", MessageBoxButton.OK, MessageBoxImage.Information);

    #endregion
}
