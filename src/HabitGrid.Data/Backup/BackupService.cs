using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using HabitGrid.Core.Models;
using HabitGrid.Core.Repositories;
using HabitGrid.Data.Repositories;
using Microsoft.Data.Sqlite;

namespace HabitGrid.Data.Backup;

/// <summary>
/// Hiện thực <see cref="IBackupService"/>.
///
/// Nguyên tắc an toàn:
/// - Chế độ Replace luôn tạo bản sao lưu CSDL hiện tại trước khi ghi.
/// - Mọi thay đổi nằm trong một transaction: lỗi thì rollback, dữ liệu cũ được giữ nguyên.
/// - Người dùng Free vẫn được nạp đầy đủ dữ liệu; thói quen vượt hạn mức chỉ bị gắn cờ chỉ đọc.
/// </summary>
public sealed class BackupService : IBackupService
{
    /// <summary>Số thói quen tối đa của gói Free.</summary>
    public const int FreeHabitLimit = 2;

    /// <summary>Tên ứng dụng ghi trong file sao lưu.</summary>
    public const string AppName = "HabitGrid";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,

        // Giữ nguyên chữ tiếng Việt trong file sao lưu thay vì escape thành \uXXXX.
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly HabitGridDatabase _database;
    private readonly IHabitRepository _habitRepository;
    private readonly IEntryRepository _entryRepository;
    private readonly string? _backupDirectory;

    public BackupService(
        HabitGridDatabase database,
        IHabitRepository habitRepository,
        IEntryRepository entryRepository,
        string? backupDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(habitRepository);
        ArgumentNullException.ThrowIfNull(entryRepository);

        _database = database;
        _habitRepository = habitRepository;
        _entryRepository = entryRepository;
        _backupDirectory = backupDirectory;
    }

    public async Task<string> ExportJsonAsync()
    {
        var habits = await _habitRepository.GetAllIncludingArchivedAsync();

        var entries = new List<Entry>();
        foreach (var habit in habits)
        {
            entries.AddRange(await _entryRepository.GetAllForHabitAsync(habit.Id));
        }

        var settings = await ReadSettingsAsync();

        var data = new BackupData(
            AppName,
            HabitGridDatabase.CurrentSchemaVersion,
            DateTime.UtcNow,
            habits,
            entries,
            settings);

        return JsonSerializer.Serialize(data, JsonOptions);
    }

    public async Task<byte[]> ExportCsvForHabitAsync(Guid habitId)
    {
        var habit = await _habitRepository.GetByIdAsync(habitId)
                    ?? throw new HabitNotFoundException(habitId);

        var entries = await _entryRepository.GetAllForHabitAsync(habitId);

        var builder = new StringBuilder();
        builder.AppendLine("Date,Value,Status");

        foreach (var entry in entries)
        {
            var status = entry.IsCompleted(habit) ? "Done" : "NotDone";
            builder.Append(entry.Day.ToString(SqlMapping.DateFormat, CultureInfo.InvariantCulture))
                   .Append(',')
                   .Append(entry.Value.ToString(CultureInfo.InvariantCulture))
                   .Append(',')
                   .Append(status)
                   .AppendLine();
        }

        // BOM để Excel nhận đúng mã hoá UTF-8.
        var content = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(builder.ToString());

        var result = new byte[content.Length + body.Length];
        Buffer.BlockCopy(content, 0, result, 0, content.Length);
        Buffer.BlockCopy(body, 0, result, content.Length, body.Length);

        return result;
    }

    public async Task ImportJsonAsync(string jsonContent, ImportMode mode, bool isProUser)
    {
        ArgumentNullException.ThrowIfNull(jsonContent);

        var data = Deserialize(jsonContent);

        if (data.SchemaVersion > HabitGridDatabase.CurrentSchemaVersion)
        {
            throw new UnsupportedSchemaVersionException(data.SchemaVersion, HabitGridDatabase.CurrentSchemaVersion);
        }

        if (mode == ImportMode.Replace)
        {
            // VACUUM INTO không chạy được bên trong transaction nên phải sao lưu trước.
            await CreatePreImportBackupAsync();
        }

        await using var connection = await _database.OpenConnectionAsync();
        await using var transaction = connection.BeginTransaction();

        try
        {
            if (mode == ImportMode.Replace)
            {
                await ClearAllDataAsync(connection, transaction);
                await InsertHabitsAsync(connection, transaction, data.Habits);
                await InsertEntriesAsync(connection, transaction, data.Entries, overwrite: true);
                await InsertSettingsAsync(connection, transaction, data.Settings, overwrite: true);
            }
            else
            {
                await InsertHabitsAsync(connection, transaction, data.Habits, ignoreExisting: true);
                await InsertEntriesAsync(connection, transaction, data.Entries, overwrite: false);
                await InsertSettingsAsync(connection, transaction, data.Settings, overwrite: false);
            }

            if (!isProUser)
            {
                await ApplyFreeLimitAsync(connection, transaction);
            }
            else
            {
                await ClearReadOnlyFlagsAsync(connection, transaction);
            }

            transaction.Commit();
        }
        catch (Exception ex)
        {
            try
            {
                transaction.Rollback();
            }
            catch (InvalidOperationException)
            {
                // Transaction đã được rollback tự động.
            }

            throw new BackupImportException("Không thể nạp dữ liệu từ bản sao lưu. Dữ liệu hiện tại được giữ nguyên.", ex);
        }
    }

