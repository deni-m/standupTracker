namespace StandUpTracker.Config
{
    public static class AppSettings
    {
        // --- Core settings ---
        public const int BreakAfterMinutes = 55;  // minutes of continuous activity before reminder
        public const int ResetIdleSeconds  = 600; // idle >= this = break (resets continuous activity)
        public const int TickSeconds       = 5;   // timer tick
        public const int ReminderRepeatMinutes = 5;
        public const int GraceBeforeBreakSeconds = 20; // за скільки секунд попередити перед перервою

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
            // Відеоконференції
            "ms-teams", 
            // Відео та стримінг
            "youtube", "netflix"
        };
    }
}
