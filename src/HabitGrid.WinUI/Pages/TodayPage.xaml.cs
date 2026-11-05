using System.ComponentModel;
using HabitGrid.Presentation.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace HabitGrid.WinUI.Pages;

public sealed partial class TodayPage : Page
{
    public TodayPage()
    {
        ViewModel = App.Host.Get<TodayViewModel>();
        InitializeComponent();
        ViewModel.PropertyChanged += OnViewModelChanged;
        ViewModel.Groups.CollectionChanged += (_, _) => UpdateEmpty();
    }

    public TodayViewModel ViewModel { get; }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        try
        {
            ApplyTexts();
            await ViewModel.LoadAsync();
            ApplyTexts();
        }
        catch (Exception ex)
        {
            App.Host.Get<Presentation.Services.IAppLogger>().Error("Mở trang Hôm nay lỗi", ex);
        }
    }

    private void ApplyTexts()
    {
        GreetingText.Text = Ui.Greeting(DateTime.Now);
        DateText.Text = Ui.LongDate(ViewModel.Today == default ? DateOnly.FromDateTime(DateTime.Today) : ViewModel.Today);
        AddButtonText.Text = Ui.T("habits.add");
        CompletedHeader.Text = Ui.T("today.completed");
        NotTodayHeader.Text = Ui.T("today.notToday");
        EmptyTitle.Text = Ui.T("today.empty.title");
        EmptyBody.Text = Ui.T("today.empty.body");
        EmptyAddButton.Content = Ui.T("habits.add");
        UndoButton.Content = Ui.T("today.undo");
        UpdateProgress();
        UpdateEmpty();
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(TodayViewModel.DoneCount):
            case nameof(TodayViewModel.TotalCount):
            case nameof(TodayViewModel.ProgressFraction):
                UpdateProgress();
                break;
            case nameof(TodayViewModel.CanUndo):
            case nameof(TodayViewModel.LastAction):
                UpdateUndo();
                break;
            case nameof(TodayViewModel.ErrorMessage):
                ErrorBar.Message = ViewModel.ErrorMessage ?? string.Empty;
                ErrorBar.Title = Ui.T("error.generic");
                ErrorBar.IsOpen = !string.IsNullOrEmpty(ViewModel.ErrorMessage);
                break;
            case nameof(TodayViewModel.IsEmpty):
                UpdateEmpty();
                break;
        }
    }

    private void UpdateProgress()
    {
        Ring.Value = ViewModel.ProgressFraction;
        ProgressText.Text = Ui.Progress(ViewModel.DoneCount, ViewModel.TotalCount);
        AllDoneText.Text = Ui.T("today.allDone");
        AllDoneText.Visibility = ViewModel.AllDone ? Visibility.Visible : Visibility.Collapsed;
        ProgressText.Visibility = ViewModel.TotalCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        Ring.Visibility = ViewModel.TotalCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        CompletedPanel.Visibility = ViewModel.Completed.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        NotTodayPanel.Visibility = ViewModel.NotToday.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateEmpty()
    {
        EmptyPanel.Visibility = ViewModel.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
        AddButton.Visibility = ViewModel.IsEmpty ? Visibility.Collapsed : Visibility.Visible;
        UpdateProgress();
    }

    private void UpdateUndo()
    {
        if (ViewModel.LastAction is { } action)
        {
            UndoBar.Message = Ui.Loc.Format("today.checked", action.HabitName);
            UndoBar.IsOpen = true;
        }
        else
        {
            UndoBar.IsOpen = false;
        }
    }

    private async void OnCheckClick(object? sender, EventArgs e)
    {
        try
        {
            if (sender is FrameworkElement { Tag: HabitTodayItem item })
            {
                await ViewModel.CheckInAsync(item);
            }
        }
        catch (Exception ex)
        {
            App.Host.Get<Presentation.Services.IAppLogger>().Error("Check-in lỗi", ex);
        }
    }

    private async void OnUndoClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await ViewModel.UndoAsync();
        }
        catch (Exception ex)
        {
            App.Host.Get<Presentation.Services.IAppLogger>().Error("Hoàn tác lỗi", ex);
        }
    }

    private async void OnAddClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (await AddHabitDialog.ShowAsync(XamlRoot))
            {
                await ViewModel.LoadAsync();
                ApplyTexts();
            }
        }
        catch (Exception ex)
        {
            App.Host.Get<Presentation.Services.IAppLogger>().Error("Thêm thói quen lỗi", ex);
        }
    }
}
