using HabitGrid.Core.Domain;
using HabitGrid.Presentation.Services;
using HabitGrid.Presentation.ViewModels;
using HabitGrid.WinUI.Controls;
using HabitGrid.WinUI.Pages;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace HabitGrid.WinUI;

/// <summary>
/// Chế độ <c>--smoke</c>: dựng thật mọi trang, mở hộp thoại, rồi check-in và hoàn tác qua chính nút trên giao diện.
/// Đây là bài kiểm tra "dựng XAML thật" mà bản v1 đã thiếu (SPEC-V2-11). Thoát 0 nếu mọi thứ đạt, khác 0 nếu có lỗi.
/// Từ chối chạy khi không đặt <c>HABITGRID_DATA_DIR</c> vì smoke test ghi dữ liệu.
/// </summary>
public static class SmokeRunner
{
    public static async Task RunAsync(MainWindow window)
    {
        var logger = App.Host.Get<IAppLogger>();

        try
        {
            if (!AppPaths.IsCustomDataDir)
            {
                throw new InvalidOperationException(
                    $"Smoke test ghi dữ liệu nên bắt buộc đặt {AppPaths.DataDirVariable}; từ chối chạy trên dữ liệu thật.");
            }

            await Task.Delay(800);

            foreach (var key in new[] { PageKeys.Today, PageKeys.Habits, PageKeys.Insights, PageKeys.Buddy, PageKeys.Settings })
            {
                await GoToAsync(window, key);
                logger.Info($"SMOKE page {key} OK");
            }

            await CheckInRoundTripAsync(window, logger);
            await DialogAsync(window, logger);

            logger.Info("SMOKE OK");
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            logger.Error("SMOKE FAILED", ex);
            Environment.Exit(2);
        }
    }

    private static async Task GoToAsync(MainWindow window, string key)
    {
        window.NavigateTo(key);
        await Task.Delay(600);

        if (window.CurrentPageKey != key)
        {
            throw new InvalidOperationException($"Không chuyển được tới trang '{key}'.");
        }
    }

    /// <summary>Tạo thói quen, bấm nút check trên giao diện, kiểm tra DB và danh sách; rồi hoàn tác.</summary>
    private static async Task CheckInRoundTripAsync(MainWindow window, IAppLogger logger)
    {
        var habits = App.Host.Get<IHabitRepository>();
        var logs = App.Host.Get<IDayLogRepository>();
        var today = App.Host.Get<TodayViewModel>();

        if ((await habits.GetAllActiveAsync()).Count == 0)
        {
            var created = await App.Host.Get<HabitCreationService>()
                .CreateAsync(new HabitDraft("Smoke test", HabitIcon.Emoji("🧪")));

            if (!created.Succeeded)
            {
                throw new InvalidOperationException("Không tạo được thói quen mẫu cho smoke test.");
            }
        }

        await GoToAsync(window, PageKeys.Habits);
        await GoToAsync(window, PageKeys.Today);
        await today.LoadAsync();
        await Task.Delay(500);

        var pending = today.Groups.SelectMany(g => g.Items).FirstOrDefault(i => i.Kind == HabitKind.Boolean)
                      ?? throw new InvalidOperationException("Không có thói quen kiểu có/không để check-in.");

        var doneBefore = today.DoneCount;

        var button = FindButtons(window.Content).FirstOrDefault(b => b.Tag is HabitTodayItem item && item.Id == pending.Id)
                     ?? throw new InvalidOperationException("Không tìm thấy nút check trên giao diện.");

        button.PerformClick();
        await Task.Delay(800);

        var saved = await logs.GetAsync(pending.Id, today.Today)
                    ?? throw new InvalidOperationException("Bấm nút nhưng DB không có ghi nhận.");

        if (saved.Status != DayStatus.Done || today.DoneCount != doneBefore + 1)
        {
            throw new InvalidOperationException(
                $"Sau check-in kỳ vọng Done và DoneCount {doneBefore + 1}, thực tế {saved.Status} / {today.DoneCount}.");
        }

        logger.Info("SMOKE check-in OK");

        await today.UndoAsync();
        await Task.Delay(500);

        if (await logs.GetAsync(pending.Id, today.Today) is not null || today.DoneCount != doneBefore)
        {
            throw new InvalidOperationException("Hoàn tác không đưa dữ liệu về trạng thái trước.");
        }

        logger.Info("SMOKE undo OK");
    }

    private static async Task DialogAsync(MainWindow window, IAppLogger logger)
    {
        if (window.Content is not FrameworkElement root)
        {
            throw new InvalidOperationException("Cửa sổ chưa có nội dung.");
        }

        var dialogTask = AddHabitDialog.ShowAsync(root.XamlRoot);
        await Task.Delay(600);

        var closed = 0;
        foreach (var popup in VisualTreeHelper.GetOpenPopupsForXamlRoot(root.XamlRoot))
        {
            if (popup.Child is ContentDialog dialog)
            {
                dialog.Hide();
                closed++;
            }
        }

        await dialogTask;

        if (closed == 0)
        {
            throw new InvalidOperationException("Hộp thoại thêm thói quen không mở được.");
        }

        logger.Info("SMOKE dialog OK");
    }

    private static IEnumerable<HabitCheckButton> FindButtons(DependencyObject? parent)
    {
        if (parent is null)
        {
            yield break;
        }

        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);

            if (child is HabitCheckButton button)
            {
                yield return button;
            }

            foreach (var nested in FindButtons(child))
            {
                yield return nested;
            }
        }
    }
}
