using FluentAssertions;
using HabitGrid.Core.Settings;
using HabitGrid.Core.Tests;
using HabitGrid.Platform.Licensing;
using HabitGrid.UI.Localization;
using HabitGrid.UI.Services;
using HabitGrid.UI.Theme;
using HabitGrid.UI.ViewModels;
using Xunit;

namespace HabitGrid.UI.Tests;

/// <summary>
/// Bộ kiểm thử ViewModel cài đặt: tự lưu ngay, đổi ngôn ngữ tức thì, gọi đúng hành động.
/// </summary>
public class SettingsViewModelTests
{
    private readonly InMemorySettingsRepository _repository = new();
    private readonly AppSettings _settings;
    private readonly ThemeService _themeService;
    private readonly FakeSettingsHost _host = new();

    public SettingsViewModelTests()
    {
        _settings = new AppSettings(_repository);
        _themeService = new ThemeService(_settings);
    }

    [Fact]
    public void SettingProperty_ShouldWriteThroughToSettings()
    {
        var viewModel = CreateViewModel();

        viewModel.Theme = AppThemeMode.Dark;
        viewModel.DayStartHour = 4;
        viewModel.CornerRadius = 8;
        viewModel.BackgroundOpacity = 40;
        viewModel.WeekStart = DayOfWeek.Sunday;
        viewModel.PinningMode = 2;
        viewModel.DefaultWidgetSize = 0;
        viewModel.EditMode = true;
        viewModel.RunAtStartup = true;

        _settings.Theme.Should().Be(AppThemeMode.Dark);
        _settings.DayStartHour.Should().Be(4);
        _settings.CornerRadius.Should().Be(8);
        _settings.BackgroundOpacity.Should().Be(40);
        _settings.WeekStart.Should().Be(DayOfWeek.Sunday);
        _settings.PinningMode.Should().Be(2);
        _settings.DefaultWidgetSize.Should().Be(0);
        _settings.EditMode.Should().BeTrue();
        _settings.RunAtStartup.Should().BeTrue();
    }

    [Fact]
    public async Task SettingProperty_ShouldBePersistedToRepository()
    {
        var viewModel = CreateViewModel();

        viewModel.Theme = AppThemeMode.Dark;

        await _settings.FlushAsync();

        _repository.Values.Should().ContainKey(SettingsKeys.Theme);
    }

    [Fact]
    public void ChangingLanguage_ShouldSwitchLocalizationImmediately()
    {
        var viewModel = CreateViewModel();

        try
        {
            viewModel.Language = AppLanguage.English;
            LocalizationManager.Instance.Get("habit.add").Should().Be("Add habit");

            viewModel.Language = AppLanguage.Vietnamese;
            LocalizationManager.Instance.Get("habit.add").Should().Be("Thêm thói quen");
        }
        finally
        {
            LocalizationManager.Instance.SetLanguage(AppLanguage.Vietnamese);
        }
    }

    [Fact]
    public void ViewModel_ShouldReflectCurrentSettings()
    {
        _settings.Theme = AppThemeMode.Light;
        _settings.DayStartHour = 2;

        var viewModel = CreateViewModel();

        viewModel.Theme.Should().Be(AppThemeMode.Light);
        viewModel.DayStartHour.Should().Be(2);
    }

    [Fact]
    public async Task Commands_ShouldCallHost()
    {
        var viewModel = CreateViewModel();

        await viewModel.ResetPositionsCommand.ExecuteAsync(null);
        await viewModel.OpenDataFolderCommand.ExecuteAsync(null);
        await viewModel.ExportJsonCommand.ExecuteAsync(null);
        await viewModel.ExportCsvCommand.ExecuteAsync(null);
        await viewModel.ImportJsonCommand.ExecuteAsync(null);
        await viewModel.CopyVersionCommand.ExecuteAsync(null);
        await viewModel.UpgradeCommand.ExecuteAsync(null);

        _host.ResetPositionsCalled.Should().BeTrue();
        _host.OpenDataFolderCalled.Should().BeTrue();
        _host.ExportJsonCalled.Should().BeTrue();
        _host.ExportCsvCalled.Should().BeTrue();
        _host.ImportJsonCalled.Should().BeTrue();
        _host.CopyVersionCalled.Should().BeTrue();
        _host.PaywallCalled.Should().BeTrue();
    }

    [Fact]
    public void VersionText_ShouldContainVersionAndArchitecture()
    {
        var viewModel = CreateViewModel();

        viewModel.VersionText.Should().Contain("(");
        viewModel.VersionText.Should().MatchRegex(@"\d+\.\d+\.\d+");
    }

    [Fact]
    public void LicenseText_ShouldDifferBetweenFreeAndPro()
    {
        var free = CreateViewModel();
        free.LicenseText.Should().Contain("Free");

        _settings.Theme = AppThemeMode.System;
        var pro = new SettingsViewModel(_settings, _themeService, new AlwaysProLicense(), _host);
        pro.LicenseText.Should().Contain("Pro");
    }

    private SettingsViewModel CreateViewModel() =>
        new(_settings, _themeService, new FreeLicenseService(), _host);

    /// <summary>Giả lập các hành động của trang cài đặt.</summary>
    private sealed class FakeSettingsHost : ISettingsHost
    {
        public bool ResetPositionsCalled { get; private set; }

        public bool OpenDataFolderCalled { get; private set; }

        public bool ExportJsonCalled { get; private set; }

        public bool ExportCsvCalled { get; private set; }

        public bool ImportJsonCalled { get; private set; }

        public bool CopyVersionCalled { get; private set; }

        public bool PaywallCalled { get; private set; }

        public Task ResetWidgetPositionsAsync()
        {
            ResetPositionsCalled = true;
            return Task.CompletedTask;
        }

        public void OpenDataFolder() => OpenDataFolderCalled = true;

        public Task ExportJsonAsync()
        {
            ExportJsonCalled = true;
            return Task.CompletedTask;
        }

        public Task ExportCsvAsync()
        {
            ExportCsvCalled = true;
            return Task.CompletedTask;
        }

        public Task ImportJsonAsync()
        {
            ImportJsonCalled = true;
            return Task.CompletedTask;
        }

        public void CopyVersionInfo(string versionText) => CopyVersionCalled = true;

        public Task ShowPaywallAsync()
        {
            PaywallCalled = true;
            return Task.CompletedTask;
        }
    }

    private sealed class AlwaysProLicense : FreeLicenseService
    {
        public AlwaysProLicense() : base(initialPro: true)
        {
        }
    }
}
