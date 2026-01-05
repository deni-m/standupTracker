using System;
using System.Timers;
using StandUpTracker.Config;
using StandUpTracker.Models;

namespace StandUpTracker.Services
{
    /// <summary>
    /// Event arguments for tooltip updates.
    /// </summary>
    public class TooltipUpdateEventArgs : EventArgs
    {
        public string TooltipText { get; }

        public TooltipUpdateEventArgs(string tooltipText)
        {
            TooltipText = tooltipText;
        }
    }

    /// <summary>
    /// Event arguments for window sample changes.
    /// </summary>
    public class WindowSampleEventArgs : EventArgs
    {
        public ActiveSample? OldSample { get; }
        public ActiveSample? NewSample { get; }

        public WindowSampleEventArgs(ActiveSample? oldSample, ActiveSample? newSample)
        {
            OldSample = oldSample;
            NewSample = newSample;
        }
    }

    /// <summary>
    /// Orchestrates the activity monitoring: timer ticks, state machine, reminders, and window tracking.
    /// Acts as a facade/coordinator between all services and the UI.
    /// </summary>
    public class ActivityMonitor : IDisposable
    {
        private readonly IdleTracker _idleTracker;
        private readonly WindowTracker _windowTracker;
        private readonly ActivityLogger _activityLogger;
        private readonly ServiceLogger _serviceLogger;
        private readonly SessionStateMachine _stateMachine;
        private readonly ReminderScheduler _reminderScheduler;

        private readonly System.Timers.Timer _timer;
        private ActiveSample? _currentSample = null;
        private int _lastIdleSeconds = 0;
        private bool _disposed = false;

        #region Events

        /// <summary>
        /// Fires when the application state changes.
        /// </summary>
        public event EventHandler<StateChangedEventArgs>? StateChanged;

        /// <summary>
        /// Fires when a stand-up reminder is due.
        /// </summary>
        public event EventHandler<ReminderEventArgs>? ReminderDue;

        /// <summary>
        /// Fires when a grace period warning is due.
        /// </summary>
        public event EventHandler<GraceWarningEventArgs>? GraceWarningDue;

        /// <summary>
        /// Fires when the tray tooltip should be updated.
        /// </summary>
        public event EventHandler<TooltipUpdateEventArgs>? TooltipUpdateRequired;

        /// <summary>
        /// Fires when pause state changes (for UI notification).
        /// </summary>
        public event EventHandler<bool>? PauseStateChanged;

        #endregion

        #region Properties

        public AppState CurrentState => _stateMachine.CurrentState;
        public bool IsPaused => _stateMachine.IsPaused;
        public DateTime ActiveStart => _stateMachine.ActiveStart;
        public ActivityLogger ActivityLogger => _activityLogger;

        #endregion

        public ActivityMonitor(
            IdleTracker idleTracker,
            WindowTracker windowTracker,
            DoNotDisturbService dndService,
            ActivityLogger activityLogger)
        {
            _serviceLogger = new ServiceLogger();
            _serviceLogger.Info("STARTUP", "=== StandUp Tracker Starting ===");

            _idleTracker = idleTracker ?? throw new ArgumentNullException(nameof(idleTracker));
            _windowTracker = windowTracker ?? throw new ArgumentNullException(nameof(windowTracker));
            _activityLogger = activityLogger ?? throw new ArgumentNullException(nameof(activityLogger));

            ValidateSettings();

            // Initialize state machine
            _stateMachine = new SessionStateMachine(_serviceLogger);
            _stateMachine.StateChanged += OnStateMachineStateChanged;
            _stateMachine.SessionStarted += OnSessionStarted;
            _stateMachine.BreakStarted += OnBreakStarted;

            // Initialize reminder scheduler
            _reminderScheduler = new ReminderScheduler(_serviceLogger, dndService);
            _reminderScheduler.ReminderDue += OnReminderDue;
            _reminderScheduler.GraceWarningDue += OnGraceWarningDue;

            // Initialize timer
            _timer = new System.Timers.Timer(AppSettings.TickSeconds * 1000)
            {
                AutoReset = true
            };
            _timer.Elapsed += OnTimerTick;
            _serviceLogger.Info("TIMER", "Timer created with interval {0}ms", AppSettings.TickSeconds * 1000);

            // Capture initial window
            _currentSample = _windowTracker.CaptureActiveSample();
            if (_currentSample != null)
            {
                _serviceLogger.Debug("WINDOW", "Initial window: {0} - {1}", _currentSample.Process, _currentSample.Title);
            }

            _serviceLogger.Info("STARTUP", "=== StandUp Tracker Initialized Successfully ===");
        }

