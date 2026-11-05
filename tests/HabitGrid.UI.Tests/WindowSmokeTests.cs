using FluentAssertions;
using HabitGrid.Core.Services;
using HabitGrid.Core.Settings;
using HabitGrid.Core.Tests;
using HabitGrid.Data;
using HabitGrid.Data.Repositories;
using HabitGrid.Platform.Licensing;
using HabitGrid.UI.Services;
using HabitGrid.UI.Theme;
using HabitGrid.UI.ViewModels;
using HabitGrid.UI.Views;
using Xunit;

namespace HabitGrid.UI.Tests;

/// <summary>
/// Smoke test: các cửa sổ/trang chính phải dựng được XAML thật (bắt lỗi BAML, binding, null lúc khởi tạo).
/// </summary>
public class WindowSmokeTests
{
    [Fact]
    public void MainWindow_WithSettingsPage_ShouldConstructWithoutException()
    {
        RunSta(() =>
        {
            var database = HabitGridDatabase.CreateInMemory();
            database.InitializeAsync().GetAwaiter().GetResult();

            try
            {
                var habits = new HabitRepository(database);
                var settings = new AppSettings(new InMemorySettingsRepository());
                var license = new FreeLicenseService();

                var main = new MainViewModel(
                    habits,
                    new EntryRepository(database),
                    new WidgetStateRepository(database),
                    license,
                    new StreakCalculator(new ScheduleCalculator()),
                    new DayProvider(),
                    new FakeDialogService(),
                    new SafeDeletionManager(habits, new FakeDeletionTimer()));

                var settingsVm = new SettingsViewModel(settings, new ThemeService(settings), license, new NoopSettingsHost());

                var window = new MainWindow(main, settingsVm);
                window.ShowSettingsPage();
                window.Should().NotBeNull();
            }
            finally
            {
                database.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
        });
    }

    private sealed class NoopSettingsHost : ISettingsHost
    {
        public Task ResetWidgetPositionsAsync() => Task.CompletedTask;

        public void OpenDataFolder()
        {
        }

        public Task ExportJsonAsync() => Task.CompletedTask;

        public Task ExportCsvAsync() => Task.CompletedTask;

        public Task ImportJsonAsync() => Task.CompletedTask;

        public void CopyVersionInfo(string versionText)
        {
        }

        public Task ShowPaywallAsync() => Task.CompletedTask;
    }

    private static void RunSta(Action action)
    {
        Exception? captured = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { captured = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (captured is not null)
        {
            throw new InvalidOperationException("Smoke test WPF thất bại trên luồng STA.", captured);
        }
    }
}
