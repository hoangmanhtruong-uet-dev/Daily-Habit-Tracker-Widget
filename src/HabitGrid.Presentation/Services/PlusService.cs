using HabitGrid.Core.Domain;
using HabitGrid.Core.Domain.Monetization;

namespace HabitGrid.Presentation.Services;

/// <summary>Lưu trạng thái giấy phép đã xác minh (bản thật mã hóa DPAPI theo người dùng Windows).</summary>
public interface ILicenseCache
{
    LicenseState? Load();

    void Save(LicenseState state);
}

public sealed class MemoryLicenseCache : ILicenseCache
{
    private LicenseState? _state;

    public LicenseState? Load() => _state;

    public void Save(LicenseState state) => _state = state;
}

/// <summary>
/// Tập thói quen "chỉ đọc" sau khi hạ cấp, dùng chung cho mọi đường ghi (trang Hôm nay, widget, toast, palette).
/// Lưu trong settings (<see cref="Key"/>) để WidgetHost — tiến trình khác — cũng tôn trọng.
/// </summary>
public static class ReadOnlyHabits
{
    public const string Key = "plan.readonly";
    public const string RunningKey = "plan.running";

    public static async Task<IReadOnlySet<Guid>> LoadAsync(ISettingsStore settings, CancellationToken ct = default) =>
        Parse(await settings.GetAsync(Key, ct));

