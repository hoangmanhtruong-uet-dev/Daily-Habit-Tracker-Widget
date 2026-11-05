using HabitGrid.Presentation.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace HabitGrid.WinUI.Pages;

/// <summary>Trang giữ chỗ cho các mục chưa làm (Insights, Buddy, Cài đặt). Nhận khóa trang làm tham số điều hướng.</summary>
public sealed partial class PlaceholderPage : Page
{
    public PlaceholderPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        var key = e.Parameter as string ?? string.Empty;
        TitleText.Text = Ui.T($"nav.{key}") is { } title && !title.StartsWith('[') ? title : Ui.T("placeholder.soon");
        BodyText.Text = Ui.T($"placeholder.{key}");
        IconText.Text = key switch
        {
            PageKeys.Insights => "📊",
            PageKeys.Buddy => "🐣",
            PageKeys.Settings => "⚙️",
            _ => "✨"
        };
    }
}
