namespace HabitGrid.Core.Domain.Focus;

public enum FocusMode
{
    Pomodoro,
    Countdown,
    Stopwatch
}

public enum FocusPhase
{
    Idle,
    Work,
    Break,
    Finished
}

/// <summary>Trạng thái lưu được của bộ hẹn giờ (để khôi phục sau khi tiến trình khởi động lại). Mọi thời điểm là UTC.</summary>
public sealed record FocusState(
    FocusMode Mode,
    FocusPhase Phase,
    int WorkSeconds,
    int BreakSeconds,
    Guid? HabitId,
    DateTime? StartedAtUtc,
    double AccumulatedSeconds,
    DateTime? RunningSinceUtc,
    DateTime? BreakEndsAtUtc);

/// <summary>Kết quả một phiên làm việc đã kết thúc (để cộng phút vào thói quen và ghi nhật ký Focus).</summary>
public sealed record FocusResult(Guid? HabitId, DateTime StartedAtUtc, DateTime EndedAtUtc, int Minutes, FocusMode Mode, bool Completed);

/// <summary>
/// Bộ hẹn giờ Focus thuần (SPEC-V2-09 §2), không tự chạy: nhận thời điểm hiện tại qua tham số nên test được.
/// Pomodoro: làm việc N phút rồi nghỉ M phút. Đếm ngược: làm việc N phút. Bấm giờ: tới khi dừng.
/// Tạm dừng không tính thời gian. Kết thúc (tự hết giờ hoặc dừng tay) trả về số phút trọn vẹn đã làm.
/// </summary>
public sealed class FocusTimer
{
    private FocusState _state;

    public FocusTimer(FocusMode mode, TimeSpan work, TimeSpan breakLength, Guid? habitId)
    {
        if (mode != FocusMode.Stopwatch)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(work, TimeSpan.FromMinutes(1));
        }