    private static BackupData Deserialize(string jsonContent)
    {
        try
        {
            var data = JsonSerializer.Deserialize<BackupData>(jsonContent, JsonOptions);

            if (data is null)
            {
                throw new BackupImportException("File sao lưu rỗng hoặc không hợp lệ.");
            }

            return data with
            {
                Habits = data.Habits ?? Array.Empty<Habit>(),
                Entries = data.Entries ?? Array.Empty<Entry>(),
                Settings = data.Settings ?? new Dictionary<string, string>()
            };
        }
        catch (JsonException ex)
        {
            throw new BackupImportException("File sao lưu không phải là JSON hợp lệ.", ex);
        }
    }

    private async Task<IReadOnlyDictionary<string, string>> ReadSettingsAsync()
    {
        await using var connection = await _database.OpenConnectionAsync();
        using var command = connection.CreateCommand();

        command.CommandText = "SELECT key, value FROM settings;";

        var settings = new Dictionary<string, string>();

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            settings[reader.GetString(0)] = reader.GetString(1);
        }

        return settings;
    }

    /// <summary>
    /// Tạo bản sao lưu CSDL hiện tại trước khi nạp theo chế độ Replace.
    /// Đường dẫn: %LocalAppData%\HabitGrid\backups\pre-import-{timestamp}.db
    /// </summary>
    private async Task CreatePreImportBackupAsync()
    {
        if (_database.IsInMemory)
        {
            return;
        }

        var directory = _backupDirectory ?? HabitGridDatabase.DefaultBackupDirectory;
        Directory.CreateDirectory(directory);

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var backupPath = Path.Combine(directory, $"pre-import-{timestamp}.db");

        if (File.Exists(backupPath))
        {
            File.Delete(backupPath);
        }

        await using var connection = await _database.OpenConnectionAsync();
        using var command = connection.CreateCommand();

        command.CommandText = "VACUUM INTO $path;";
        command.Parameters.AddWithValue("$path", backupPath);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task ClearAllDataAsync(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;

        // entries bị xóa tự động nhờ ON DELETE CASCADE.
        command.CommandText = """
            DELETE FROM widget_state;
            DELETE FROM settings;
            DELETE FROM habits;
            """;

        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertHabitsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        IReadOnlyList<Habit> habits,
        bool ignoreExisting = false,
        bool overwrite = true)
    {
        if (habits.Count == 0)
        {
            return;
        }

        using var command = connection.CreateCommand();
        command.Transaction = transaction;

        // Chế độ Merge: giữ nguyên bản ghi hiện có nếu định danh đã tồn tại.
        // Chế độ Replace: ghi đè hoàn toàn bản ghi cũ.
        command.CommandText = ignoreExisting
            ? """
              INSERT INTO habits (
                id, name, icon, kind, target, schedule_mask, palette_id, custom_color,
                start_date, reminder_time, reminder_mask, sort_order,
                archived_at, deleted_at, created_at, read_only)
              VALUES (
                $id, $name, $icon, $kind, $target, $scheduleMask, $paletteId, $customColor,
                $startDate, $reminderTime, $reminderMask, $sortOrder,
                $archivedAt, $deletedAt, $createdAt, $readOnly)
              ON CONFLICT(id) DO NOTHING;
              """
            : """
              INSERT INTO habits (
                id, name, icon, kind, target, schedule_mask, palette_id, custom_color,
                start_date, reminder_time, reminder_mask, sort_order,
                archived_at, deleted_at, created_at, read_only)
              VALUES (
                $id, $name, $icon, $kind, $target, $scheduleMask, $paletteId, $customColor,
                $startDate, $reminderTime, $reminderMask, $sortOrder,
                $archivedAt, $deletedAt, $createdAt, $readOnly)
              ON CONFLICT(id) DO UPDATE SET
                name = excluded.name,
                icon = excluded.icon,
                kind = excluded.kind,
                target = excluded.target,
                schedule_mask = excluded.schedule_mask,
                palette_id = excluded.palette_id,
                custom_color = excluded.custom_color,
                start_date = excluded.start_date,
                reminder_time = excluded.reminder_time,
                reminder_mask = excluded.reminder_mask,
                sort_order = excluded.sort_order,
                archived_at = excluded.archived_at,
                deleted_at = excluded.deleted_at,
                read_only = excluded.read_only;
              """;

        foreach (var habit in habits)
        {
            command.Parameters.Clear();
            command.Parameters.AddWithValue("$id", habit.Id.ToString("D"));
            command.Parameters.AddWithValue("$name", habit.Name);
            command.Parameters.AddWithValue("$icon", habit.Icon);
            command.Parameters.AddWithValue("$kind", (int)habit.Kind);
            command.Parameters.AddWithValue("$target", habit.Target);
            command.Parameters.AddWithValue("$scheduleMask", (int)habit.ScheduleMask);
            command.Parameters.AddWithValue("$paletteId", habit.PaletteId);
            command.Parameters.AddWithValue("$customColor", (object?)habit.CustomColorHex ?? DBNull.Value);
            command.Parameters.AddWithValue("$startDate", SqlMapping.ToSqlDate(habit.StartDate));
            command.Parameters.AddWithValue("$reminderTime", (object?)SqlMapping.ToSqlTime(habit.ReminderTime) ?? DBNull.Value);
            command.Parameters.AddWithValue("$reminderMask", (int)habit.ReminderMask);
            command.Parameters.AddWithValue("$sortOrder", habit.SortOrder);
            command.Parameters.AddWithValue("$archivedAt", (object?)SqlMapping.ToSqlTimestamp(habit.ArchivedAt) ?? DBNull.Value);
            command.Parameters.AddWithValue("$deletedAt", (object?)SqlMapping.ToSqlTimestamp(habit.DeletedAt) ?? DBNull.Value);
            command.Parameters.AddWithValue("$createdAt", SqlMapping.ToSqlTimestamp(habit.CreatedAt));
            command.Parameters.AddWithValue("$readOnly", habit.IsReadOnly ? 1 : 0);

            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task InsertEntriesAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        IReadOnlyList<Entry> entries,
        bool overwrite)
    {
        if (entries.Count == 0)
        {
            return;
        }

        using var command = connection.CreateCommand();
        command.Transaction = transaction;

        // Chế độ Merge: khi trùng ngày thì giữ giá trị lớn hơn.
        // Chế độ Replace: ghi đè hoàn toàn.
        command.CommandText = overwrite
            ? """
              INSERT INTO entries (habit_id, day, value, updated_at)
              VALUES ($habitId, $day, $value, $updatedAt)
              ON CONFLICT(habit_id, day) DO UPDATE SET
                  value = excluded.value,
                  updated_at = excluded.updated_at;
              """
            : """
              INSERT INTO entries (habit_id, day, value, updated_at)
              VALUES ($habitId, $day, $value, $updatedAt)
              ON CONFLICT(habit_id, day) DO UPDATE SET
                  value = MAX(entries.value, excluded.value),
                  updated_at = excluded.updated_at;
              """;

        foreach (var entry in entries)
        {
            command.Parameters.Clear();
            command.Parameters.AddWithValue("$habitId", entry.HabitId.ToString("D"));
            command.Parameters.AddWithValue("$day", SqlMapping.ToSqlDate(entry.Day));
            command.Parameters.AddWithValue("$value", entry.Value);
            command.Parameters.AddWithValue("$updatedAt", SqlMapping.ToSqlTimestamp(entry.UpdatedAt));

            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task InsertSettingsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        IReadOnlyDictionary<string, string> settings,
        bool overwrite)
    {
        if (settings.Count == 0)
        {
            return;
        }

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = overwrite
            ? """
              INSERT INTO settings (key, value) VALUES ($key, $value)
              ON CONFLICT(key) DO UPDATE SET value = excluded.value;
              """
            : """
              INSERT INTO settings (key, value) VALUES ($key, $value)
              ON CONFLICT(key) DO NOTHING;
              """;

        foreach (var setting in settings)
        {
            command.Parameters.Clear();
            command.Parameters.AddWithValue("$key", setting.Key);
            command.Parameters.AddWithValue("$value", setting.Value);

            await command.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// Áp dụng hạn mức gói Free: 2 thói quen đầu theo thứ tự hiển thị được sửa,
    /// các thói quen còn lại chỉ đọc. Tuyệt đối không xóa dữ liệu.
    /// </summary>
    private static async Task ApplyFreeLimitAsync(SqliteConnection connection, SqliteTransaction transaction)
    {
        var habitIds = new List<Guid>();

        using (var selectCommand = connection.CreateCommand())
        {
            selectCommand.Transaction = transaction;
            selectCommand.CommandText = "SELECT id FROM habits WHERE deleted_at IS NULL ORDER BY sort_order, created_at;";

            await using var reader = await selectCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                habitIds.Add(Guid.Parse(reader.GetString(0)));
            }
        }

        using var updateCommand = connection.CreateCommand();
        updateCommand.Transaction = transaction;
        updateCommand.CommandText = "UPDATE habits SET read_only = $readOnly WHERE id = $id;";

        for (var index = 0; index < habitIds.Count; index++)
        {
            updateCommand.Parameters.Clear();
            updateCommand.Parameters.AddWithValue("$id", habitIds[index].ToString("D"));
            updateCommand.Parameters.AddWithValue("$readOnly", index >= FreeHabitLimit ? 1 : 0);

            await updateCommand.ExecuteNonQueryAsync();
        }
    }

    private static async Task ClearReadOnlyFlagsAsync(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE habits SET read_only = 0;";

        await command.ExecuteNonQueryAsync();
    }
}
