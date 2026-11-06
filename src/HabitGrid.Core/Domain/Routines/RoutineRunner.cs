namespace HabitGrid.Core.Domain.Routines;

public sealed record TimedStep(Guid HabitId, string Name, int Minutes);

/// <param name="Completed">true nếu hết giờ hoặc bấm "Xong"; false nếu bị bỏ qua.</param>
public sealed record StepOutcome(TimedStep Step, bool Completed);

/// <summary>
/// Chạy routine nối tiếp từng bước có thời lượng (P4), thuần và không tự chạy: nhận thời điểm hiện tại qua tham số nên test được.
/// Hết giờ một bước thì tự sang bước kế và báo "hoàn thành"; một lần <see cref="Tick"/> sau khoảng nghỉ dài (máy ngủ) có thể kết thúc nhiều bước.
/// Tạm dừng không tính thời gian.
/// </summary>
public sealed class RoutineRunner
{
    public const int MinMinutes = 1;
    public const int MaxMinutes = 120;
    public const int DefaultMinutes = 5;

    private readonly IReadOnlyList<TimedStep> _steps;
    private double _accumulated;
    private DateTime? _runningSince;

    public RoutineRunner(IReadOnlyList<TimedStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);

        if (steps.Any(s => s.Minutes is < MinMinutes or > MaxMinutes))
        {
            throw new ArgumentOutOfRangeException(nameof(steps), $"Thời lượng mỗi bước phải từ {MinMinutes} đến {MaxMinutes} phút.");
        }

        _steps = steps;
    }

    public IReadOnlyList<TimedStep> Steps => _steps;

    /// <summary>Chỉ số bước đang chạy; bằng số bước khi đã xong.</summary>
    public int Index { get; private set; }

    public bool IsFinished => Index >= _steps.Count;

    public bool IsRunning => _runningSince is not null;

    public bool IsPaused => !IsFinished && _runningSince is null && _started;

    public TimedStep? Current => IsFinished ? null : _steps[Index];

    private bool _started;

    public void Start(DateTime nowUtc)
    {
        if (_started || _steps.Count == 0)
        {
            return;
        }

        _started = true;
        _runningSince = nowUtc;
    }

    public void Pause(DateTime nowUtc)
    {
        if (_runningSince is { } since)
        {
            _accumulated += Math.Max(0, (nowUtc - since).TotalSeconds);
            _runningSince = null;
        }
    }

    public void Resume(DateTime nowUtc)
    {
        if (_started && !IsFinished && _runningSince is null)
        {
            _runningSince = nowUtc;
        }
    }

    /// <summary>Thời gian đã trôi của bước hiện tại.</summary>
    public TimeSpan Elapsed(DateTime nowUtc) =>
        TimeSpan.FromSeconds(_accumulated + (_runningSince is { } since ? Math.Max(0, (nowUtc - since).TotalSeconds) : 0));

    public TimeSpan Remaining(DateTime nowUtc)
    {
        if (Current is not { } step)
        {
            return TimeSpan.Zero;
        }

        var left = TimeSpan.FromMinutes(step.Minutes) - Elapsed(nowUtc);
        return left > TimeSpan.Zero ? left : TimeSpan.Zero;
    }

    /// <summary>Tổng thời gian còn lại của bước hiện tại và các bước sau.</summary>
    public TimeSpan TotalRemaining(DateTime nowUtc) =>
        Remaining(nowUtc) + TimeSpan.FromMinutes(_steps.Skip(Index + 1).Sum(s => s.Minutes));

    /// <summary>Cập nhật theo đồng hồ; trả về các bước vừa hết giờ (đã tự chuyển sang bước kế).</summary>
    public IReadOnlyList<StepOutcome> Tick(DateTime nowUtc)
    {
        var finished = new List<StepOutcome>();

        while (!IsFinished && _runningSince is not null)
        {
            var total = TimeSpan.FromMinutes(_steps[Index].Minutes).TotalSeconds;
            var elapsed = _accumulated + Math.Max(0, (nowUtc - _runningSince.Value).TotalSeconds);

            if (elapsed < total)
            {
                break;
            }

            // Phần dư chuyển sang bước kế: bước kế bắt đầu đúng lúc bước trước hết giờ.
            var overflow = elapsed - total;
            finished.Add(new StepOutcome(_steps[Index], true));
            Index++;
            _accumulated = 0;
            _runningSince = IsFinished ? null : nowUtc - TimeSpan.FromSeconds(overflow);
        }

        return finished;
    }

    /// <summary>Bấm "Xong" sớm: tính hoàn thành và sang bước kế.</summary>
    public StepOutcome? CompleteCurrent(DateTime nowUtc) => Advance(nowUtc, completed: true);

    /// <summary>Bỏ qua bước hiện tại (không đánh dấu xong) và sang bước kế.</summary>
    public StepOutcome? SkipCurrent(DateTime nowUtc) => Advance(nowUtc, completed: false);

    private StepOutcome? Advance(DateTime nowUtc, bool completed)
    {
        if (IsFinished || !_started)
        {
            return null;
        }

        var wasPaused = _runningSince is null;
        var outcome = new StepOutcome(_steps[Index], completed);
        Index++;
        _accumulated = 0;
        _runningSince = IsFinished || wasPaused ? null : nowUtc;
        return outcome;
    }
}
