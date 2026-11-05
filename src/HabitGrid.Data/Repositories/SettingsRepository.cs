using System.Text.Json;
using HabitGrid.Core.Repositories;

namespace HabitGrid.Data.Repositories;

/// <summary>
/// Hiện thực <see cref="ISettingsRepository"/> trên SQLite.
/// Giá trị kiểu chuỗi được lưu nguyên văn, các kiểu khác được lưu dưới dạng JSON.
/// </summary>
public sealed class SettingsRepository : ISettingsRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HabitGridDatabase _database;

    public SettingsRepository(HabitGridDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _database = database;
    }

    public async Task<string?> GetAsync(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        await using var connection = await _database.OpenConnectionAsync();
        using var command = connection.CreateCommand();

        command.CommandText = "SELECT value FROM settings WHERE key = $key;";
        command.Parameters.AddWithValue("$key", key);

        var result = await command.ExecuteScalarAsync();
        return result is null or DBNull ? null : Convert.ToString(result);
    }

    public async Task<T> GetAsync<T>(string key, T defaultValue)
    {
        var raw = await GetAsync(key);

        if (raw is null)
        {
            return defaultValue;
        }

        if (typeof(T) == typeof(string))
        {
            return (T)(object)raw;
        }

        try
        {
            var value = JsonSerializer.Deserialize<T>(raw, JsonOptions);
            return value is null ? defaultValue : value;
        }
        catch (JsonException)
        {
            // Dữ liệu cài đặt bị hỏng: dùng giá trị mặc định thay vì làm sập ứng dụng.
            return defaultValue;
        }
    }

    public async Task SetAsync<T>(string key, T value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        await using var connection = await _database.OpenConnectionAsync();
        await using var transaction = connection.BeginTransaction();

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT OR REPLACE INTO settings (key, value) VALUES ($key, $value);";
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", Serialize(value));

        await command.ExecuteNonQueryAsync();

        transaction.Commit();
    }

    private static string Serialize<T>(T value)
    {
        return value is string text
            ? text
            : JsonSerializer.Serialize(value, JsonOptions);
    }
}
