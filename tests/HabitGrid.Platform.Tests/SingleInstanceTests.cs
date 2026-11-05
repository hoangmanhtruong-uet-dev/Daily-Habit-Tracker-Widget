using FluentAssertions;
using HabitGrid.Platform.System;
using Xunit;

namespace HabitGrid.Platform.Tests;

public class SingleInstanceTests
{
    [Fact]
    public void TryAcquire_FirstInstance_ShouldSucceed()
    {
        var testId = Guid.NewGuid().ToString("N");
        using var service = new SingleInstanceService(testId);

        var acquired = service.TryAcquire();

        acquired.Should().BeTrue();
    }

    [Fact]
    public void TryAcquire_SecondInstance_ShouldFail()
    {
        var testId = Guid.NewGuid().ToString("N");
        using var first = new SingleInstanceService(testId);
        using var second = new SingleInstanceService(testId);

        var firstAcquired = first.TryAcquire();
        var secondAcquired = second.TryAcquire();

        firstAcquired.Should().BeTrue();
        secondAcquired.Should().BeFalse();
    }

    [Fact]
    public void Dispose_ShouldReleaseMutex_AllowingSubsequentInstance()
    {
        var testId = Guid.NewGuid().ToString("N");
        var first = new SingleInstanceService(testId);
        first.TryAcquire().Should().BeTrue();
        first.Dispose();

        using var second = new SingleInstanceService(testId);
        second.TryAcquire().Should().BeTrue();
    }
}
