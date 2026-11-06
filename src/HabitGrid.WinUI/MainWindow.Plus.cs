using HabitGrid.Core.Domain;
using HabitGrid.Core.Domain.Monetization;
using HabitGrid.Presentation.Services;
using HabitGrid.Presentation.Theming;
using HabitGrid.Presentation.ViewModels;
using HabitGrid.WinUI.Plus;

namespace HabitGrid.WinUI;

/// <summary>Vòng đời HabitGrid Plus trong cửa sổ chính: xác minh quyền, theme bị khóa khi hết Plus, hạ cấp có chọn thói quen.</summary>
public sealed partial class MainWindow
{
    private bool _askedToChoose;

    /// <summary>Tự sao lưu mỗi ngày một lần (chạy nền, lỗi chỉ ghi log).</summary>
    private static async Task RunDailyBackupAsync()
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5));
            await App.Host.Get<HabitGrid.Data.V3.BackupManager>().RunIfDueAsync();
        }
        catch (Exception ex)
        {
            App.Host.Get<IAppLogger>().Error("Tự sao lưu lỗi", ex);
        }
    }

    /// <summary>Gọi một lần khi mở app, trước khi vào trang đầu tiên.</summary>
    private async Task InitializePlusAsync()
    {
        var plus = App.Host.Get<PlusService>();

        try
        {
            await plus.InitializeAsync();
        }
        catch (Exception ex)
        {
            App.Host.Get<IAppLogger>().Error("Khởi tạo Plus lỗi", ex);
        }

        await ApplyThemeAccessAsync();

        plus.Changed += (_, _) => DispatcherQueue.TryEnqueue(async () =>
        {
            await ApplyThemeAccessAsync();
            await App.Host.Get<TodayViewModel>().LoadAsync();
        });

        // Thói quen thêm/lưu trữ/xóa thì tính lại danh sách chỉ đọc (gói Free tối đa 5 đang chạy).
        App.Host.Get<IDataChangeBus>().Changed += (_, change) =>
        {
            if (change.Entity is DataEntity.Habit or DataEntity.Everything)
            {
                DispatcherQueue.TryEnqueue(async () =>
                {
                    try
                    {
                        await plus.UpdateAllowanceAsync();
                    }
                    catch (Exception ex)
                    {
                        App.Host.Get<IAppLogger>().Error("Tính lại giới hạn thói quen lỗi", ex);
                    }
                });
            }
        };
    }

    /// <summary>Theme đang chọn không còn quyền (hết Plus, chưa đủ level) thì hiện Candy Bento, giữ nguyên lựa chọn đã lưu.</summary>
    private async Task ApplyThemeAccessAsync()
    {
        var plus = App.Host.Get<PlusService>();
        var level = (await App.Host.Get<IProfileRepository>().GetAsync()).Level;
        var unlocked = ThemeAccess.IsUnlocked(_theme.Theme.Id, plus.Status.IsPlus, level);
        var desired = unlocked ? null : Themes.CandyBento;

        var accentChanged = _theme.AccentAllowed != plus.Status.IsPlus;
        _theme.AccentAllowed = plus.Status.IsPlus;

        if (_theme.Override != desired || accentChanged)
        {
            _theme.Override = desired;
            _theme.Apply(Root);
        }
    }

    /// <summary>Sau khi hạ cấp mà người dùng chưa chọn thói quen đang chạy: hỏi một lần mỗi phiên (không chặn thao tác khác).</summary>
    private async Task AskToChooseRunningIfNeededAsync()
    {
        if (_askedToChoose || IsOnboarding || !GamificationCoordinator.DialogsEnabled)
        {
            return;
        }

        var allowance = await App.Host.Get<PlusService>().GetAllowanceAsync();

        if (allowance.NeedsChoice && allowance.ReadOnly.Count > 0)
        {
            _askedToChoose = true;
            await ChooseRunningDialog.ShowAsync(Root.XamlRoot);
        }
    }
}
