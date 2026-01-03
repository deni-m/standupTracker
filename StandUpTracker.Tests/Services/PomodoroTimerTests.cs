using Xunit;
using StandUpTracker.Services;
using StandUpTracker.Config;
using System;

namespace StandUpTracker.Tests.Services
{
    public class PomodoroTimerTests
    {
        private readonly ServiceLogger _logger;

        public PomodoroTimerTests()
        {
            _logger = new ServiceLogger();
        }

        #region Initial State Tests

        [Fact]
        public void InitialState_ShouldNotBeRunning()
        {
            // Arrange & Act
            var sut = new PomodoroTimer(_logger);

            // Assert
            Assert.False(sut.IsRunning);
        }

        [Fact]
        public void InitialState_TimeRemaining_ShouldBeZero()
        {
            // Arrange & Act
            var sut = new PomodoroTimer(_logger);

            // Assert
            Assert.Equal(TimeSpan.Zero, sut.TimeRemaining);
        }

        [Fact]
        public void InitialState_DurationMinutes_ShouldBeDefault()
        {
            // Arrange & Act
            var sut = new PomodoroTimer(_logger);

            // Assert
            Assert.Equal(AppSettings.PomodoroMinutes, sut.DurationMinutes);
        }

        #endregion

        #region Start Tests

        [Fact]
        public void Start_SetsIsRunningToTrue()
        {
            // Arrange
            var sut = new PomodoroTimer(_logger);

            // Act
            sut.Start();

            // Assert
            Assert.True(sut.IsRunning);
        }

        [Fact]
        public void Start_SetsTimeRemainingToDuration()
        {
            // Arrange
            var sut = new PomodoroTimer(_logger);

            // Act
            sut.Start();

            // Assert
            Assert.Equal(TimeSpan.FromMinutes(AppSettings.PomodoroMinutes), sut.TimeRemaining);
        }

        [Fact]
        public void Start_FiresStartedEvent()
        {
            // Arrange
            var sut = new PomodoroTimer(_logger);
            bool eventFired = false;
            sut.Started += (s, e) => eventFired = true;

            // Act
            sut.Start();

            // Assert
            Assert.True(eventFired);
        }

        [Fact]
        public void Start_WhenAlreadyRunning_ResetsTimer()
        {
            // Arrange
            var sut = new PomodoroTimer(_logger);
            sut.Start();
            sut.ProcessTick(); // Reduce time by 1 second

            // Act
            sut.Start(); // Start again

            // Assert - should be full duration again
            Assert.Equal(TimeSpan.FromMinutes(AppSettings.PomodoroMinutes), sut.TimeRemaining);
        }

        #endregion

        #region Stop Tests

        [Fact]
        public void Stop_SetsIsRunningToFalse()
        {
            // Arrange
            var sut = new PomodoroTimer(_logger);
            sut.Start();

            // Act
            sut.Stop();

            // Assert
            Assert.False(sut.IsRunning);
        }

        [Fact]
        public void Stop_FiresStoppedEvent()
        {
            // Arrange
            var sut = new PomodoroTimer(_logger);
            sut.Start();
            bool eventFired = false;
            sut.Stopped += (s, e) => eventFired = true;

            // Act
            sut.Stop();

            // Assert
            Assert.True(eventFired);
        }

        [Fact]
        public void Stop_ResetsTimeRemainingToZero()
        {
            // Arrange
            var sut = new PomodoroTimer(_logger);
            sut.Start();

            // Act
            sut.Stop();

            // Assert
            Assert.Equal(TimeSpan.Zero, sut.TimeRemaining);
        }

        [Fact]
        public void Stop_WhenNotRunning_DoesNotFireEvent()
        {
            // Arrange
            var sut = new PomodoroTimer(_logger);
            bool eventFired = false;
            sut.Stopped += (s, e) => eventFired = true;

            // Act
            sut.Stop();

            // Assert
            Assert.False(eventFired);
        }

        #endregion

        #region Restart Tests

        [Fact]
        public void Restart_WhenRunning_ResetsTimeRemaining()
        {
            // Arrange
            var sut = new PomodoroTimer(_logger);
            sut.Start();
            sut.ProcessTick(); // Reduce time

            // Act
            sut.Restart();

            // Assert
            Assert.Equal(TimeSpan.FromMinutes(AppSettings.PomodoroMinutes), sut.TimeRemaining);
            Assert.True(sut.IsRunning);
        }

        [Fact]
        public void Restart_WhenNotRunning_StartsTimer()
        {
            // Arrange
            var sut = new PomodoroTimer(_logger);

            // Act
            sut.Restart();

            // Assert
            Assert.True(sut.IsRunning);
            Assert.Equal(TimeSpan.FromMinutes(AppSettings.PomodoroMinutes), sut.TimeRemaining);
        }

