using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using HabitGrid.Core.Domain;
using HabitGrid.Data;
using HabitGrid.Data.V3;
using HabitGrid.Presentation.Services;
using HabitGrid.Presentation.ViewModels;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using DayProvider = HabitGrid.Core.Services.DayProvider;
using IDayProvider = HabitGrid.Core.Services.IDayProvider;

namespace HabitGrid.WinUI;

/// <summary>Đường dẫn dữ liệu. Biến môi trường <c>HABITGRID_DATA_DIR</c> cho phép chạy thử trên thư mục riêng.</summary>
public static class AppPaths
{
    public const string DataDirVariable = "HABITGRID_DATA_DIR";

    public static bool IsCustomDataDir => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(DataDirVariable));

    public static string DataDir => IsCustomDataDir
        ? Path.GetFullPath(Environment.GetEnvironmentVariable(DataDirVariable)!)
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HabitGrid");

    public static string LegacyDatabase => Path.Combine(DataDir, "habitgrid.db");

    public static string Database => Path.Combine(DataDir, HabitGridV3.FileName);

    public static string LogDir => Path.Combine(DataDir, "logs");

    /// <summary>Khóa single-instance; thư mục dữ liệu riêng có khóa riêng để không đụng bản đang chạy thật.</summary>
    public static string InstanceKey => IsCustomDataDir
        ? "HabitGrid.Main." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(DataDir)))[..12]
        : "HabitGrid.Main";
}

/// <summary>Log ra file xoay theo ngày, giữ 7 ngày. Không bao giờ ném lỗi.</summary>
public sealed class FileLogger : IAppLogger
{
    private readonly string _directory;
    private readonly object _gate = new();

    public FileLogger(string directory)
    {
        _directory = directory;

        try
        {
            Directory.CreateDirectory(directory);
            foreach (var old in Directory.GetFiles(directory, "habitgrid-*.log")
                         .OrderByDescending(f => f, StringComparer.Ordinal)
                         .Skip(7))
            {
                File.Delete(old);
            }
        }
        catch
        {
            // Log không được làm app lỗi.
        }
    }

    public string CurrentFile => Path.Combine(_directory, $"habitgrid-{DateTime.Now:yyyyMMdd}.log");

    public void Info(string message) => Write("INFO", message);

    public void Error(string message, Exception? exception = null) =>
        Write("ERROR", exception is null ? message : $"{message}{Environment.NewLine}{exception}");

