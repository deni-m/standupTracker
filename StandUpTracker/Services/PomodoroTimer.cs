using System;
using StandUpTracker.Config;

namespace StandUpTracker.Services
{
    /// <summary>
    /// Pomodoro timer service that tracks work sessions with configurable duration.
    /// Fires events for UI notifications at key points (warning, completion).
    /// </summary>
    public class PomodoroTimer
    {
        #region Fields

        private readonly ServiceLogger _logger;
        private readonly object _lock = new();

        private bool _isRunning;
        private DateTime _startTime;
        private TimeSpan _duration;
        private TimeSpan _timeRemaining;
        private bool _warningFired;

        #endregion

        #region Events

        /// <summary>Fired when the timer is started.</summary>
        public event EventHandler? Started;

        /// <summary>Fired when the timer is stopped manually.</summary>
        public event EventHandler? Stopped;

        /// <summary>Fired on each tick while running.</summary>
        public event EventHandler<PomodoroTickEventArgs>? Tick;

        /// <summary>Fired when warning time is reached (e.g., 3 minutes before break).</summary>
        public event EventHandler? WarningDue;

        /// <summary>Fired when the timer completes (reaches zero).</summary>
        public event EventHandler? Completed;

        #endregion

        #region Properties

        /// <summary>Whether the timer is currently running.</summary>
        public bool IsRunning
        {
            get { lock (_lock) return _isRunning; }
        }

        /// <summary>Time remaining until the break.</summary>
        public TimeSpan TimeRemaining
        {
            get { lock (_lock) return _timeRemaining; }
        }

        /// <summary>Configured duration in minutes.</summary>
        public int DurationMinutes => AppSettings.PomodoroMinutes;

        #endregion

        #region Constructor

        public PomodoroTimer(ServiceLogger logger)
        {
            _logger = logger;
            _duration = TimeSpan.FromMinutes(AppSettings.PomodoroMinutes);
            _timeRemaining = TimeSpan.Zero;
            _isRunning = false;
            _warningFired = false;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Starts the Pomodoro timer. If already running, resets to full duration.
        /// </summary>
        public void Start()
        {
            lock (_lock)
            {
                _startTime = DateTime.Now;
                _duration = TimeSpan.FromMinutes(AppSettings.PomodoroMinutes);
                _timeRemaining = _duration;
                _isRunning = true;
                _warningFired = false;

                _logger.Info("POMODORO", $"Timer started: {DurationMinutes} min");
            }

            Started?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Stops the timer and resets time remaining.
        /// </summary>
        public void Stop()
        {
            bool wasRunning;
            lock (_lock)
            {
                wasRunning = _isRunning;
                if (!wasRunning) return;

                _isRunning = false;
                _timeRemaining = TimeSpan.Zero;
                _warningFired = false;

                _logger.Info("POMODORO", "Timer stopped by user");
            }

            if (wasRunning)
            {
                Stopped?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Restarts the timer (stops if running, then starts fresh).
        /// </summary>
        public void Restart()
        {
            lock (_lock)
            {
                _startTime = DateTime.Now;
                _duration = TimeSpan.FromMinutes(AppSettings.PomodoroMinutes);
                _timeRemaining = _duration;
                _isRunning = true;
                _warningFired = false;

                _logger.Info("POMODORO", $"Timer restarted: {DurationMinutes} min");
            }

            Started?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Called on each timer tick (typically every 5 seconds).
        /// Updates time remaining and fires events as needed.
        /// </summary>
        public void ProcessTick()
        {
            bool fireWarning = false;
            bool fireCompleted = false;
            TimeSpan currentRemaining;

            lock (_lock)
            {
                if (!_isRunning) return;

                // Decrease time by tick interval
                _timeRemaining -= TimeSpan.FromSeconds(AppSettings.TickSeconds);

                // Clamp to zero
                if (_timeRemaining < TimeSpan.Zero)
                {
                    _timeRemaining = TimeSpan.Zero;
                }

                currentRemaining = _timeRemaining;

                // Check for warning threshold (3 minutes remaining)
                var warningThreshold = TimeSpan.FromMinutes(AppSettings.PomodoroWarningMinutes);
                if (!_warningFired && _timeRemaining <= warningThreshold && _timeRemaining > TimeSpan.Zero)
                {
                    _warningFired = true;
                    fireWarning = true;
                    _logger.Info("POMODORO", $"Warning: {_timeRemaining.TotalMinutes:F1} min remaining");
                }

                // Check for completion
                if (_timeRemaining <= TimeSpan.Zero)
                {
                    _isRunning = false;
                    fireCompleted = true;
                    _logger.Info("POMODORO", "Timer completed!");
                }
            }

            // Fire events outside lock
            Tick?.Invoke(this, new PomodoroTickEventArgs(currentRemaining));

            if (fireWarning)
            {
                WarningDue?.Invoke(this, EventArgs.Empty);
            }

            if (fireCompleted)
            {
                Completed?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Gets tooltip text showing time remaining.
        /// </summary>
        public string GetTooltipText()
        {
            lock (_lock)
            {
                if (!_isRunning)
                {
                    return string.Empty;
                }

                return $"Pomodoro: {_timeRemaining:mm\\:ss}";
            }
        }

        #endregion
    }

    #region Event Args

    /// <summary>
    /// Event args for Pomodoro timer tick.
    /// </summary>
    public class PomodoroTickEventArgs : EventArgs
    {
        public TimeSpan TimeRemaining { get; }

        public PomodoroTickEventArgs(TimeSpan timeRemaining)
        {
            TimeRemaining = timeRemaining;
        }
    }

    #endregion
}