        #endregion

        #region ProcessTick Tests

        [Fact]
        public void ProcessTick_WhenRunning_DecreasesTimeRemaining()
        {
            // Arrange
            var sut = new PomodoroTimer(_logger);
            sut.Start();
            var initialTime = sut.TimeRemaining;

            // Act
            sut.ProcessTick();

            // Assert
            Assert.True(sut.TimeRemaining < initialTime);
        }

        [Fact]
        public void ProcessTick_WhenNotRunning_DoesNothing()
        {
            // Arrange
            var sut = new PomodoroTimer(_logger);
            var initialTime = sut.TimeRemaining;

            // Act
            sut.ProcessTick();

            // Assert
            Assert.Equal(initialTime, sut.TimeRemaining);
        }

        [Fact]
        public void ProcessTick_FiresTickEvent_WhenRunning()
        {
            // Arrange
            var sut = new PomodoroTimer(_logger);
            sut.Start();
            bool eventFired = false;
            sut.Tick += (s, e) => eventFired = true;

            // Act
            sut.ProcessTick();

            // Assert
            Assert.True(eventFired);
        }

        #endregion

        #region Warning Tests

        [Fact]
        public void ProcessTick_FiresWarningDue_WhenWarningTimeReached()
        {
            // Arrange
            var sut = new PomodoroTimer(_logger);
            sut.Start();
            bool warningFired = false;
            sut.WarningDue += (s, e) => warningFired = true;

            // Simulate time passing until warning threshold
            // Warning at 3 minutes remaining, tick every 5 seconds
            // So we need to tick until TimeRemaining <= 3 minutes
            int warningSeconds = AppSettings.PomodoroWarningMinutes * 60;
            int totalSeconds = AppSettings.PomodoroMinutes * 60;
            int ticksNeeded = (totalSeconds - warningSeconds) / AppSettings.TickSeconds;

            for (int i = 0; i < ticksNeeded + 1; i++)
            {
                sut.ProcessTick();
            }

            // Assert
            Assert.True(warningFired);
        }

        [Fact]
        public void ProcessTick_WarningDue_FiresOnlyOnce()
        {
            // Arrange
            var sut = new PomodoroTimer(_logger);
            sut.Start();
            int warningCount = 0;
            sut.WarningDue += (s, e) => warningCount++;

            // Tick many times through warning period
            int totalSeconds = AppSettings.PomodoroMinutes * 60;
            int ticksNeeded = totalSeconds / AppSettings.TickSeconds + 10;

            for (int i = 0; i < ticksNeeded; i++)
            {
                sut.ProcessTick();
            }

            // Assert - warning should fire only once
            Assert.Equal(1, warningCount);
        }

        #endregion

        #region Completion Tests

        [Fact]
        public void ProcessTick_FiresCompleted_WhenTimerReachesZero()
        {
            // Arrange
            var sut = new PomodoroTimer(_logger);
            sut.Start();
            bool completedFired = false;
            sut.Completed += (s, e) => completedFired = true;

            // Tick until completion
            int totalSeconds = AppSettings.PomodoroMinutes * 60;
            int ticksNeeded = totalSeconds / AppSettings.TickSeconds + 1;

            for (int i = 0; i < ticksNeeded; i++)
            {
                sut.ProcessTick();
            }

            // Assert
            Assert.True(completedFired);
        }

        [Fact]
        public void ProcessTick_WhenCompleted_StopsTimer()
        {
            // Arrange
            var sut = new PomodoroTimer(_logger);
            sut.Start();

            // Tick until completion
            int totalSeconds = AppSettings.PomodoroMinutes * 60;
            int ticksNeeded = totalSeconds / AppSettings.TickSeconds + 1;

            for (int i = 0; i < ticksNeeded; i++)
            {
                sut.ProcessTick();
            }

            // Assert
            Assert.False(sut.IsRunning);
        }

        #endregion

        #region GetTooltipText Tests

        [Fact]
        public void GetTooltipText_WhenNotRunning_ReturnsEmptyOrNull()
        {
            // Arrange
            var sut = new PomodoroTimer(_logger);

            // Act
            var result = sut.GetTooltipText();

            // Assert
            Assert.True(string.IsNullOrEmpty(result));
        }

        [Fact]
        public void GetTooltipText_WhenRunning_ReturnsTimeRemaining()
        {
            // Arrange
            var sut = new PomodoroTimer(_logger);
            sut.Start();

            // Act
            var result = sut.GetTooltipText();

            // Assert
            Assert.Contains("Pomodoro", result);
            Assert.Contains(":", result); // Contains time format
        }

        #endregion
    }
}
