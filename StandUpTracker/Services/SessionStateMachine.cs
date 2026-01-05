using System;
using StandUpTracker.Config;
using StandUpTracker.Models;

namespace StandUpTracker.Services
{
    /// <summary>
    /// Application states for the activity tracker.
    /// </summary>
    public enum AppState
    {
        Idle,
        Active,
        Locked,
        Paused
    }

    /// <summary>
    /// Event arguments for state transitions.
    /// </summary>
    public class StateChangedEventArgs : EventArgs
    {
        public AppState OldState { get; }
        public AppState NewState { get; }
        public string Reason { get; }

        public StateChangedEventArgs(AppState oldState, AppState newState, string reason)
        {
            OldState = oldState;
            NewState = newState;
            Reason = reason;
        }
    }

    /// <summary>
    /// Event arguments for session events.
    /// </summary>
    public class SessionEventArgs : EventArgs
    {
        public DateTime Timestamp { get; }
        public TimeSpan? SessionDuration { get; }

        public SessionEventArgs(DateTime timestamp, TimeSpan? sessionDuration = null)
        {
            Timestamp = timestamp;
            SessionDuration = sessionDuration;
        }
    }

    /// <summary>
    /// Manages state transitions (Idle, Active, Locked, Paused) with proper break tracking.
    /// Emits events for state changes and session start/end.
    /// </summary>
    public class SessionStateMachine
    {
        private readonly ServiceLogger _logger;
        private readonly object _stateLock = new object();

        private AppState _currentState = AppState.Idle;
        private bool _paused = false;
        private bool _breakTaken = false;
        private DateTime _activeStart;
        private DateTime _nextReminderAt;

        /// <summary>
        /// Fires when the application state changes.
        /// </summary>
        public event EventHandler<StateChangedEventArgs>? StateChanged;

        /// <summary>
        /// Fires when a new active session starts (user becomes active after break).
        /// </summary>
        public event EventHandler<SessionEventArgs>? SessionStarted;

        /// <summary>
        /// Fires when a break starts (user goes idle or locks session).
        /// </summary>
        public event EventHandler<SessionEventArgs>? BreakStarted;

        public AppState CurrentState
        {
            get { lock (_stateLock) return _currentState; }
        }

        public bool IsPaused
        {
            get { lock (_stateLock) return _paused; }
        }

        public DateTime ActiveStart
        {
            get { lock (_stateLock) return _activeStart; }
        }

        public DateTime NextReminderAt
        {
            get { lock (_stateLock) return _nextReminderAt; }
        }

        public SessionStateMachine(ServiceLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            InitializeState();
        }

        private void InitializeState()
        {
            _activeStart = DateTime.Now;
            _nextReminderAt = _activeStart.AddMinutes(AppSettings.BreakAfterMinutes);
            _logger.Info("STATE", "Initial state set - ActiveStart: {0}, NextReminder: {1}",
                _activeStart.ToString("HH:mm:ss"), _nextReminderAt.ToString("HH:mm:ss"));
        }

        /// <summary>
        /// Sets the paused state (user toggled pause from menu).
        /// </summary>
        public void SetPaused(bool paused)
        {
            lock (_stateLock)
            {
                if (_paused == paused) return;

                _paused = paused;
                _logger.Info("USER_ACTION", "Pause state changed to: {0}", paused);

                if (paused)
                {
                    var oldState = _currentState;
                    _currentState = AppState.Paused;
                    OnStateChanged(oldState, AppState.Paused, "User paused");
                }
            }
        }

        /// <summary>
        /// Handles the timer tick and updates state based on idle time and session lock.
        /// Returns true if processing should stop (paused or locked).
        /// </summary>
        public StateTransitionResult ProcessTick(int idleSeconds, bool isSessionLocked)
        {
            lock (_stateLock)
            {
                // Handle paused state
                if (_paused)
                {
                    if (_currentState != AppState.Paused)
                    {
                        var oldState = _currentState;
                        _currentState = AppState.Paused;
                        OnStateChanged(oldState, AppState.Paused, "Paused");
                    }
                    return new StateTransitionResult(shouldContinue: false, stateChanged: false);
                }

                // Handle locked session
                if (isSessionLocked)
                {
                    return HandleLockedSession();
                }

                // Handle idle state (check if user has been idle long enough for a break)
                if (idleSeconds >= AppSettings.ResetIdleMinutes * 60)
                {
                    return HandleIdleTimeout(idleSeconds);
                }

                // Handle active state (user is active)
                return HandleActiveState(idleSeconds);
            }
        }

