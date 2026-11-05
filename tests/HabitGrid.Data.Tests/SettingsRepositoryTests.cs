using FluentAssertions;
using Xunit;

namespace HabitGrid.Data.Tests;

/// <summary>
/// Bộ kiểm thử bảng cài đặt.
/// </summary>
public class SettingsRepositoryTests : DatabaseTestBase
{
    [Fact]
    public async Task SetAsync_ThenGetAsync_ShouldReturnStoredValue()
    {
        await Settings.SetAsync("weekStart", "monday");

        (await Settings.GetAsync("weekStart")).Should().Be("monday");
    }

    [Fact]
    public async Task GetAsync_WithMissingKey_ShouldReturnNull()
    {
        (await Settings.GetAsync("khong-ton-tai")).Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_WithGenericType_ShouldDeserializeValue()
    {
        await Settings.SetAsync("dayStartHour", 3);

        var value = await Settings.GetAsync("dayStartHour", 0);

        value.Should().Be(3);
    }

    [Fact]
    public async Task GetAsync_WithMissingKey_ShouldReturnDefaultValue()
    {
        var value = await Settings.GetAsync<int>("khong-ton-tai", 42);

        value.Should().Be(42);
    }

    [Fact]
    public async Task SetAsync_CalledTwice_ShouldOverwriteValue()
    {
        await Settings.SetAsync("weekStart", "monday");
        await Settings.SetAsync("weekStart", "sunday");

        (await Settings.GetAsync("weekStart")).Should().Be("sunday");
    }

    [Fact]
    public async Task GetAsync_WithCorruptedValue_ShouldReturnDefaultValue()
    {
        await Settings.SetAsync("dayStartHour", "khong-phai-so");

        var value = await Settings.GetAsync("dayStartHour", 7);

        value.Should().Be(7, "dữ liệu cài đặt hỏng không được làm sập ứng dụng");
    }
}
