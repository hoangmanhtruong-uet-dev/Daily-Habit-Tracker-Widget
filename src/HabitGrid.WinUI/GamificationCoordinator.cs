using HabitGrid.Core.Domain;
using HabitGrid.Core.Domain.Gamification;
using HabitGrid.Presentation.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HabitGrid.WinUI;

/// <summary>
/// Nối game hóa vào vòng đời app: tính lại (có debounce) khi nhật ký/thói quen đổi, hỏi dùng freeze khi lỡ ngày,
/// và ăn mừng lên cấp / huy hiệu mới bằng hộp thoại nhỏ. Mỗi lúc chỉ mở một hộp thoại.
/// </summary>
public sealed class GamificationCoordinator : IDisposable
{
    private readonly GamificationService _service;
    private readonly IDataChangeBus _bus;
    private readonly DispatcherQueueTimer _debounce;
    private readonly Func<XamlRoot?> _root;
    private bool _dialogOpen;

    /// <summary>Tắt hộp thoại ăn mừng/hỏi freeze (smoke test không được bị chặn bởi hộp thoại).</summary>
    public static bool DialogsEnabled { get; set; } = true;
    private bool _disposed;

    public GamificationCoordinator(GamificationService service, IDataChangeBus bus, DispatcherQueue queue, Func<XamlRoot?> root)
    {
        _service = service;
        _bus = bus;
        _root = root;
        _debounce = queue.CreateTimer();
        _debounce.Interval = TimeSpan.FromMilliseconds(900);
        _debounce.IsRepeating = false;
        _debounce.Tick += async (_, _) => await RefreshSafelyAsync();

        _bus.Changed += OnChanged;
        _service.Celebrated += OnCelebrated;
    }

    /// <summary>Khi mở app: tính lần đầu (im lặng với dữ liệu cũ), rồi xử lý ngày lỡ.</summary>
    public async Task StartAsync()
    {
        await RefreshSafelyAsync();

        try
        {
            var plan = await _service.CheckFreezeAsync();

            if (plan.HasPlan && plan.NeedsConfirmation)
            {
                await AskFreezeAsync(plan);
            }
        }
        catch (Exception ex)
        {
            App.Host.Get<IAppLogger>().Error("Kiểm tra freeze lỗi", ex);
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _bus.Changed -= OnChanged;
        _service.Celebrated -= OnCelebrated;
        _debounce.Stop();
    }

    private void OnChanged(object? sender, DataChange change)
    {
        // Chỉ nhật ký và thói quen ảnh hưởng XP/huy hiệu; hồ sơ/huy hiệu do chính dịch vụ ghi nên bỏ qua (tránh vòng lặp).
        if (_disposed || change.Entity is not (DataEntity.DayLog or DataEntity.Habit or DataEntity.XpEvent or DataEntity.Everything))
        {
            return;
        }

        _debounce.Stop();
        _debounce.Start();
    }

    private async Task RefreshSafelyAsync()
    {
        try
        {
            await _service.RefreshAsync();
        }
        catch (Exception ex)
        {
            App.Host.Get<IAppLogger>().Error("Tính lại game hóa lỗi", ex);
        }
    }

    private async void OnCelebrated(object? sender, GamificationSnapshot snapshot)
    {
        try
        {
            if (snapshot.LeveledUpTo is { } level)
            {
                await ShowAsync(Ui.Loc.Format("celebrate.level.title", level), "🎉  " + Ui.T("celebrate.level.body"));
            }

            if (snapshot.NewlyUnlocked.Count > 0)
            {
                var lines = snapshot.NewlyUnlocked.Select(id => $"🏅  {Ui.T($"badge.{id}.name")}\n{Ui.T($"badge.{id}.desc")}");
                await ShowAsync(Ui.T("celebrate.badge.title"), string.Join("\n\n", lines));
            }
        }
        catch (Exception ex)
        {
            App.Host.Get<IAppLogger>().Error("Hiện ăn mừng lỗi", ex);
        }
    }

    private async Task ShowAsync(string title, string body)
    {
        if (!DialogsEnabled || _dialogOpen || _root() is not { } root)
        {
            return;
        }

        _dialogOpen = true;

        try
        {
            await new ContentDialog
            {
                XamlRoot = root,
                Title = title,
                Content = new TextBlock { Text = body, TextWrapping = TextWrapping.Wrap },
                CloseButtonText = Ui.T("celebrate.close"),
                DefaultButton = ContentDialogButton.Close
            }.ShowAsync();
        }
        finally
        {
            _dialogOpen = false;
        }
    }

    private async Task AskFreezeAsync(FreezePlan plan)
    {
        if (!DialogsEnabled || _dialogOpen || _root() is not { } root)
        {
            return;
        }

        _dialogOpen = true;

        try
        {
            var result = await new ContentDialog
            {
                XamlRoot = root,
                Title = Ui.T("freeze.ask.title"),
                Content = new TextBlock { Text = "❄  " + Ui.Loc.Format("freeze.ask.body", plan.TokensNeeded), TextWrapping = TextWrapping.Wrap },
                PrimaryButtonText = Ui.T("freeze.use"),
                CloseButtonText = Ui.T("freeze.skip"),
                DefaultButton = ContentDialogButton.Primary
            }.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                await _service.ApplyFreezeAsync(plan);
                await RefreshSafelyAsync();
            }
        }
        finally
        {
            _dialogOpen = false;
        }
    }
}
