using StandUpTracker.Services;

namespace StandUpTracker.Tests.Services;

/// <summary>
/// Tests for ReminderScheduler - User experience timing and DND muting logic
/// </summary>
public class ReminderSchedulerTests
{
    private readonly ServiceLogger _logger;
    private readonly DoNotDisturbService _dndService;

    public ReminderSchedulerTests()
    {
        _logger = new ServiceLogger();
        _dndService = new DoNotDisturbService();
    }

    [Fact]
    public void CheckReminder_Before55Min_ReturnsFalse()
    {
        // Arrange
        var sut = new ReminderScheduler(_logger, _dndService);
        var activeStart = DateTime.Now.AddMinutes(-54); // 54 minutes
        var nextReminderAt = activeStart.AddMinutes(55);

        // Act
        var result = sut.CheckReminder(activeStart, nextReminderAt);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void CheckReminder_After55Min_ReturnsTrue()
    {
        // Arrange
        var sut = new ReminderScheduler(_logger, _dndService);
        var activeStart = DateTime.Now.AddMinutes(-56); // 56 minutes
        var nextReminderAt = activeStart.AddMinutes(55);

        // Act
        var result = sut.CheckReminder(activeStart, nextReminderAt);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void CheckReminder_FiresReminderDueEvent()
    {
        // Arrange
        var sut = new ReminderScheduler(_logger, _dndService);
        var activeStart = DateTime.Now.AddMinutes(-56);
        var nextReminderAt = activeStart.AddMinutes(55);
        var eventFired = false;
        bool? wasMuted = null;
        sut.ReminderDue += (s, e) =>
        {
            eventFired = true;
            wasMuted = e.IsMuted;
        };

        // Act
        sut.CheckReminder(activeStart, nextReminderAt);

        // Assert
        Assert.True(eventFired);
        Assert.NotNull(wasMuted);
    }

    [Fact]
    public void CheckGraceWarning_At570Seconds_ShowsWarning()
    {
        // Arrange
        var sut = new ReminderScheduler(_logger, _dndService);
        var eventFired = false;
        sut.GraceWarningDue += (s, e) => eventFired = true;

        // Act - Grace warning at 570s (30s before 600s break)
        sut.CheckGraceWarning(idleSeconds: 570, isActive: true);

        // Assert
        Assert.True(eventFired, "Grace warning should fire at 570 seconds");
    }

    [Fact]
    public void CheckGraceWarning_Before570Seconds_NoWarning()
    {
        // Arrange
        var sut = new ReminderScheduler(_logger, _dndService);
        var eventFired = false;
        sut.GraceWarningDue += (s, e) => eventFired = true;

        // Act
        sut.CheckGraceWarning(idleSeconds: 569, isActive: true);

        // Assert
        Assert.False(eventFired, "Grace warning should NOT fire before 570 seconds");
    }

    [Fact]
    public void CheckGraceWarning_After600Seconds_NoWarning()
    {
        // Arrange
        var sut = new ReminderScheduler(_logger, _dndService);
        var eventFired = false;
        sut.GraceWarningDue += (s, e) => eventFired = true;

        // Act
        sut.CheckGraceWarning(idleSeconds: 601, isActive: true);

        // Assert
        Assert.False(eventFired, "Grace warning should NOT fire after break starts (600s)");
    }

    [Fact]
    public void CheckGraceWarning_AlreadyShown_DoesNotRepeat()
    {
        // Arrange
        var sut = new ReminderScheduler(_logger, _dndService);
        var eventCount = 0;
        sut.GraceWarningDue += (s, e) => eventCount++;

        // Act
        sut.CheckGraceWarning(idleSeconds: 570, isActive: true); // First time
        sut.CheckGraceWarning(idleSeconds: 570, isActive: true); // Second time

        // Assert
        Assert.Equal(1, eventCount);
    }

    [Fact]
    public void ResetGraceBalloon_UserActivity_ResetsFlag()
    {
        // Arrange
        var sut = new ReminderScheduler(_logger, _dndService);
        sut.CheckGraceWarning(idleSeconds: 570, isActive: true); // Show grace warning
        var eventCount = 0;
        sut.GraceWarningDue += (s, e) => eventCount++;

        // Act
        sut.ResetGraceBalloon(); // User became active again
        sut.CheckGraceWarning(idleSeconds: 570, isActive: true); // Check again

        // Assert
        Assert.Equal(1, eventCount); // Grace warning should fire again after reset
    }

    [Fact]
    public void CheckMuteStatus_ReturnsTuple()
    {
        // Arrange
        var sut = new ReminderScheduler(_logger, _dndService);

        // Act
        var result = sut.CheckMuteStatus();

        // Assert - Verify tuple structure
        Assert.IsType<bool>(result.IsMuted);
        Assert.IsType<string>(result.Reason);
    }
}
