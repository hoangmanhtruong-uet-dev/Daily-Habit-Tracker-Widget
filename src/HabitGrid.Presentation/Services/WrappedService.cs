using HabitGrid.Core.Domain;
using HabitGrid.Core.Domain.Insights;
using IDayProvider = HabitGrid.Core.Services.IDayProvider;

namespace HabitGrid.Presentation.Services;

/// <summary>Wrapped tháng (P1): gom thói quen + nhật ký của tháng và tháng trước để dựng số liệu. Xem đủ 6 slide và xuất ảnh là quyền Plus (kiểm ở UI).</summary>
public sealed class WrappedService
{
    private readonly IHabitRepository _habits;
    private readonly IDayLogRepository _logs;
    private readonly IDayProvider _days;
    private readonly IEntitlements _entitlements;

    public WrappedService(IHabitRepository habits, IDayLogRepository logs, IDayProvider days, IEntitlements entitlements)
    {
        _habits = habits ?? throw new ArgumentNullException(nameof(habits));
        _logs = logs ?? throw new ArgumentNullException(nameof(logs));
        _days = days ?? throw new ArgumentNullException(nameof(days));
        _entitlements = entitlements ?? throw new ArgumentNullException(nameof(entitlements));
    }

    public bool IsPlus => _entitlements.IsPlus;

    public DateOnly Today => _days.GetToday();

    /// <summary>Tháng hiện tại (tính đến hôm nay) hoặc tháng trước (<paramref name="monthsAgo"/> = 1).</summary>
    public async Task<MonthlyWrapped> BuildAsync(int monthsAgo = 1, CancellationToken ct = default)
    {
        var today = _days.GetToday();
        var month = PeriodCalendar.StartOfMonth(today).AddMonths(-Math.Max(0, monthsAgo));
        var habits = await _habits.GetAllAsync(ct);
        var logs = new List<DayLog>();
        var from = month.AddMonths(-1);
        var to = PeriodCalendar.EndOfMonth(month);

        foreach (var habit in habits.Where(h => h.DeletedAt is null))
        {
            logs.AddRange(await _logs.GetRangeAsync(habit.Id, from, to, ct));
        }

        return MonthlyWrappedBuilder.Build(habits, logs, month, today);
    }
}
