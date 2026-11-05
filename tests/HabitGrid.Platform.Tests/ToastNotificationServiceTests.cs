using FluentAssertions;
using HabitGrid.Platform.System;
using Xunit;

namespace HabitGrid.Platform.Tests;

public class ToastNotificationServiceTests
{
    [Fact]
    public void ParseAndRaise_ValidCheckinArguments_ShouldRaiseActionInvoked()
    {
        var service = new ToastNotificationService();
        var targetHabitId = Guid.NewGuid();
        ToastActionArgs? received = null;

        service.ActionInvoked += (_, args) => received = args;
        service.ParseAndRaise($"action=checkin&habitId={targetHabitId}");

        received.Should().NotBeNull();
        received!.Action.Should().Be("checkin");
        received.HabitId.Should().Be(targetHabitId);
    }

    [Fact]
    public void ParseAndRaise_SnoozeArguments_ShouldRaiseActionInvoked()
    {
        var service = new ToastNotificationService();
        var targetHabitId = Guid.NewGuid();
        ToastActionArgs? received = null;

        service.ActionInvoked += (_, args) => received = args;
        service.ParseAndRaise($"action=snooze&habitId={targetHabitId}");

        received.Should().NotBeNull();
        received!.Action.Should().Be("snooze");
        received.HabitId.Should().Be(targetHabitId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid_payload")]
    [InlineData("action=checkin")]
    [InlineData("habitId=not-a-guid")]
    public void ParseAndRaise_InvalidArguments_ShouldNotRaiseEvent(string invalidPayload)
    {
        var service = new ToastNotificationService();
        var triggered = false;

        service.ActionInvoked += (_, _) => triggered = true;
        service.ParseAndRaise(invalidPayload);

        triggered.Should().BeFalse();
    }
}
