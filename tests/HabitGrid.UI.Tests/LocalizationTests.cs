using System.Collections;
using System.Globalization;
using System.Resources;
using FluentAssertions;
using HabitGrid.Core.Settings;
using HabitGrid.UI.Localization;
using Xunit;

namespace HabitGrid.UI.Tests;

/// <summary>
/// Bộ kiểm thử đa ngôn ngữ: chuyển ngôn ngữ tức thì và kiểm tra parity giữa hai file tài nguyên.
/// </summary>
[Collection("Localization")]
public class LocalizationTests
{
    private static ResourceManager CreateResourceManager() =>
        new("HabitGrid.UI.Resources.Resources", typeof(LocalizationManager).Assembly);

    [Fact]
    public void Resources_ShouldBeEmbeddedAndResolvable()
    {
        var manager = CreateResourceManager();

        manager.GetString("habit.add", CultureInfo.InvariantCulture)
            .Should().NotBeNull("file Resources.resx phải được nhúng vào assembly");
    }

    [Fact]
    public void VietnameseResource_ShouldOverrideEnglishDefault()
    {
        var manager = CreateResourceManager();

        manager.GetString("habit.add", new CultureInfo("vi-VN")).Should().Be("Thêm thói quen");
        manager.GetString("habit.add", CultureInfo.InvariantCulture).Should().Be("Add habit");
    }

    [Fact]
    public void AllKeys_ShouldExistInBothLanguages()
    {
        var manager = CreateResourceManager();

        using var neutral = manager.GetResourceSet(CultureInfo.InvariantCulture, createIfNotExists: true, tryParents: false);
        using var vietnamese = manager.GetResourceSet(new CultureInfo("vi-VN"), createIfNotExists: true, tryParents: false);

        neutral.Should().NotBeNull();
        vietnamese.Should().NotBeNull("file Resources.vi-VN.resx phải được nhúng và tải được");

        var neutralKeys = neutral!.Cast<DictionaryEntry>().Select(e => e.Key).ToHashSet();
        var vietnameseKeys = vietnamese!.Cast<DictionaryEntry>().Select(e => e.Key).ToHashSet();

        vietnameseKeys.Should().BeEquivalentTo(neutralKeys,
            "mỗi khóa trong Resources.resx đều phải có bản dịch trong Resources.vi-VN.resx");
    }

    [Fact]
    public void SetLanguage_ShouldSwitchStringsImmediately()
    {
        var manager = LocalizationManager.Instance;

        try
        {
            manager.SetLanguage(AppLanguage.English);
            manager.Get("habit.add").Should().Be("Add habit");
            manager.Get("settings.title").Should().Be("Settings");

            manager.SetLanguage(AppLanguage.Vietnamese);
            manager.Get("habit.add").Should().Be("Thêm thói quen");
            manager.Get("settings.title").Should().Be("Cài đặt");
        }
        finally
        {
            manager.SetLanguage(AppLanguage.Vietnamese);
        }
    }

    [Fact]
    public void SetLanguage_ShouldRaiseLanguageChanged()
    {
        var manager = LocalizationManager.Instance;
        var raised = false;

        void Handler(object? sender, EventArgs e) => raised = true;

        manager.LanguageChanged += Handler;

        try
        {
            var current = manager.Culture.TwoLetterISOLanguageName == "vi" ? AppLanguage.English : AppLanguage.Vietnamese;
            manager.SetLanguage(current);

            raised.Should().BeTrue();
        }
        finally
        {
            manager.LanguageChanged -= Handler;
            manager.SetLanguage(AppLanguage.Vietnamese);
        }
    }

    [Fact]
    public void Get_WithUnknownKey_ShouldReturnKeyItself()
    {
        LocalizationManager.Instance.Get("khong-ton-tai-abc").Should().Be("khong-ton-tai-abc");
    }

    [Fact]
    public void Format_ShouldReplacePlaceholders()
    {
        var manager = LocalizationManager.Instance;

        try
        {
            manager.SetLanguage(AppLanguage.Vietnamese);

            var text = manager.Format("toast.deleted", "Đọc sách");

            text.Should().Contain("Đọc sách");
            text.Should().NotContain("{0}");
        }
        finally
        {
            manager.SetLanguage(AppLanguage.Vietnamese);
        }
    }
}
