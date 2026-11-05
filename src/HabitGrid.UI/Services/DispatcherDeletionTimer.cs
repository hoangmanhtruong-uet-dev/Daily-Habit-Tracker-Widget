using System.Windows.Threading;

namespace HabitGrid.UI.Services;

/// <summary>
/// Hẹn giờ chạy trên Dispatcher của WPF.
/// </summary>
public sealed class DispatcherDeletionTimer : IDeletionTimer
{
    public IDisposable Schedule(TimeSpan delay, Action callback)
    {
        var timer = new DispatcherTimer { Interval = delay };

        void OnTick(object? sender, EventArgs e)
        {
            timer.Stop();
            timer.Tick -= OnTick;
            callback();
        }

        timer.Tick += OnTick;
        timer.Start();

        return new Registration(timer, OnTick);
    }

    private sealed class Registration : IDisposable
    {
        private readonly DispatcherTimer _timer;
        private readonly EventHandler _handler;
        private bool _disposed;

        public Registration(DispatcherTimer timer, EventHandler handler)
        {
            _timer = timer;
            _handler = handler;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _timer.Stop();
            _timer.Tick -= _handler;
        }
    }
}