    public static IReadOnlySet<Guid> Parse(string? raw) =>
        (raw ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => Guid.TryParse(s, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .ToHashSet();

    public static string Format(IEnumerable<Guid> ids) => string.Join(',', ids);
}

/// <summary>
/// Điều phối HabitGrid Plus (SPEC-V2-10): đọc quyền từ Store, cache offline, áp quyền "sống", hạ cấp không mất dữ liệu.
/// Mua hàng chỉ đi qua <see cref="PurchaseAsync"/>, mà chỉ <see cref="ViewModels.PaywallViewModel.BuyAsync"/> gọi.
/// </summary>
public sealed class PlusService
{
    private readonly IPlusStore _store;
    private readonly ILicenseCache _cache;
    private readonly LiveEntitlements _live;
    private readonly ISettingsStore _settings;
    private readonly IHabitRepository _habits;
    private readonly Func<DateTime> _utcNow;

    public PlusService(
        IPlusStore store,
        ILicenseCache cache,
        LiveEntitlements live,
        ISettingsStore settings,
        IHabitRepository habits,
        Func<DateTime>? utcNow = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _live = live ?? throw new ArgumentNullException(nameof(live));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _habits = habits ?? throw new ArgumentNullException(nameof(habits));
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
        Status = LicensePolicy.Evaluate(null, _utcNow());
    }

    public LicenseStatus Status { get; private set; }

    public LicenseState? State { get; private set; }

    public bool StoreAvailable => _store.IsAvailable;

    /// <summary>Quyền hoặc danh sách chỉ đọc đổi.</summary>
    public event EventHandler? Changed;

    /// <summary>Khi mở app: áp quyền từ cache ngay (offline vẫn đúng), rồi xác minh với Store nếu được.</summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        Apply(_cache.Load());
        await RefreshAsync(ct);
        await ApplyPromoAsync(ct);
        await UpdateAllowanceAsync(ct);
    }

    /// <summary>Quyền Plus tặng đang chạy (nếu có), để giao diện hiện "còn N ngày".</summary>
    public LicenseState? Promo { get; private set; }

    public int PromoDaysLeft => Promo is null ? 0 : PlusPromo.DaysLeft(Promo, _utcNow());

    public const string PromoStartedKey = "promo.startedAt";

    /// <summary>Có tặng Plus miễn phí cho người dùng mới không (mặc định theo <see cref="PlusPromo.Enabled"/>).</summary>
    public bool PromoEnabled { get; init; } = PlusPromo.Enabled;

    /// <summary>
    /// Tặng Plus <see cref="PlusPromo.Days"/> ngày tính từ lần đầu mở app (lưu ngày bắt đầu, không gia hạn lại).
    /// Chỉ áp khi người dùng chưa có Plus thật; mua trên Store luôn được ưu tiên.
    /// </summary>
    private async Task ApplyPromoAsync(CancellationToken ct)
    {
        var started = await _settings.GetAsync(PromoStartedKey, ct);

        if (started is null)
        {
            if (!PromoEnabled)
            {
                return;
            }

            started = _utcNow().ToString("O", System.Globalization.CultureInfo.InvariantCulture);
            await _settings.SetAsync(PromoStartedKey, started, ct);
        }

        if (!DateTime.TryParse(started, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out var startedAt))
        {
            return;
        }

        Promo = PlusPromo.StateFor(startedAt.ToUniversalTime());

        if (!Status.IsPlus && LicensePolicy.Evaluate(Promo, _utcNow()).IsPlus)
        {
            Apply(Promo);
        }
    }

    /// <summary>Đọc lại quyền từ Store (cũng là "Khôi phục mua hàng"). Lỗi mạng thì giữ cache, không hạ quyền.</summary>
    public async Task<bool> RefreshAsync(CancellationToken ct = default)
    {
        if (!_store.IsAvailable)
        {
            return false;
        }

        try
        {
            var owned = await _store.GetOwnedAsync(ct);
            var state = LicensePolicy.FromOwned(owned, _utcNow());
            _cache.Save(state);
            Apply(state);

            // Store chưa có gì nhưng còn trong thời gian tặng: giữ Plus tặng.
            if (!Status.IsPlus && Promo is not null && LicensePolicy.Evaluate(Promo, _utcNow()).IsPlus)
            {
                Apply(Promo);
            }
            await UpdateAllowanceAsync(ct);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public Task<bool> RestoreAsync(CancellationToken ct = default) => RefreshAsync(ct);

    public Task<IReadOnlyList<PlusProduct>> GetProductsAsync(CancellationToken ct = default) => _store.GetProductsAsync(ct);

    /// <summary>Mua một gói. Chỉ gọi từ nút Mua người dùng bấm trên paywall.</summary>
    public async Task<PurchaseOutcome> PurchaseAsync(PlusPlan plan, IntPtr ownerWindow, CancellationToken ct = default)
    {
        if (!_store.IsAvailable)
        {
            return PurchaseOutcome.StoreUnavailable;
        }

        var outcome = await _store.PurchaseAsync(plan, ownerWindow, ct);

        if (outcome is PurchaseOutcome.Succeeded or PurchaseOutcome.AlreadyOwned)
        {
            await RefreshAsync(ct);
        }

        return outcome;
    }

    /// <summary>Kết quả hạ cấp hiện tại (thói quen nào đang chạy, thói quen nào chỉ đọc).</summary>
    public async Task<HabitAllowance.Result> GetAllowanceAsync(CancellationToken ct = default)
    {
        var habits = await _habits.GetAllAsync(ct);
        var chosen = ReadOnlyHabits.Parse(await _settings.GetAsync(ReadOnlyHabits.RunningKey, ct));
        return HabitAllowance.Evaluate(habits, chosen, _live.MaxActiveHabits);
    }

    /// <summary>Người dùng chọn các thói quen tiếp tục chạy sau khi hạ cấp.</summary>
    public async Task ChooseRunningAsync(IEnumerable<Guid> running, CancellationToken ct = default)
    {
        await _settings.SetAsync(ReadOnlyHabits.RunningKey, ReadOnlyHabits.Format(running.Take(_live.MaxActiveHabits)), ct);
        await UpdateAllowanceAsync(ct);
    }

    /// <summary>Tính lại danh sách chỉ đọc và lưu cho mọi tiến trình (gọi khi quyền đổi hoặc thói quen thêm/lưu trữ/xóa).</summary>
    public async Task<HabitAllowance.Result> UpdateAllowanceAsync(CancellationToken ct = default)
    {
        var result = await GetAllowanceAsync(ct);
        var formatted = ReadOnlyHabits.Format(result.ReadOnly.OrderBy(id => id));

        if (await _settings.GetAsync(ReadOnlyHabits.Key, ct) != formatted)
        {
            await _settings.SetAsync(ReadOnlyHabits.Key, formatted, ct);
            Changed?.Invoke(this, EventArgs.Empty);
        }

        return result;
    }

    private void Apply(LicenseState? state)
    {
        State = state;
        var before = Status;
        Status = LicensePolicy.Evaluate(state, _utcNow());
        _live.Set(Status.IsPlus);

        if (before != Status)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
