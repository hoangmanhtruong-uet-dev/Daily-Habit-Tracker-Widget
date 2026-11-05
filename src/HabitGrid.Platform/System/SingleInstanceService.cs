using System.IO.Pipes;
using System.Text.Json;

namespace HabitGrid.Platform.System;

/// <summary>
/// Hiện thực hóa Single Instance bằng Mutex hệ thống và Named Pipe IPC.
/// </summary>
public sealed class SingleInstanceService : ISingleInstanceService
{
    private const string PipeName = "HabitGrid_IPC_Pipe_f1107b88";
    private readonly string _mutexName;
    private Mutex? _mutex;
    private bool _hasAcquired;
    private CancellationTokenSource? _cts;
    private Task? _listenerTask;
    private bool _disposed;

    public SingleInstanceService(string? appIdentifier = null)
    {
        _mutexName = $"Global\\HabitGrid_Mutex_{appIdentifier ?? "Default"}";
    }

    public event EventHandler<string[]>? ArgumentsReceived;

    public bool TryAcquire()
    {
        if (_hasAcquired)
        {
            return true;
        }

        try
        {
            _mutex = new Mutex(true, _mutexName, out bool createdNew);
            if (!createdNew)
            {
                _mutex.Dispose();
                _mutex = null;
                return false;
            }

            _hasAcquired = true;
            StartPipeListener();
            return true;
        }
        catch (AbandonedMutexException)
        {
            _hasAcquired = true;
            StartPipeListener();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> NotifyExistingInstanceAsync(string[] args, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            await client.ConnectAsync(1500, cancellationToken).ConfigureAwait(false);

            var payload = JsonSerializer.Serialize(args);
            using var writer = new StreamWriter(client) { AutoFlush = true };
            await writer.WriteLineAsync(payload.AsMemory(), cancellationToken).ConfigureAwait(false);

            return true;
        }
        catch
        {
            return false;
        }
    }

    private void StartPipeListener()
    {
        _cts = new CancellationTokenSource();
        _listenerTask = Task.Run(() => ListenLoopAsync(_cts.Token));
    }

    private async Task ListenLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.In,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(token).ConfigureAwait(false);

                using var reader = new StreamReader(server);
                var line = await reader.ReadLineAsync(token).ConfigureAwait(false);

                if (!string.IsNullOrEmpty(line))
                {
                    var args = JsonSerializer.Deserialize<string[]>(line) ?? Array.Empty<string>();
                    ArgumentsReceived?.Invoke(this, args);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Thử lại nếu có lỗi pipe tạm thời
                try
                {
                    await Task.Delay(200, token).ConfigureAwait(false);
                }
                catch
                {
                    break;
                }
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
        }

        if (_mutex != null)
        {
            if (_hasAcquired)
            {
                try
                {
                    _mutex.ReleaseMutex();
                }
                catch
                {
                    // ignored
                }
            }

            _mutex.Dispose();
            _mutex = null;
        }
    }
}
