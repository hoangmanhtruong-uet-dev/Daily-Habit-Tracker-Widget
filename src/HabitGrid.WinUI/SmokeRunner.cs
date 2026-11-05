using HabitGrid.Core.Domain;
using HabitGrid.Presentation.Services;
using HabitGrid.Presentation.Templates;
using HabitGrid.Presentation.Theming;
using HabitGrid.Presentation.ViewModels;
using HabitGrid.WinUI.Controls;
using HabitGrid.WinUI.Pages;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace HabitGrid.WinUI;

/// <summary>
/// Chế độ <c>--smoke</c>: chạy toàn bộ hành trình người dùng mới trên giao diện thật rồi xuất ảnh chụp từng bước:
/// onboarding → Hôm nay → check-in/hoàn tác → tạo thói quen → sửa thói quen → mọi trang.
/// Đây là bài kiểm tra "dựng XAML thật" mà bản v1 đã thiếu (SPEC-V2-11). Thoát 0 nếu đạt, khác 0 nếu có lỗi.
/// Từ chối chạy khi không đặt <c>HABITGRID_DATA_DIR</c> vì smoke test ghi dữ liệu.
/// </summary>
public static class SmokeRunner
{
    private static int _shotNo;

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

            await OnboardingJourneyAsync(window, logger);
            await CheckInRoundTripAsync(window, logger);
            await EditorJourneyAsync(window, logger);

            foreach (var key in new[] { PageKeys.Habits, PageKeys.Insights, PageKeys.Buddy, PageKeys.Settings, PageKeys.Today })
            {
                await GoToAsync(window, key);
                await SnapAsync(window, $"page-{key}");
                logger.Info($"SMOKE page {key} OK");
            }

            // Mọi theme × sáng/tối phải dựng được (ảnh để duyệt giao diện).
            var variants = new (ThemeDefinition Theme, ThemeMode Mode, string Name)[]
            {
                (Themes.CandyBento, ThemeMode.Dark, "candy-dark"),
                (Themes.CandyBento, ThemeMode.Light, "candy-light"),
                (Themes.NeonNight, ThemeMode.Dark, "neon"),
                (Themes.SoftPastel, ThemeMode.Light, "pastel")
            };

            foreach (var (theme, mode, name) in variants)
            {
                await window.SetThemeAsync(theme, mode);
                await GoToAsync(window, PageKeys.Today);
                await Task.Delay(500);
                await SnapAsync(window, $"theme-{name}");
            }

            await window.SetThemeAsync(Themes.CandyBento, ThemeMode.System);
            logger.Info("SMOKE themes OK");

