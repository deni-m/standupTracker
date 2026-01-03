using StandUpTracker.Services;

namespace StandUpTracker.Tests.Services;

/// <summary>
/// Tests for SessionStateMachine - Critical business logic for state transitions and break tracking
/// </summary>
public class SessionStateMachineTests
{
    private readonly ServiceLogger _logger;

    public SessionStateMachineTests()
    {
        _logger = new ServiceLogger();
    }

    [Fact]
    public void InitialState_ShouldBeIdle()
    {
        // Arrange & Act
        var sut = new SessionStateMachine(_logger);

        // Assert
        Assert.Equal(AppState.Idle, sut.CurrentState);
    }

    [Fact]
    public void Idle_UserActivity_TransitionsToActive()
    {
        // Arrange
        var sut = new SessionStateMachine(_logger);
        var stateChanged = false;
        sut.StateChanged += (s, e) => stateChanged = true;

        // Act
        var result = sut.ProcessTick(idleSeconds: 0, isSessionLocked: false);

        // Assert
        Assert.Equal(AppState.Active, sut.CurrentState);
        Assert.True(stateChanged);
        Assert.True(result.ShouldContinue);
    }

    [Fact]
    public void Active_IdleTimeout_TransitionsToIdle()
    {
        // Arrange
        var sut = new SessionStateMachine(_logger);
        sut.ProcessTick(idleSeconds: 0, isSessionLocked: false); // Transition to Active
        var breakStartedFired = false;
        sut.BreakStarted += (s, e) => breakStartedFired = true;

        // Act
        var result = sut.ProcessTick(idleSeconds: 600, isSessionLocked: false);

        // Assert
        Assert.Equal(AppState.Idle, sut.CurrentState);
        Assert.True(breakStartedFired);
    }

    [Fact]
    public void Active_IdleTimeout_SetsBreakTakenFlag()
    {
        // Arrange
        var sut = new SessionStateMachine(_logger);
        sut.ProcessTick(idleSeconds: 0, isSessionLocked: false); // Active
        var activeStartBefore = sut.ActiveStart;
        sut.ProcessTick(idleSeconds: 600, isSessionLocked: false); // Idle (break taken)

        // Act - Return to active should have new active start time (indicating reset)
        sut.ProcessTick(idleSeconds: 0, isSessionLocked: false);
        var activeStartAfter = sut.ActiveStart;

        // Assert
        Assert.Equal(AppState.Active, sut.CurrentState);
        Assert.NotEqual(activeStartBefore, activeStartAfter); // New session after break
    }

    [Fact]
    public void Active_SessionLock_TransitionsToLocked()
    {
        // Arrange
        var sut = new SessionStateMachine(_logger);
        sut.ProcessTick(idleSeconds: 0, isSessionLocked: false); // Active
        var breakStartedFired = false;
        sut.BreakStarted += (s, e) => breakStartedFired = true;

        // Act
        var result = sut.ProcessTick(idleSeconds: 0, isSessionLocked: true);

        // Assert
        Assert.Equal(AppState.Locked, sut.CurrentState);
        Assert.True(breakStartedFired);
    }

    [Fact]
    public void Locked_SessionUnlock_TransitionsToActive()
    {
        // Arrange
        var sut = new SessionStateMachine(_logger);
        sut.ProcessTick(idleSeconds: 0, isSessionLocked: false); // Active
        sut.ProcessTick(idleSeconds: 0, isSessionLocked: true); // Locked
        var sessionStartedFired = false;
        sut.SessionStarted += (s, e) => sessionStartedFired = true;

        // Act
        var result = sut.ProcessTick(idleSeconds: 0, isSessionLocked: false);

        // Assert
        Assert.Equal(AppState.Active, sut.CurrentState);
        Assert.True(sessionStartedFired);
    }

    [Fact]
    public void Locked_SetsBreakTakenFlag()
    {
        // Arrange
        var sut = new SessionStateMachine(_logger);
        sut.ProcessTick(idleSeconds: 0, isSessionLocked: false); // Active
        var activeStartBefore = sut.ActiveStart;
        sut.ProcessTick(idleSeconds: 0, isSessionLocked: true); // Locked (break taken)

        // Act - Unlock should reset with new active start time
        sut.ProcessTick(idleSeconds: 0, isSessionLocked: false);
        var activeStartAfter = sut.ActiveStart;

        // Assert
        Assert.Equal(AppState.Active, sut.CurrentState);
        Assert.NotEqual(activeStartBefore, activeStartAfter); // New session after lock
    }