    private void Write(string level, string message)
    {
        try
        {
            lock (_gate)
            {
                File.AppendAllText(CurrentFile, $"{DateTime.Now:HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // nuốt lỗi ghi log
        }

        Debug.WriteLine($"[{level}] {message}");
    }
}

/// <summary>Đưa việc về luồng UI của WinUI.</summary>
public sealed class WinUiDispatcher : IUiDispatcher
{
    private readonly DispatcherQueue _queue;

    public WinUiDispatcher(DispatcherQueue queue) => _queue = queue;

    public void Post(Action action)
    {
        if (_queue.HasThreadAccess)
        {
            action();
        }
        else
        {
            _queue.TryEnqueue(() => action());
        }
    }
}

/// <summary>Composition root: dựng DI, mở CSDL v3, nhập dữ liệu v1 ở lần chạy đầu.</summary>
public sealed class AppHost : IDisposable
{
    private AppHost(ServiceProvider services, string? importNote)
    {
        Services = services;
        ImportNote = importNote;
    }

    public ServiceProvider Services { get; }

    /// <summary>Ghi chú về lần nhập dữ liệu cũ (nếu có), để hiển thị/ghi log.</summary>
    public string? ImportNote { get; }

    public static async Task<AppHost> CreateAsync(DispatcherQueue dispatcherQueue)
    {
        Directory.CreateDirectory(AppPaths.DataDir);
        var logger = new FileLogger(AppPaths.LogDir);
        logger.Info($"Khởi động. Dữ liệu: {AppPaths.DataDir}");

        var deviceId = ReadOrCreateDeviceId();
        var clock = new HlcClock(deviceId);

        // Lần chạy đầu: dựng v3 từ CSDL v1/v2 (chỉ đọc, không sửa file cũ).
        string? importNote = null;
        if (!File.Exists(AppPaths.Database))
        {
            var result = await LegacyImporter.ImportAsync(AppPaths.LegacyDatabase, AppPaths.Database, clock);
            importNote = result.Imported
                ? $"Đã nhập {result.Habits} thói quen, {result.DayLogs} ghi nhận từ phiên bản cũ."
                : $"Không nhập dữ liệu cũ ({result.SkippedReason}).";
            logger.Info(importNote);
        }

        var database = HabitGridV3.Open(AppPaths.Database);
        var bus = new DataChangeBus();
        var localizer = new Localizer();
        var settings = new SettingsStore(database);

        // Ngôn ngữ: theo cài đặt đã lưu, nếu chưa có thì theo ngôn ngữ giao diện của Windows.
        var savedLanguage = await settings.GetAsync(SettingKeys.Language);
        localizer.SetLanguage(savedLanguage ?? (CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "vi" ? "vi" : "en"));
        var entitlements = new FreeEntitlements();
        var dayStartHour = int.TryParse(await settings.GetAsync(SettingKeys.DayStartHour), NumberStyles.Integer, CultureInfo.InvariantCulture, out var hour)
            ? Math.Clamp(hour, 0, 5)
            : 0;

        var services = new ServiceCollection();
        services.AddSingleton<IAppLogger>(logger);
        services.AddSingleton(database);
        services.AddSingleton<IHlcClock>(clock);
        services.AddSingleton<IDataChangeBus>(bus);
        services.AddSingleton<ILocalizer>(localizer);
        services.AddSingleton<IUiDispatcher>(new WinUiDispatcher(dispatcherQueue));
        services.AddSingleton<IHabitRepository>(_ => new HabitRepository(database, clock, bus));
        services.AddSingleton<IDayLogRepository>(_ => new DayLogRepository(database, clock, bus));
        services.AddSingleton<IRoutineRepository>(_ => new RoutineRepository(database, clock, bus));
        services.AddSingleton<IProfileRepository>(_ => new ProfileRepository(database, clock, bus));
        services.AddSingleton<IAchievementRepository>(_ => new AchievementRepository(database, clock, bus));
        services.AddSingleton<ISettingsStore>(settings);
        services.AddSingleton(new ThemeService(settings));
        services.AddSingleton<IEntitlements>(entitlements);
        services.AddSingleton<IDayProvider>(_ => new DayProvider(TimeSpan.FromHours(dayStartHour)));
        services.AddSingleton<IStreakCalculator>(new StreakCalculator());
        services.AddSingleton<IStatsCalculator>(new StatsCalculator());
        services.AddSingleton<GamificationService>(sp => new GamificationService(
            sp.GetRequiredService<IHabitRepository>(),
            sp.GetRequiredService<IDayLogRepository>(),
            sp.GetRequiredService<IProfileRepository>(),
            sp.GetRequiredService<IAchievementRepository>(),
            sp.GetRequiredService<ISettingsStore>(),
            sp.GetRequiredService<IDayProvider>(),
            sp.GetRequiredService<IEntitlements>()));
        services.AddTransient<BuddyViewModel>(sp => new BuddyViewModel(
            sp.GetRequiredService<GamificationService>(),
            sp.GetRequiredService<IProfileRepository>(),
            sp.GetRequiredService<IAchievementRepository>()));
        services.AddSingleton<HabitCreationService>(sp => new HabitCreationService(
            sp.GetRequiredService<IHabitRepository>(),
            sp.GetRequiredService<IDayProvider>(),
            sp.GetRequiredService<IEntitlements>()));
        services.AddTransient<HabitEditorViewModel>(sp => new HabitEditorViewModel(
            sp.GetRequiredService<IHabitRepository>(),
            sp.GetRequiredService<IDayProvider>(),
            sp.GetRequiredService<IEntitlements>()));
        services.AddTransient<OnboardingViewModel>(sp => new OnboardingViewModel(
            sp.GetRequiredService<HabitCreationService>(),
            sp.GetRequiredService<IProfileRepository>(),
            sp.GetRequiredService<ISettingsStore>(),
            sp.GetRequiredService<IDayProvider>(),
            sp.GetRequiredService<ILocalizer>()));
        services.AddSingleton<ShellViewModel>();
        services.AddSingleton<TodayViewModel>(sp => new TodayViewModel(
            sp.GetRequiredService<IHabitRepository>(),
            sp.GetRequiredService<IDayLogRepository>(),
            sp.GetRequiredService<IDayProvider>(),
            sp.GetRequiredService<IStreakCalculator>(),
            null,
            sp.GetRequiredService<IDataChangeBus>(),
            sp.GetRequiredService<IUiDispatcher>(),
            sp.GetRequiredService<IAppLogger>(),
            sp.GetRequiredService<IProfileRepository>()));
        services.AddSingleton<HabitsViewModel>(sp => new HabitsViewModel(
            sp.GetRequiredService<IHabitRepository>(),
            sp.GetRequiredService<IDayLogRepository>(),
            sp.GetRequiredService<IDayProvider>(),
            sp.GetRequiredService<IStreakCalculator>(),
            sp.GetRequiredService<IStatsCalculator>(),
            sp.GetRequiredService<IUiDispatcher>(),
            sp.GetRequiredService<IAppLogger>()));
        services.AddTransient<HabitDetailViewModel>(sp => new HabitDetailViewModel(
            sp.GetRequiredService<IHabitRepository>(),
            sp.GetRequiredService<IDayLogRepository>(),
            sp.GetRequiredService<IDayProvider>(),
            sp.GetRequiredService<IStreakCalculator>(),
            sp.GetRequiredService<IStatsCalculator>(),
            sp.GetRequiredService<IUiDispatcher>(),
            sp.GetRequiredService<IAppLogger>()));

        return new AppHost(services.BuildServiceProvider(), importNote);
    }

    public T Get<T>() where T : notnull => Services.GetRequiredService<T>();

    public void Dispose() => Services.Dispose();

    // ------------------------------------------------------------------

    private static string ReadOrCreateDeviceId()
    {
        // Thiết bị chưa có CSDL v3 thì tạo id mới; importer sẽ ghi lại id này vào sync_state.
        if (File.Exists(AppPaths.Database))
        {
            try
            {
                using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
                {
                    DataSource = AppPaths.Database,
                    Mode = SqliteOpenMode.ReadOnly,
                    Pooling = false
                }.ToString());
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT value FROM sync_state WHERE key = 'device_id';";
                if (command.ExecuteScalar() is string id && id.Length > 0)
                {
                    return id;
                }
            }
            catch (SqliteException)
            {
                // CSDL chưa có sync_state: dùng id mới bên dưới.
            }
        }

        var idFile = Path.Combine(AppPaths.DataDir, "device-id");
        if (File.Exists(idFile))
        {
            var existing = File.ReadAllText(idFile).Trim();
            if (existing.Length > 0)
            {
                return existing;
            }
        }

        var created = $"win-{Guid.NewGuid():N}"[..16];
        File.WriteAllText(idFile, created);
        return created;
    }
}
