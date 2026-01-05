using System;
using StandUpTracker.Config;

namespace StandUpTracker.Services
{
    /// <summary>
    /// Event arguments for reminder events.
    /// </summary>
    public class ReminderEventArgs : EventArgs
    {
        public TimeSpan SessionDuration { get; }
        public bool IsMuted { get; }
        public string MuteReason { get; }

        public ReminderEventArgs(TimeSpan sessionDuration, bool isMuted, string muteReason = "")
        {
            SessionDuration = sessionDuration;
            IsMuted = isMuted;
            MuteReason = muteReason;
        }
    }

    /// <summary>
    /// Event arguments for grace period warnings.
    /// </summary>
    public class GraceWarningEventArgs : EventArgs
    {
        public int IdleSeconds { get; }
        public int SecondsUntilBreak { get; }
        public bool IsMuted { get; }

        public GraceWarningEventArgs(int idleSeconds, int secondsUntilBreak, bool isMuted)
        {
            IdleSeconds = idleSeconds;
            SecondsUntilBreak = secondsUntilBreak;
            IsMuted = isMuted;
        }
    }

    /// <summary>
    /// Manages reminder scheduling, grace periods, and muting logic.
    /// </summary>
    public class ReminderScheduler
    {
        private readonly ServiceLogger _logger;
        private readonly DoNotDisturbService _dnd;

        private bool _graceBalloonShown = false;

        /// <summary>
        /// Fires when it's time to show a stand-up reminder.
        /// </summary>
        public event EventHandler<ReminderEventArgs>? ReminderDue;

        /// <summary>
        /// Fires when the grace period warning should be shown.
        /// </summary>
        public event EventHandler<GraceWarningEventArgs>? GraceWarningDue;

        public ReminderScheduler(ServiceLogger logger, DoNotDisturbService dnd)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dnd = dnd ?? throw new ArgumentNullException(nameof(dnd));
        }

        /// <summary>
        /// Checks if a reminder should be shown and fires the appropriate event.
        /// </summary>
        /// <param name="activeStart">When the current active session started.</param>
        /// <param name="nextReminderAt">When the next reminder is scheduled.</param>
        /// <returns>True if a reminder was triggered.</returns>
        public bool CheckReminder(DateTime activeStart, DateTime nextReminderAt)
        {
            var now = DateTime.Now;
            if (now < nextReminderAt) return false;

            var sessionDuration = now - activeStart;
            _logger.Info("REMINDER", "Reminder time reached - Session duration: {0}",
                sessionDuration.ToString(@"hh\:mm\:ss"));

            var (isMuted, muteReason) = CheckMuteStatus();
            
            if (isMuted)
            {
                _logger.Info("REMINDER", "Reminder muted - {0}", muteReason);
            }
            else
            {
                _logger.Info("REMINDER", "Showing stand-up reminder");
            }

            OnReminderDue(sessionDuration, isMuted, muteReason);
            return true;
        }

        /// <summary>
        /// Checks if grace period warning should be shown.
        /// </summary>
        /// <param name="idleSeconds">Current idle time in seconds.</param>
        /// <param name="isActive">Whether the user is in active state.</param>
        public void CheckGraceWarning(int idleSeconds, bool isActive)
        {
            if (!isActive) return;
            if (_graceBalloonShown) return;

            var graceStart = CalculateGraceStart();
            if (idleSeconds < graceStart || idleSeconds >= AppSettings.ResetIdleMinutes * 60) return;

            var secondsUntilBreak = (AppSettings.ResetIdleMinutes * 60) - idleSeconds;

            _logger.Info("GRACE", "Grace balloon conditions met - Idle: {0}s, Grace start: {1}s, Reset threshold: {2}s",
                idleSeconds, graceStart, AppSettings.ResetIdleMinutes * 60);

            var (isMuted, muteReason) = CheckMuteStatus();

            if (isMuted)
            {
                _logger.Info("GRACE", "Grace balloon muted - {0}", muteReason);
            }
            else
            {
                _logger.Info("GRACE", "Showing grace balloon");
            }

            _graceBalloonShown = true;
            OnGraceWarningDue(idleSeconds, secondsUntilBreak, isMuted);
        }

        /// <summary>
        /// Resets the grace balloon flag when user becomes active again.
        /// </summary>
        /// <param name="idleSeconds">Current idle time in seconds.</param>
        public void ResetGraceBalloonIfNeeded(int idleSeconds)
        {
            var graceStart = CalculateGraceStart();
            if (_graceBalloonShown && idleSeconds < graceStart)
            {
                _logger.Debug("GRACE", "Grace balloon reset due to user activity (idle {0}s < grace {1}s)",
                    idleSeconds, graceStart);
                _graceBalloonShown = false;
            }
        }

        /// <summary>
        /// Resets the grace balloon flag (called when transitioning to idle/locked).
        /// </summary>
        public void ResetGraceBalloon()
        {
            _graceBalloonShown = false;
        }

        /// <summary>
        /// Checks if notifications should be muted.
        /// </summary>
        public (bool IsMuted, string Reason) CheckMuteStatus()
        {
            var dndActive = _dnd.IsDoNotDisturb();
            var shouldMute = AppSettings.MuteWhenPresenting && dndActive;

            _logger.Debug("NOTIFICATION", "Mute check - MuteWhenPresenting: {0}, DND active: {1}, Result: {2}",
                AppSettings.MuteWhenPresenting, dndActive, shouldMute);

            var reason = shouldMute 
                ? $"MuteWhenPresenting: {AppSettings.MuteWhenPresenting}, DND active: {dndActive}"
                : "";

            return (shouldMute, reason);
        }

        private int CalculateGraceStart()
        {
            return Math.Max(5, Math.Max(1, (AppSettings.ResetIdleMinutes * 60) - AppSettings.GraceBeforeBreakSeconds));
        }

        private void OnReminderDue(TimeSpan sessionDuration, bool isMuted, string muteReason)
        {
            ReminderDue?.Invoke(this, new ReminderEventArgs(sessionDuration, isMuted, muteReason));
        }

        private void OnGraceWarningDue(int idleSeconds, int secondsUntilBreak, bool isMuted)
        {
            GraceWarningDue?.Invoke(this, new GraceWarningEventArgs(idleSeconds, secondsUntilBreak, isMuted));
        }
    }
}
