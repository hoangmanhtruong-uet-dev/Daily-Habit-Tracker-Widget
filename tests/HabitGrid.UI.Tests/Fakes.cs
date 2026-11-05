using HabitGrid.Core.Models;
using HabitGrid.UI.Services;

namespace HabitGrid.UI.Tests;

/// <summary>
/// Giả lập dịch vụ hộp thoại để kiểm thử logic ViewModel mà không cần mở cửa sổ WPF.
/// </summary>
public sealed class FakeDialogService : IDialogService
{
    public int PaywallShownCount { get; private set; }

    public int EditDialogShownCount { get; private set; }

    public int OnboardingShownCount { get; private set; }

    public string? LastPaywallReason { get; private set; }

    /// <summary>Kết quả trả về cho hộp thoại tạo/sửa.</summary>
    public HabitEditResult? EditResult { get; set; }

    /// <summary>Kết quả trả về cho luồng làm quen.</summary>
    public OnboardingChoice? OnboardingResult { get; set; }

    /// <summary>True nếu người dùng "đã mua" trong paywall.</summary>
    public bool PaywallPurchased { get; set; }

    public List<string> Toasts { get; } = new();

    public Task<HabitEditResult?> ShowEditHabitAsync(Habit? existing)
    {
        EditDialogShownCount++;
        return Task.FromResult(EditResult);
    }

    public Task<bool> ShowPaywallAsync(string? reason)
    {
        PaywallShownCount++;
        LastPaywallReason = reason;
        return Task.FromResult(PaywallPurchased);
    }

    public Task<OnboardingChoice?> ShowOnboardingAsync()
    {
        OnboardingShownCount++;
        return Task.FromResult(OnboardingResult);
    }

    public void ShowToast(string message, string? actionText = null, Action? action = null)
    {
        Toasts.Add(message);
        action?.Invoke();
    }
}

/// <summary>
/// Hẹn giờ giả lập: cho phép kiểm thử thời gian hoàn tác xóa một cách xác định.
/// </summary>
public sealed class FakeDeletionTimer : IDeletionTimer
{
    private Action? _callback;

    public bool HasScheduled { get; private set; }

    public IDisposable Schedule(TimeSpan delay, Action callback)
    {
        _callback = callback;
        HasScheduled = true;
        return new Registration(this);
    }

    /// <summary>Kích hoạt hẹn giờ (mô phỏng hết 10 giây).</summary>
    public void Fire()
    {
        _callback?.Invoke();
    }

    private sealed class Registration : IDisposable
    {
        private readonly FakeDeletionTimer _owner;

        public Registration(FakeDeletionTimer owner) => _owner = owner;

        public void Dispose() => _owner.HasScheduled = false;
    }
}