        _state = new FocusState(mode, FocusPhase.Idle, (int)work.TotalSeconds, (int)breakLength.TotalSeconds, habitId, null, 0, null, null);
    }

    private FocusTimer(FocusState state) => _state = state;

    public static FocusTimer Restore(FocusState state) => new(state ?? throw new ArgumentNullException(nameof(state)));

    public FocusState State => _state;

    public FocusMode Mode => _state.Mode;

    public FocusPhase Phase => _state.Phase;

    public Guid? HabitId => _state.HabitId;

    public bool IsRunning => _state.RunningSinceUtc is not null;

    public bool IsPaused => _state.Phase == FocusPhase.Work && _state.RunningSinceUtc is null;

    public TimeSpan Elapsed(DateTime nowUtc) =>
        TimeSpan.FromSeconds(_state.AccumulatedSeconds + (_state.RunningSinceUtc is { } since ? Math.Max(0, (nowUtc - since).TotalSeconds) : 0));

    /// <summary>Thời gian còn lại của pha hiện tại (bấm giờ thì không có).</summary>
    public TimeSpan? Remaining(DateTime nowUtc) => _state.Phase switch
    {
        FocusPhase.Work when _state.Mode != FocusMode.Stopwatch => TimeSpan.FromSeconds(Math.Max(0, _state.WorkSeconds - Elapsed(nowUtc).TotalSeconds)),
        FocusPhase.Break when _state.BreakEndsAtUtc is { } end => end > nowUtc ? end - nowUtc : TimeSpan.Zero,
        _ => null
    };

    public void Start(DateTime nowUtc)
    {
        if (_state.Phase is FocusPhase.Work)
        {
            return;
        }

        _state = _state with
        {
            Phase = FocusPhase.Work,
            StartedAtUtc = nowUtc,
            AccumulatedSeconds = 0,
            RunningSinceUtc = nowUtc,
            BreakEndsAtUtc = null
        };
    }

    public void Pause(DateTime nowUtc)
    {
        if (_state is { Phase: FocusPhase.Work, RunningSinceUtc: { } since })
        {
            _state = _state with { AccumulatedSeconds = _state.AccumulatedSeconds + Math.Max(0, (nowUtc - since).TotalSeconds), RunningSinceUtc = null };
        }
    }

    public void Resume(DateTime nowUtc)
    {
        if (_state is { Phase: FocusPhase.Work, RunningSinceUtc: null })
        {
            _state = _state with { RunningSinceUtc = nowUtc };
        }
    }

    /// <summary>Bật/tắt: chưa chạy thì bắt đầu, đang chạy thì tạm dừng, đang dừng thì chạy tiếp.</summary>
    public void Toggle(DateTime nowUtc)
    {
        if (_state.Phase != FocusPhase.Work)
        {
            Start(nowUtc);
        }
        else if (IsRunning)
        {
            Pause(nowUtc);
        }
        else
        {
            Resume(nowUtc);
        }
    }

    /// <summary>Gọi định kỳ: tự kết thúc pha làm việc khi hết giờ (trả về kết quả đúng một lần), kết thúc nghỉ thì về Idle.</summary>
    public FocusResult? Tick(DateTime nowUtc)
    {
        if (_state.Phase == FocusPhase.Work && _state.Mode != FocusMode.Stopwatch && Elapsed(nowUtc).TotalSeconds >= _state.WorkSeconds)
        {
            // Thời điểm kết thúc thật = lúc đủ giờ, không phải lúc nhịp Tick đến (máy bận/ngủ).
            var overshoot = Elapsed(nowUtc).TotalSeconds - _state.WorkSeconds;
            var endedAt = nowUtc.AddSeconds(-overshoot);
            var result = new FocusResult(_state.HabitId, _state.StartedAtUtc ?? endedAt, endedAt, _state.WorkSeconds / 60, _state.Mode, true);

            _state = _state.Mode == FocusMode.Pomodoro && _state.BreakSeconds > 0
                ? _state with { Phase = FocusPhase.Break, AccumulatedSeconds = 0, RunningSinceUtc = null, BreakEndsAtUtc = endedAt.AddSeconds(_state.BreakSeconds) }
                : _state with { Phase = FocusPhase.Finished, AccumulatedSeconds = 0, RunningSinceUtc = null };
            return result;
        }

        if (_state is { Phase: FocusPhase.Break, BreakEndsAtUtc: { } end } && nowUtc >= end)
        {
            _state = _state with { Phase = FocusPhase.Idle, BreakEndsAtUtc = null };
        }

        return null;
    }

    /// <summary>Dừng tay. Đang làm việc thì trả về số phút trọn vẹn đã làm (0 phút thì null — không ghi gì).</summary>
    public FocusResult? Stop(DateTime nowUtc)
    {
        FocusResult? result = null;

        if (_state.Phase == FocusPhase.Work)
        {
            var minutes = (int)Math.Floor(Elapsed(nowUtc).TotalMinutes);

            if (minutes >= 1)
            {
                result = new FocusResult(_state.HabitId, _state.StartedAtUtc ?? nowUtc, nowUtc, minutes, _state.Mode, false);
            }
        }

        _state = _state with { Phase = FocusPhase.Idle, AccumulatedSeconds = 0, RunningSinceUtc = null, BreakEndsAtUtc = null, StartedAtUtc = null };
        return result;
    }
}

/// <summary>Một phiên Focus đã ghi (nhật ký, dùng cho XP và huy hiệu focus_10h).</summary>
public sealed record FocusRecord(Guid Id, Guid? HabitId, DateTime StartedAtUtc, DateTime EndedAtUtc, int Minutes, FocusMode Mode)
{
    /// <summary>Ngày (giờ địa phương) của phiên, theo lúc kết thúc.</summary>
    public DateOnly Day => DateOnly.FromDateTime(EndedAtUtc.Kind == DateTimeKind.Utc ? EndedAtUtc.ToLocalTime() : EndedAtUtc);
}
