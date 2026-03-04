namespace StandUpTracker.Config
{
    public enum NotificationMode
    {
        CustomOverlay,
        WindowsBalloon,
        Both
    }

    public static class AppSettings
    {
        // --- Core settings ---
        public const int BreakAfterMinutes = 45;  // minutes of continuous activity before reminder
        public const int ResetIdleMinutes  = 10;  // idle >= this = break (resets continuous activity)
        public const int TickSeconds       = 5;   // timer tick
        public const int ReminderRepeatMinutes = 5;
        public const int AnnoyingReminderAfterMinutes = 60; // escalate reminders after 1h continuous work
        public const int AnnoyingReminderRepeatMinutes = 1;  // repeat aggressive reminders every minute
        public const int GraceBeforeBreakSeconds = 30; // How many seconds before break to show warning

        // Notification UI mode: CustomOverlay (default), WindowsBalloon, or Both
        public static readonly NotificationMode ReminderNotificationMode = NotificationMode.CustomOverlay;

        // Custom overlay appearance/placement
        public const double CustomNotificationOpacity = 0.72;
        public const int CustomNotificationWidth = 320;
        public const int CustomNotificationHeight = 100;
        public const int CustomNotificationMarginRight = 18;
        public const int CustomNotificationMarginBottom = 18;
        public const string CustomNotificationBackgroundColorHex = "#B00020";
        public const string CustomNotificationTextColorHex = "#FFFFFF";

        // Mute reminders during fullscreen/likely sharing/PPT slideshow
        public const bool MuteWhenPresenting = true;

        // --- Pomodoro settings ---
        public const int PomodoroMinutes = 20;        // Default Pomodoro duration
        public const int PomodoroWarningMinutes = 3;  // Show warning X minutes before break

        // CSV logging root: %LOCALAPPDATA%\StandUpTracker\logs\YYYY-MM-DD.csv
        public static string LogsFolder =>
            System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                "StandUpTracker", "logs");

        public static readonly string[] ActiveWorkApplications = new[]
        {
            // Video conferencing
            "ms-teams",
            // Video and streaming
            "youtube", "netflix"
        };
    }
}
