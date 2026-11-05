using HabitGrid.Core.Repositories;

namespace HabitGrid.Core.Tests;

/// <summary>
/// Bảng cài đặt giả lập, lưu trong bộ nhớ để kiểm thử tầng Domain mà không cần CSDL.
/// </summary>
public sealed class InMemorySettingsRepository : ISettingsRepository
{
    private readonly Dictionary<string, string> _values = new();

    public IReadOnlyDictionary<string, string> Values => _values;

    public Task<string?> GetAsync(string key)
    {
        return Task.FromResult(_values.TryGetValue(key, out var value) ? value : null);
    }

    public async Task<T> GetAsync<T>(string key, T defaultValue)
    {
        if (!_values.TryGetValue(key, out var raw))
        {
            return defaultValue;
        }

        if (typeof(T) == typeof(string))
        {
            return (T)(object)raw;
        }

        try
        {
            var value = System.Text.Json.JsonSerializer.Deserialize<T>(raw);
            return value is null ? defaultValue : value;
        }
        catch (System.Text.Json.JsonException)
        {
            return defaultValue;
        }
    }

    public Task SetAsync<T>(string key, T value)
    {
        _values[key] = value is string text
            ? text
            : System.Text.Json.JsonSerializer.Serialize(value);

        return Task.CompletedTask;
    }
}