        #region Public Methods

        /// <summary>
        /// Starts the activity monitoring.
        /// </summary>
        public void Start()
        {
            _timer.Start();
            UpdateTooltip();
            _serviceLogger.Info("TIMER", "Activity monitoring started");
        }

        /// <summary>
        /// Stops the activity monitoring.
        /// </summary>
        public void Stop()
        {
            _timer.Stop();
            _serviceLogger.Info("TIMER", "Activity monitoring stopped");
        }

        /// <summary>
        /// Sets the paused state.
        /// </summary>
        public void SetPaused(bool paused)
        {
            _stateMachine.SetPaused(paused);
            
            if (paused)
            {
                CloseCurrentSample();
            }

            PauseStateChanged?.Invoke(this, paused);
            UpdateTooltip();
        }

        /// <summary>
        /// Gets the current tooltip text.
        /// </summary>
        public string GetTooltipText()
        {
            return _stateMachine.CurrentState switch
            {
                AppState.Paused => "StandUp: Paused",
                AppState.Idle or AppState.Locked => "StandUp: —",
                AppState.Active => $"StandUp: {(DateTime.Now - _stateMachine.ActiveStart):hh\\:mm\\:ss}",
                _ => "StandUp: —"
            };
        }

        /// <summary>
        /// Shuts down the monitor and cleans up resources.
        /// </summary>
        public void Shutdown()
        {
            _serviceLogger.Info("SHUTDOWN", "=== StandUp Tracker Shutting Down ===");

            try
            {
                _serviceLogger.Info("SHUTDOWN", "Stopping timer...");
                _timer?.Stop();

                _serviceLogger.Info("SHUTDOWN", "Closing current sample...");
                CloseCurrentSample();

                _serviceLogger.Info("SHUTDOWN", "Logging session end...");
                _activityLogger.LogSessionEndAndDailyTotal();
            }
            catch (Exception ex)
            {
                _serviceLogger.Error("SHUTDOWN", "Error during cleanup: {0}", ex.Message);
            }

            _serviceLogger.Info("SHUTDOWN", "=== StandUp Tracker Shutdown Complete ===");
        }

        #endregion

        #region Timer Logic

        private void OnTimerTick(object? sender, ElapsedEventArgs e)
        {
            try
            {
                ProcessTick();
            }
            catch (Exception ex)
            {
                _serviceLogger.Error("TIMER", "Timer tick error: {0}", ex.Message);
            }
        }

        private void ProcessTick()
        {
            int idleSeconds = _idleTracker.GetIdleSeconds();

            // Log significant idle time changes
            if (Math.Abs(idleSeconds - _lastIdleSeconds) > 5)
            {
                _serviceLogger.Debug("IDLE", "Idle time changed: {0}s (was {1}s)", idleSeconds, _lastIdleSeconds);
                _lastIdleSeconds = idleSeconds;
            }

            // Check grace warning before state processing
            _reminderScheduler.CheckGraceWarning(idleSeconds, _stateMachine.CurrentState == AppState.Active);

            // Check if active work window should prevent idle
            bool preventIdleForActiveWork = false;
            if (idleSeconds >= AppSettings.ResetIdleMinutes * 60 && _stateMachine.CurrentState == AppState.Active)
            {
                preventIdleForActiveWork = _stateMachine.ShouldPreventIdleForActiveWork(_currentSample);
                if (preventIdleForActiveWork)
                {
                    _serviceLogger.Info("ACTIVE_WORK", "Idle timeout reached ({0}s) but active work detected - staying active",
                        idleSeconds);
                }
            }

            // Process state machine (unless prevented by active work)
            var effectiveIdleSeconds = preventIdleForActiveWork ? 0 : idleSeconds;
            var result = _stateMachine.ProcessTick(effectiveIdleSeconds, _idleTracker.IsLocked);

            if (result.StateChanged)
            {
                if (_stateMachine.CurrentState == AppState.Idle || _stateMachine.CurrentState == AppState.Locked)
                {
                    _reminderScheduler.ResetGraceBalloon();
                    CloseCurrentSample();
                }
            }

            // Continue processing only if in active state
            if (result.ShouldContinue)
            {
                _reminderScheduler.ResetGraceBalloonIfNeeded(idleSeconds);
                HandleWindowTracking();
                HandleReminders();
            }

            UpdateTooltip();
        }

        #endregion

        #region Window Tracking

