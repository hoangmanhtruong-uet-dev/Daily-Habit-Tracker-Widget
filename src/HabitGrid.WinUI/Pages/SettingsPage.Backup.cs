using HabitGrid.Data.V3;
using HabitGrid.Presentation.Services;
using HabitGrid.Presentation.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;

namespace HabitGrid.WinUI.Pages;

/// <summary>Cài đặt → Sao lưu (F5): sao lưu ngay, khôi phục từ file hoặc từ danh sách bản tự động, mở thư mục.</summary>
public sealed partial class SettingsPage
{
    private void BuildBackupCard()
    {
        var manager = App.Host.Get<BackupManager>();
        var card = Card(Ui.T("backup.title"));
        card.Children.Add(new TextBlock { Text = Ui.T("backup.hint"), Style = Res("BodySecondaryTextStyle"), TextWrapping = TextWrapping.Wrap });

        var status = new TextBlock { TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
        var list = new StackPanel { Spacing = 6 };

        var now = new Button { Content = Ui.T("backup.now"), Style = Res("GradientButtonStyle") };
        var pick = new Button { Content = Ui.T("backup.restore.file"), Style = Res("GhostButtonStyle") };
        var open = new Button { Content = Ui.T("backup.open"), Style = Res("GhostButtonStyle") };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        buttons.Children.Add(now);
        buttons.Children.Add(pick);
        buttons.Children.Add(open);

        void Show(string text, bool error = false)
        {
            status.Text = text;
            status.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[error ? "WarningBrush" : "SuccessBrush"];
            status.Visibility = Visibility.Visible;
        }

        void Render()
        {
            list.Children.Clear();

            foreach (var file in manager.List().Take(5))
            {
                var row = new Grid { ColumnSpacing = 10 };
                row.ColumnDefinitions.Add(new ColumnDefinition());
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var label = $"{file.CreatedAtUtc.ToLocalTime():dd/MM/yyyy HH:mm} · {(file.IsAutomatic ? Ui.T("backup.auto") : Ui.T("backup.manual"))} · {file.SizeBytes / 1024} KB";
                row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Style = Res("BodySecondaryTextStyle") });
                var restore = new Button { Content = Ui.T("backup.restore"), Style = Res("GhostButtonStyle") };
                AutomationProperties.SetName(restore, $"{Ui.T("backup.restore")} {label}");
                restore.Click += async (_, _) => await RestoreAsync(file.Path);
                Grid.SetColumn(restore, 1);
                row.Children.Add(restore);
                list.Children.Add(row);
            }

            if (list.Children.Count == 0)
            {
                list.Children.Add(new TextBlock { Text = Ui.T("backup.none"), Style = Res("BodySecondaryTextStyle") });
            }
        }

        async Task RestoreAsync(string path)
        {
            try
            {
                var confirm = await new ContentDialog
                {
                    XamlRoot = XamlRoot,
                    Title = Ui.T("backup.confirm.title"),
                    Content = new TextBlock { Text = Ui.T("backup.confirm.body"), TextWrapping = TextWrapping.Wrap },
                    PrimaryButtonText = Ui.T("backup.restore"),
                    CloseButtonText = Ui.T("plus.close"),
                    DefaultButton = ContentDialogButton.Close
                }.ShowAsync();

                if (confirm != ContentDialogResult.Primary)
                {
                    return;
                }

                var manifest = await manager.RestoreAsync(path);

                // Dữ liệu vừa được thay thế: nạp lại các trang đang giữ trạng thái.
                await App.Host.Get<TodayViewModel>().LoadAsync();
                await App.Host.Get<HabitsViewModel>().LoadAsync();
                Show(Ui.Loc.Format("backup.restored", manifest.Habits, manifest.DayLogs));
                Render();
            }
            catch (UnsupportedBackupException ex)
            {
                Show(Ui.Loc.Format("backup.invalid", ex.Message), error: true);
            }
            catch (Exception ex)
            {
                App.Host.Get<IAppLogger>().Error("Khôi phục sao lưu lỗi", ex);
                Show(Ui.T("backup.failed"), error: true);
            }
        }

        now.Click += async (_, _) =>
        {
            try
            {
                var file = await manager.BackupNowAsync();
                Show(Ui.Loc.Format("backup.done", file.Path));
                Render();
            }
            catch (Exception ex)
            {
                App.Host.Get<IAppLogger>().Error("Sao lưu lỗi", ex);
                Show(Ui.T("backup.failed"), error: true);
            }
        };

        pick.Click += async (_, _) =>
        {
            var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
            picker.FileTypeFilter.Add(SnapshotBackupService.Extension);

            if (App.MainWindowInstance is { } window)
            {
                WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(window));
            }

            if (await picker.PickSingleFileAsync() is { } file)
            {
                await RestoreAsync(file.Path);
            }
        };

        open.Click += (_, _) =>
        {
            System.IO.Directory.CreateDirectory(manager.Directory);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{manager.Directory}\"") { UseShellExecute = true });
        };

        card.Children.Add(buttons);
        card.Children.Add(status);
        card.Children.Add(list);
        Render();
        Wrap(card);
    }
}