        /// <summary>
        /// Checks if the current active window is a "work" application that should prevent idle transition.
        /// </summary>
        public bool ShouldPreventIdleForActiveWork(ActiveSample? currentSample)
        {
            if (currentSample == null) return false;

            var processName = currentSample.Process?.ToLowerInvariant() ?? "";
            var windowTitle = currentSample.Title?.ToLowerInvariant() ?? "";

            foreach (var app in AppSettings.ActiveWorkApplications)
            {
                if (processName.Contains(app.ToLowerInvariant()))
                {
                    _logger.Debug("ACTIVE_WORK", "Active work detected by process: {0} contains {1}",
                        processName, app);
                    return true;
                }
            }

            if (windowTitle.Contains("teams") || windowTitle.Contains("youtube"))
            {
                _logger.Debug("ACTIVE_WORK", "Active work detected by window title: {0}", windowTitle);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Called when a reminder was shown. Schedules the next reminder.
        /// </summary>
        public void OnReminderShown()
        {
            lock (_stateLock)
            {
                _nextReminderAt = DateTime.Now.AddMinutes(AppSettings.ReminderRepeatMinutes);
                _logger.Info("REMINDER", "Next reminder scheduled for: {0}",
                    _nextReminderAt.ToString("HH:mm:ss"));
            }
        }

        private StateTransitionResult HandleLockedSession()
        {
            if (_currentState == AppState.Locked)
            {
                return new StateTransitionResult(shouldContinue: false, stateChanged: false);
            }

            var oldState = _currentState;
            _logger.Info("STATE", "State change: {0} -> Locked (session locked)", oldState);

            if (oldState == AppState.Active)
            {
                var duration = DateTime.Now - _activeStart;
                OnBreakStarted(duration);
                _logger.Info("ACTIVITY", "Break started due to session lock");
            }

            _currentState = AppState.Locked;
            _breakTaken = true;
            OnStateChanged(oldState, AppState.Locked, "Session locked");

            return new StateTransitionResult(shouldContinue: false, stateChanged: true);
        }

        private StateTransitionResult HandleIdleTimeout(int idleSeconds)
        {
            if (_currentState != AppState.Active)
            {
                return new StateTransitionResult(shouldContinue: false, stateChanged: false);
            }

            var oldState = _currentState;
            _logger.Info("STATE", "State change: Active -> Idle (idle for {0}s, threshold {1}s)",
                idleSeconds, AppSettings.ResetIdleMinutes * 60);

            var duration = DateTime.Now - _activeStart;
            OnBreakStarted(duration);
            _logger.Info("ACTIVITY", "Break started due to idle timeout");

            _currentState = AppState.Idle;
            _breakTaken = true;
            OnStateChanged(oldState, AppState.Idle, $"Idle for {idleSeconds}s");

            return new StateTransitionResult(shouldContinue: false, stateChanged: true);
        }

        private StateTransitionResult HandleActiveState(int idleSeconds)
        {
            if (_currentState == AppState.Active)
            {
                return new StateTransitionResult(shouldContinue: true, stateChanged: false);
            }

            // Transition from Idle/Locked to Active
            var oldState = _currentState;
            _logger.Info("STATE", "State change: {0} -> Active (user activity detected, idle {1}s)",
                oldState, idleSeconds);

            StartNewActiveSession(resetReminderSchedule: _breakTaken);
            _breakTaken = false;

            OnStateChanged(oldState, AppState.Active, "User activity detected");

            return new StateTransitionResult(shouldContinue: true, stateChanged: true);
        }

        private void StartNewActiveSession(bool resetReminderSchedule)
        {
            _activeStart = DateTime.Now;

            if (resetReminderSchedule)
            {
                _nextReminderAt = _activeStart.AddMinutes(AppSettings.BreakAfterMinutes);
                _logger.Info("SESSION", "New active session started - Next reminder at {0}",
                    _nextReminderAt.ToString("HH:mm:ss"));
            }
            else
            {
                _logger.Info("SESSION", "Resumed active session - Keeping existing reminder schedule (next at {0})",
                    _nextReminderAt.ToString("HH:mm:ss"));
            }

            _currentState = AppState.Active;
            OnSessionStarted();
        }

        private void OnStateChanged(AppState oldState, AppState newState, string reason)
        {
            StateChanged?.Invoke(this, new StateChangedEventArgs(oldState, newState, reason));
        }

        private void OnSessionStarted()
        {
            SessionStarted?.Invoke(this, new SessionEventArgs(DateTime.Now));
        }

        private void OnBreakStarted(TimeSpan sessionDuration)
        {
            BreakStarted?.Invoke(this, new SessionEventArgs(DateTime.Now, sessionDuration));
        }
    }

    /// <summary>
    /// Result of a state transition processing.
    /// </summary>
    public class StateTransitionResult
    {
        /// <summary>
        /// Whether the caller should continue processing (window tracking, reminders).
        /// False when paused, locked, or idle.
        /// </summary>
        public bool ShouldContinue { get; }

        /// <summary>
        /// Whether the state actually changed during this tick.
        /// </summary>
        public bool StateChanged { get; }

        public StateTransitionResult(bool shouldContinue, bool stateChanged)
        {
            ShouldContinue = shouldContinue;
            StateChanged = stateChanged;
        }
    }
}
