using System.IO;
using FluentAssertions;
using HabitGrid.Platform.Licensing;
using Xunit;

namespace HabitGrid.Platform.Tests;

public class StoreLicenseTests : IDisposable
{
    private readonly string _tempCachePath;

    public StoreLicenseTests()
    {
        _tempCachePath = Path.Combine(Path.GetTempPath(), $"HabitGrid_LicTest_{Guid.NewGuid():N}.dat");
    }

    [Fact]
    public void Constructor_WhenNoCache_ShouldDefaultToFree()
    {
        var service = new StoreLicenseService(_tempCachePath);

        service.IsPro.Should().BeFalse();
        service.HabitLimit.Should().Be(StoreLicenseService.FreeHabitLimit);
    }

    [Fact]
    public async Task PurchaseProAsync_UnpackagedDev_ShouldUnlockProAndSaveDpapiCache()
    {
        var service = new StoreLicenseService(_tempCachePath);
        var changedFired = false;
        service.LicenseChanged += (_, isPro) => changedFired = isPro;

        var result = await service.PurchaseProAsync(IntPtr.Zero);

        result.Success.Should().BeTrue();
        service.IsPro.Should().BeTrue();
        service.HabitLimit.Should().Be(int.MaxValue);
        changedFired.Should().BeTrue();

        File.Exists(_tempCachePath).Should().BeTrue();

        // Khởi tạo instance mới đọc lại cache vừa mã hóa DPAPI
        var reloaded = new StoreLicenseService(_tempCachePath);
        reloaded.IsPro.Should().BeTrue();
        reloaded.HabitLimit.Should().Be(int.MaxValue);
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_tempCachePath))
            {
                File.Delete(_tempCachePath);
            }
        }
        catch
        {
            // ignored
        }
    }
}