            logger.Info("SMOKE OK");
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            logger.Error("SMOKE FAILED", ex);
            Environment.Exit(2);
        }
    }

    // ------------------------------------------------------------------ onboarding

    private static async Task OnboardingJourneyAsync(MainWindow window, IAppLogger logger)
    {
        Require(window.IsOnboarding, "Dữ liệu trống nhưng không thấy onboarding.");
        var page = window.CurrentOnboardingPage ?? throw new InvalidOperationException("Không có trang onboarding.");
        var vm = page.ViewModel;
        await Task.Delay(500);
        await SnapAsync(window, "onboarding-1-welcome");

        await page.GoNextAsync();
        Require(vm.Step == OnboardingStep.Goals && page.IsPanelVisible(OnboardingStep.Goals), "Không sang được bước chủ đề.");
        Require(!vm.CanGoNext, "Chưa chọn chủ đề mà vẫn đi tiếp được.");
        vm.ToggleTopic(HabitTopic.Fitness);
        vm.ToggleTopic(HabitTopic.Mind);
        page.RefreshForSmoke();
        await Task.Delay(400);
        await SnapAsync(window, "onboarding-2-goals");

        await page.GoNextAsync();
        Require(vm.Step == OnboardingStep.Habits && page.IsPanelVisible(OnboardingStep.Habits), "Không sang được bước thói quen.");
        Require(vm.Suggestions.Count >= 16, "Thiếu gợi ý thói quen.");
        vm.TogglePick(HabitTemplates.Find("fit.gym")!);
        vm.TogglePick(HabitTemplates.Find("mind.meditate")!);
        page.RefreshForSmoke();
        await Task.Delay(700);
        await SnapAsync(window, "onboarding-3-habits");

        await page.GoNextAsync();
        Require(vm.Step == OnboardingStep.Buddy && page.IsPanelVisible(OnboardingStep.Buddy), "Không sang được bước Buddy.");
        vm.BuddyName = "Mochi";
        page.RefreshForSmoke();
        await Task.Delay(400);
        await SnapAsync(window, "onboarding-4-buddy");

        await page.GoNextAsync();
        Require(vm.Step == OnboardingStep.Reminder && page.IsPanelVisible(OnboardingStep.Reminder), "Không sang được bước nhắc nhở.");
        await Task.Delay(400);
        await SnapAsync(window, "onboarding-5-reminder");

        await page.FinishAsync();
        await Task.Delay(1200);

        Require(!window.IsOnboarding, "Onboarding xong nhưng vẫn ở chế độ onboarding.");
        var habits = await App.Host.Get<IHabitRepository>().GetAllActiveAsync();
        Require(habits.Count == 2, $"Kỳ vọng 2 thói quen sau onboarding, thực tế {habits.Count}.");
        Require((await App.Host.Get<IProfileRepository>().GetAsync()).BuddyName == "Mochi", "Tên Buddy không được lưu.");
        Require(await App.Host.Get<ISettingsStore>().GetAsync(SettingKeys.OnboardingDone) == "1", "Chưa đánh dấu onboarding xong.");
        Require(window.CurrentPageKey == PageKeys.Today, "Sau onboarding không vào trang Hôm nay.");
        await SnapAsync(window, "today-after-onboarding");
        logger.Info("SMOKE onboarding OK");
    }

    // ------------------------------------------------------------------ check-in

    private static async Task CheckInRoundTripAsync(MainWindow window, IAppLogger logger)
    {
        var logs = App.Host.Get<IDayLogRepository>();
        var today = App.Host.Get<TodayViewModel>();

        await today.LoadAsync();
        await Task.Delay(500);

        var pending = today.Groups.SelectMany(g => g.Items).FirstOrDefault(i => i.Kind == HabitKind.Boolean)
                      ?? throw new InvalidOperationException("Không có thói quen kiểu có/không để check-in.");

        var doneBefore = today.DoneCount;
        var button = FindCards(window.Content).FirstOrDefault(c => c.Item?.Id == pending.Id)
                     ?? throw new InvalidOperationException("Không tìm thấy nút check trên giao diện.");

        await button.PerformClickAsync();
        await Task.Delay(900);

        var saved = await logs.GetAsync(pending.Id, today.Today)
                    ?? throw new InvalidOperationException("Bấm nút nhưng DB không có ghi nhận.");

        Require(saved.Status == DayStatus.Done && today.DoneCount == doneBefore + 1,
            $"Sau check-in kỳ vọng Done và DoneCount {doneBefore + 1}, thực tế {saved.Status} / {today.DoneCount}.");
        await SnapAsync(window, "today-checked");
        logger.Info("SMOKE check-in OK");

        await today.UndoAsync();
        await Task.Delay(500);
        Require(await logs.GetAsync(pending.Id, today.Today) is null && today.DoneCount == doneBefore, "Hoàn tác không đưa dữ liệu về trạng thái trước.");
        logger.Info("SMOKE undo OK");

        // Trạng thái dở dang của thẻ số (thanh tiến độ), rồi hoàn thành cả ngày (hero ăn mừng), rồi xem ngày cũ.
        var numeric = today.Groups.SelectMany(g => g.Items).FirstOrDefault(i => i.Kind is HabitKind.Duration or HabitKind.Count);
        if (numeric is not null)
        {
            await today.SetValueAsync(numeric, Math.Max(1, numeric.Target / 3));
            await Task.Delay(700);
            AssertCardsMatchViewModel(window, today);
        await SnapAsync(window, "today-partial");
            Require(numeric.Status != DayStatus.Done && numeric.Progress > 0, "Thẻ số không hiện tiến độ dở dang.");
        }

        foreach (var item in today.Groups.SelectMany(g => g.Items).Where(i => !i.IsDone && i.Kind != HabitKind.Quit).ToList())
        {
            var card = FindCards(window.Content).First(c => c.Item?.Id == item.Id);
            await card.PerformClickAsync();
            await Task.Delay(500);
        }

        await Task.Delay(700);
        Require(today.AllDone, "Check hết mà chưa AllDone.");
        Require(today.BuddyMood == BuddyMood.Ecstatic, "Buddy không ăn mừng khi xong hết.");
        AssertCardsMatchViewModel(window, today);
        await SnapAsync(window, "today-all-done");
        logger.Info("SMOKE all-done OK");

        await today.SelectDayAsync(today.Today.AddDays(-1));
        await Task.Delay(700);
        Require(today.IsViewingPast, "Không chuyển sang xem ngày cũ.");
        await SnapAsync(window, "today-past-day");
        await today.SelectDayAsync(today.Today);
        await Task.Delay(400);
        logger.Info("SMOKE past-day OK");
    }

    // ------------------------------------------------------------------ tạo / sửa thói quen

    private static async Task EditorJourneyAsync(MainWindow window, IAppLogger logger)
    {
        var habits = App.Host.Get<IHabitRepository>();
        var root = window.Content as FrameworkElement ?? throw new InvalidOperationException("Cửa sổ chưa có nội dung.");

        // --- tạo mới ---
        var createTask = HabitEditorDialog.ShowAsync(root.XamlRoot);
        await Task.Delay(700);
        var view = FindEditorView(root) ?? throw new InvalidOperationException("Hộp thoại tạo thói quen không mở được.");

        view.ViewModel.Name = "Đọc sách (smoke)";
        view.ViewModel.Kind = HabitKind.Duration;
        view.ViewModel.Target = 25;
        view.ViewModel.FrequencyType = FrequencyType.WeekDays;
        view.ViewModel.WeekDays = Core.Models.DayOfWeekMask.Monday | Core.Models.DayOfWeekMask.Wednesday | Core.Models.DayOfWeekMask.Friday;
        view.ViewModel.TimeOfDay = TimeOfDay.Evening;
        view.ViewModel.ColorHue = "#EC4899";
        view.ViewModel.AddReminder();
        await Task.Delay(600);
        await SnapElementAsync(view.FullContent, "editor-new");

        Require(await view.TrySaveAsync(), "Lưu thói quen mới thất bại.");
        CloseDialogs(root);
        Require(await createTask is not null, "Hộp thoại không trả về thói quen đã lưu.");

        var created = (await habits.GetAllActiveAsync()).FirstOrDefault(h => h.Name == "Đọc sách (smoke)")
                      ?? throw new InvalidOperationException("Thói quen mới không có trong DB.");
        Require(created.Kind == HabitKind.Duration && created.Target.Value == 25, "Loại/mục tiêu không được lưu.");
        Require(created.Frequency is HabitFrequency.WeekDays, "Tần suất không được lưu.");
        Require(created.Reminders.Count == 1, "Nhắc nhở không được lưu.");
        logger.Info("SMOKE editor create OK");

        // --- dữ liệu không hợp lệ phải được chặn ---
        var invalidTask = HabitEditorDialog.ShowAsync(root.XamlRoot);
        await Task.Delay(600);
        var invalidView = FindEditorView(root) ?? throw new InvalidOperationException("Không mở được hộp thoại lần hai.");
        Require(!await invalidView.TrySaveAsync(), "Lưu thói quen không tên lại thành công.");
        await Task.Delay(300);
        await SnapElementAsync(invalidView.FullContent, "editor-error");
        CloseDialogs(root);
        await invalidTask;
        logger.Info("SMOKE editor validation OK");

        // --- sửa ---
        var editTask = HabitEditorDialog.ShowAsync(root.XamlRoot, created);
        await Task.Delay(700);
        var editView = FindEditorView(root) ?? throw new InvalidOperationException("Hộp thoại sửa không mở được.");
        Require(editView.ViewModel.IsEditing && editView.ViewModel.Name == "Đọc sách (smoke)", "Hộp thoại sửa không nạp đúng dữ liệu.");
        editView.ViewModel.Name = "Đọc sách (đã sửa)";
        editView.ViewModel.FrequencyType = FrequencyType.TimesPerWeek;
        editView.ViewModel.TimesPerWeek = 4;
        await Task.Delay(400);
        await SnapElementAsync(editView.FullContent, "editor-edit");

        Require(await editView.TrySaveAsync(), "Lưu chỉnh sửa thất bại.");
        CloseDialogs(root);
        await editTask;

        var edited = await habits.GetByIdAsync(created.Id);
        Require(edited!.Name == "Đọc sách (đã sửa)" && edited.Frequency is HabitFrequency.TimesPerWeek { Times: 4 }, "Chỉnh sửa không được lưu.");
        Require(edited.Id == created.Id, "Sửa không được đổi Id.");
        logger.Info("SMOKE editor edit OK");
    }

    // ------------------------------------------------------------------ tiện ích

    private static async Task GoToAsync(MainWindow window, string key)
    {
        window.NavigateTo(key);
        await Task.Delay(700);
        Require(window.CurrentPageKey == key, $"Không chuyển được tới trang '{key}'.");
    }

    private static async Task SnapAsync(MainWindow window, string name)
    {
        if (window.Content is UIElement content)
        {
            await Shot.SaveAsync(content, $"{++_shotNo:00}-{name}");
        }
    }

    private static async Task SnapElementAsync(UIElement element, string name) =>
        await Shot.SaveAsync(element, $"{++_shotNo:00}-{name}");

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static HabitEditorView? FindEditorView(FrameworkElement root)
    {
        foreach (var popup in VisualTreeHelper.GetOpenPopupsForXamlRoot(root.XamlRoot))
        {
            if (popup.Child is ContentDialog { Content: HabitEditorView view })
            {
                return view;
            }
        }

        return null;
    }

    private static void CloseDialogs(FrameworkElement root)
    {
        foreach (var popup in VisualTreeHelper.GetOpenPopupsForXamlRoot(root.XamlRoot))
        {
            if (popup.Child is ContentDialog dialog)
            {
                dialog.Hide();
            }
        }
    }

    /// <summary>Mỗi thẻ trên màn hình phải khớp trạng thái ViewModel (bắt lỗi thẻ "mất kết nối" với dữ liệu).</summary>
    private static void AssertCardsMatchViewModel(MainWindow window, TodayViewModel today)
    {
        var cards = FindCards(window.Content).Where(c => c.Item is not null).ToList();
        Require(cards.Count >= today.Groups.Sum(g => g.Items.Count), "Số thẻ trên màn hình ít hơn ViewModel.");

        foreach (var card in cards)
        {
            var item = card.Item!;
            Require(
                card.IsShowingDone == item.IsDone,
                $"Thẻ '{item.Name}' hiển thị {(card.IsShowingDone ? "xong" : "chưa xong")} nhưng ViewModel là {(item.IsDone ? "xong" : "chưa xong")}.");

            if (item.Kind is HabitKind.Count or HabitKind.Duration && item.Value > 0)
            {
                Require(card.DisplayedSubText.Contains(item.Value.ToString()), $"Thẻ '{item.Name}' không hiển thị giá trị {item.Value}: '{card.DisplayedSubText}'.");
            }
        }
    }

    private static IEnumerable<HabitCard> FindCards(DependencyObject? parent)
    {
        if (parent is null)
        {
            yield break;
        }

        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);

            if (child is HabitCard card)
            {
                yield return card;
            }

            foreach (var nested in FindCards(child))
            {
                yield return nested;
            }
        }
    }
}