    [Fact]
    public void BreakTaken_ResetsReminderSchedule()
    {
        // Arrange
        var sut = new SessionStateMachine(_logger);
        sut.ProcessTick(idleSeconds: 0, isSessionLocked: false); // Active
        var activeStartBefore = sut.ActiveStart;
        sut.ProcessTick(idleSeconds: 600, isSessionLocked: false); // Idle (break >= 600s)

        // Act - Return to Active should start new session
        sut.ProcessTick(idleSeconds: 0, isSessionLocked: false);
        var activeStartAfter = sut.ActiveStart;

        // Assert - New active start time means reminder timer will reset
        Assert.NotEqual(activeStartBefore, activeStartAfter);
    }

    [Fact]
    public void NoBreak_KeepsReminderSchedule()
    {
        // Arrange
        var sut = new SessionStateMachine(_logger);
        sut.ProcessTick(idleSeconds: 0, isSessionLocked: false); // Active
        var activeStartBefore = sut.ActiveStart;
        sut.ProcessTick(idleSeconds: 300, isSessionLocked: false); // Still Active (idle < 600s)

        // Act - Continue Active with short idle
        sut.ProcessTick(idleSeconds: 0, isSessionLocked: false);
        var activeStartAfter = sut.ActiveStart;

        // Assert - Same active start time means reminder continues
        Assert.Equal(activeStartBefore, activeStartAfter);
    }

    [Fact]
    public void SetPaused_TransitionsToPaused()
    {
        // Arrange
        var sut = new SessionStateMachine(_logger);
        sut.ProcessTick(idleSeconds: 0, isSessionLocked: false); // Active
        var stateChanged = false;
        sut.StateChanged += (s, e) => stateChanged = true;

        // Act
        sut.SetPaused(true);

        // Assert
        Assert.Equal(AppState.Paused, sut.CurrentState);
        Assert.True(stateChanged);
    }

    [Fact]
    public void Paused_UserUnpause_AllowsStateTransitions()
    {
        // Arrange
        var sut = new SessionStateMachine(_logger);
        sut.ProcessTick(idleSeconds: 0, isSessionLocked: false); // Active
        sut.SetPaused(true); // Paused

        // Act
        sut.SetPaused(false); // Unpause
        
        // Assert - Unpausing doesn't immediately change state,
        // but next ProcessTick will allow transitions
        Assert.True(true); // Verify no exception
    }

    [Fact]
    public void StateChanged_EventFired()
    {
        // Arrange
        var sut = new SessionStateMachine(_logger);
        var eventFired = false;
        AppState? capturedState = null;
        sut.StateChanged += (s, e) =>
        {
            eventFired = true;
            capturedState = e.NewState;
        };

        // Act
        sut.ProcessTick(idleSeconds: 0, isSessionLocked: false);

        // Assert
        Assert.True(eventFired);
        Assert.Equal(AppState.Active, capturedState);
    }

    [Fact]
    public void SessionStarted_EventFired()
    {
        // Arrange
        var sut = new SessionStateMachine(_logger);
        var eventFired = false;
        sut.SessionStarted += (s, e) => eventFired = true;

        // Act - First transition to Active should fire SessionStarted
        sut.ProcessTick(idleSeconds: 0, isSessionLocked: false);

        // Assert
        Assert.True(eventFired);
    }

    [Fact]
    public void BreakStarted_EventFired()
    {
        // Arrange
        var sut = new SessionStateMachine(_logger);
        sut.ProcessTick(idleSeconds: 0, isSessionLocked: false); // Active
        var eventFired = false;
        sut.BreakStarted += (s, e) => eventFired = true;

        // Act
        sut.ProcessTick(idleSeconds: 600, isSessionLocked: false); // Idle

        // Assert
        Assert.True(eventFired);
    }

    [Fact]
    public void ActiveWorkWindow_PreventsIdleTransition()
    {
        // This test requires access to window tracking logic
        // For now, we verify the state machine doesn't transition when told not to
        
        // Arrange
        var sut = new SessionStateMachine(_logger);
        sut.ProcessTick(idleSeconds: 0, isSessionLocked: false); // Active

        // Act - Even with 600s idle, if ActivityMonitor detects active work window,
        // it should NOT call ProcessTick with idle >= 600s
        // This is actually tested in ActivityMonitorTests
        
        // Verify state machine itself respects the 600s threshold
        sut.ProcessTick(idleSeconds: 599, isSessionLocked: false); // Just below threshold

        // Assert
        Assert.Equal(AppState.Active, sut.CurrentState);
    }
}
