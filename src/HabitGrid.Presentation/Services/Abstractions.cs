namespace HabitGrid.Presentation.Services;

/// <summary>Đưa một việc về luồng UI. Tách khỏi WinUI để ViewModel test được bằng xUnit thường.</summary>
public interface IUiDispatcher
{
    void Post(Action action);
}

/// <summary>Chạy ngay trên luồng gọi (dùng trong test và khi chưa có UI).</summary>
public sealed class ImmediateDispatcher : IUiDispatcher
{
    public void Post(Action action) => action();
}

/// <summary>Ghi lỗi không làm sập app. Hiện thực thật ghi file log.</summary>
public interface IAppLogger
{
    void Info(string message);

    void Error(string message, Exception? exception = null);
}

public sealed class NullLogger : IAppLogger
{
    public static readonly NullLogger Instance = new();

    public void Info(string message)
    {
    }

    public void Error(string message, Exception? exception = null)
    {
    }
}