        private void HandleWindowTracking()
        {
            if (_stateMachine.CurrentState != AppState.Active) return;

            var nowSample = _windowTracker.CaptureActiveSample();

            if (nowSample == null)
            {
                if (_currentSample != null)
                {
                    _serviceLogger.Debug("WINDOW", "Window lost: {0} - {1}",
                        _currentSample.Process, _currentSample.Title);
                }
                CloseCurrentSample();
            }
            else if (!AreSameWindow(_currentSample, nowSample))
            {
                if (_currentSample != null)
                {
                    _serviceLogger.Debug("WINDOW", "Window changed: {0}-{1} -> {2}-{3}",
                        _currentSample.Process, _currentSample.Title,
                        nowSample.Process, nowSample.Title);
                }
                else
                {
                    _serviceLogger.Debug("WINDOW", "Window appeared: {0} - {1}",
                        nowSample.Process, nowSample.Title);
                }

                CloseCurrentSample();
                _currentSample = nowSample;
            }
        }

        private void CloseCurrentSample()
        {
            if (_currentSample == null) return;

            try
            {
                _activityLogger.Append(_currentSample);
                _serviceLogger.Debug("SAMPLE", "Sample closed: {0} - {1}",
                    _currentSample.Process, _currentSample.Title);
            }
            catch (Exception ex)
            {
                _serviceLogger.Error("SAMPLE", "Failed to close current sample: {0}", ex.Message);
            }
            finally
            {
                _currentSample = null;
            }
        }

        private static bool AreSameWindow(ActiveSample? a, ActiveSample? b)
        {
            if (a == null || b == null) return false;

            return string.Equals(a.Process, b.Process, StringComparison.OrdinalIgnoreCase)
                && string.Equals(a.Title, b.Title, StringComparison.Ordinal);
        }

        #endregion

        #region Reminders

        private void HandleReminders()
        {
            if (_stateMachine.CurrentState != AppState.Active) return;

            if (_reminderScheduler.CheckReminder(_stateMachine.ActiveStart, _stateMachine.NextReminderAt))
            {
                _stateMachine.OnReminderShown();
            }
        }

        #endregion

        #region Event Handlers

        private void OnStateMachineStateChanged(object? sender, StateChangedEventArgs e)
        {
            StateChanged?.Invoke(this, e);
        }

        private void OnSessionStarted(object? sender, SessionEventArgs e)
        {
            _activityLogger.LogSessionStart();
            _currentSample = _windowTracker.CaptureActiveSample();

            if (_currentSample != null)
            {
                _serviceLogger.Debug("WINDOW", "Session started with window: {0} - {1}",
                    _currentSample.Process, _currentSample.Title);
            }
        }

        private void OnBreakStarted(object? sender, SessionEventArgs e)
        {
            _activityLogger.LogBreakStart();
        }

        private void OnReminderDue(object? sender, ReminderEventArgs e)
        {
            ReminderDue?.Invoke(this, e);
        }

        private void OnGraceWarningDue(object? sender, GraceWarningEventArgs e)
        {
            GraceWarningDue?.Invoke(this, e);
        }

        #endregion

        #region Helpers

        private void UpdateTooltip()
        {
            var text = GetTooltipText();
            TooltipUpdateRequired?.Invoke(this, new TooltipUpdateEventArgs(text));
        }

        private void ValidateSettings()
        {
            _serviceLogger.Info("CONFIG", "Validating settings...");
            _serviceLogger.Debug("CONFIG", "ResetIdleMinutes: {0} ({1}s)", AppSettings.ResetIdleMinutes, AppSettings.ResetIdleMinutes * 60);
            _serviceLogger.Debug("CONFIG", "BreakAfterMinutes: {0}", AppSettings.BreakAfterMinutes);
            _serviceLogger.Debug("CONFIG", "GraceBeforeBreakSeconds: {0}", AppSettings.GraceBeforeBreakSeconds);
            _serviceLogger.Debug("CONFIG", "ReminderRepeatMinutes: {0}", AppSettings.ReminderRepeatMinutes);
            _serviceLogger.Debug("CONFIG", "MuteWhenPresenting: {0}", AppSettings.MuteWhenPresenting);
            
            // Note: No validation needed - AppSettings uses compile-time constants
            _serviceLogger.Info("CONFIG", "Settings loaded successfully");
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                _timer?.Stop();
                _timer?.Dispose();
                _idleTracker?.Dispose();

                _stateMachine.StateChanged -= OnStateMachineStateChanged;
                _stateMachine.SessionStarted -= OnSessionStarted;
                _stateMachine.BreakStarted -= OnBreakStarted;

                _reminderScheduler.ReminderDue -= OnReminderDue;
                _reminderScheduler.GraceWarningDue -= OnGraceWarningDue;
            }

            _disposed = true;
        }

        #endregion
    }
}
