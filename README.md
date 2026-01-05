# StandUpTracker

> **A Windows system tray application that helps you maintain healthy work habits through intelligent break reminders and activity tracking.**

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows-0078D4)](https://www.microsoft.com/windows)
[![Tests](https://img.shields.io/badge/Tests-54%20passed-success)](https://xunit.net/)

## ✨ Features

### Core Functionality
- ⏰ **Smart Break Reminders** - Reminds you to take breaks after 55 minutes of continuous activity
- 🖱️ **Activity Tracking** - Monitors keyboard and mouse activity to determine active work time
- 📊 **Window Logging** - Records active window (process name + window title, including browser tabs)
- 📝 **Daily CSV Logs** - Automatically logs activity to `%LOCALAPPDATA%\StandUpTracker\logs\YYYY-MM-DD.csv`
- 📈 **Work/Break Reports** - Beautiful HTML reports with visual timeline (see [ReportGenerator](ReportGenerator/))
- 🎯 **State Machine** - Intelligent state management (Idle, Active, Locked, Paused)
- 🔕 **Do Not Disturb Mode** - Automatically mutes notifications during:
  - Fullscreen applications
  - PowerPoint presentations
  - Screen sharing sessions (detected heuristically)

### Pomodoro Timer
- 🍅 **Built-in Pomodoro Timer** - 20-minute work sessions with configurable breaks
- ⚠️ **Advance Warning** - Notifies you 3 minutes before break time
- ⏸️ **Pause/Resume** - Full control over your work sessions
- 📊 **Progress Tracking** - Real-time countdown in tray tooltip

### User Interface
- 🖥️ **System Tray Integration** - Unobtrusive, always accessible
- 📈 **Daily Statistics** - View today's activity summary
- 📂 **Quick Log Access** - Open logs folder directly from menu
- ⏸️ **Pause Mode** - Temporarily disable tracking and reminders
- 🎨 **Real-time Tooltip** - Shows current state and time information

## 🏗️ Architecture

StandUpTracker follows **event-driven architecture** with clear separation of concerns:

```
UI Layer (TrayApp)
    ↓ subscribes to events
Business Logic (ActivityMonitor)
    ↓ coordinates
Services Layer
├── SessionStateMachine (state transitions)
├── ReminderScheduler (timing logic)
├── PomodoroTimer (Pomodoro sessions)
├── IdleTracker (system idle detection)
├── WindowTracker (active window capture)
├── DoNotDisturbService (presentation detection)
└── ActivityLogger (CSV logging)
```

### Key Components

| Component | Responsibility | Lines of Code |
|-----------|---------------|---------------|
| **TrayApp** | UI only - tray icon, menus, notifications | 410 |
| **ActivityMonitor** | Orchestrates all services via timer | 475 |
| **SessionStateMachine** | State transitions and break tracking | 339 |
| **ReminderScheduler** | Reminder timing and DND logic | 175 |
| **PomodoroTimer** | Pomodoro session management | 233 |
| **ActivityLogger** | CSV logging with daily rotation | — |
| **ServiceLogger** | Thread-safe debug logging | — |

## 🚀 Getting Started

### Prerequisites
- Windows 10/11
- .NET 8 SDK or Visual Studio 2022+

### Build & Run

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd standupTracker_github
   ```

2. **Build the solution**
   ```bash
   dotnet build StandUpTracker.sln -c Release
   ```

3. **Run the application**
   ```bash
   cd StandUpTracker\bin\Release\net8.0-windows
   .\StandUpTracker.exe
   ```

The application will appear in the **system tray** (notification area).

### Auto-start on Windows Login

1. Press `Win + R` and type `shell:startup`
2. Create a shortcut to `StandUpTracker.exe` in the opened folder

## ⚙️ Configuration

All settings are in `StandUpTracker/Config/AppSettings.cs`:

```csharp
public static class AppSettings
{
    // Break reminder settings
    public const int BreakAfterMinutes = 55;         // Remind after 55 min of continuous work
    public const int ResetIdleSeconds = 600;         // 10 min idle = break taken
    public const int GraceBeforeBreakSeconds = 20;   // Warn 20s before break
    public const int ReminderRepeatMinutes = 5;      // Repeat reminder every 5 min
    
    // System settings
    public const int TickSeconds = 5;                // Process tick interval
    public const bool MuteWhenPresenting = true;     // Mute during presentations
    
    // Pomodoro settings
    public const int PomodoroMinutes = 20;           // Pomodoro duration
    public const int PomodoroWarningMinutes = 3;     // Warning before break
    
    // Active work detection (prevents idle timeout during passive activities)
    public static readonly string[] ActiveWorkApplications = new[]
    {
        "ms-teams",   // Video conferencing
        "youtube",    // Video streaming
        "netflix"     // Entertainment
    };
}
```

## 📊 Activity Logging

### CSV Format

Logs are saved to: `%LOCALAPPDATA%\StandUpTracker\logs\YYYY-MM-DD.csv`

```csv
start,end,duration_sec,process,title
12:05:01,12:07:36,155,chrome.exe,"YouTube — Video Title"
12:07:36,12:10:45,189,Code.exe,"StandUpTracker - Visual Studio Code"
12:10:45,12:10:45,0,SESSION_START,""
12:30:00,12:30:00,0,BREAK_START,""
#TOTAL_ACTIVE_SEC,3600
```

### Special Markers
- `SESSION_START` - New active session began (after break)
- `BREAK_START` - User went on break (idle or locked)
- `#TOTAL_ACTIVE_SEC` - Total active seconds for the day (last line)

### Daily Rotation
- New CSV file created automatically at midnight
- Previous day's session closed and totals written

## 🎮 Usage

### Tray Menu Options
- **Show Today's Statistics** - Displays total active time, breaks, and window activity
- **Pomodoro Timer** - Start/stop/pause Pomodoro sessions
- **Pause Tracking** - Temporarily disable all tracking and reminders
- **Open Logs Folder** - Quick access to CSV logs
- **Exit** - Close the application

### State Machine Behavior

```
┌──────┐  user active    ┌────────┐  idle >= 10 min
│ Idle │ ──────────────► │ Active │ ───────────────┐
└──────┘                 └────────┘                 │
                             │  ▲                   │
      session lock           │  │  session unlock   │
                             ▼  │                   ▼
                         ┌────────┐             ┌──────┐
                         │ Locked │             │ Idle │
                         └────────┘             └──────┘
```

### Break Tracking Logic

**Problem Solved:** Previously, the app would immediately remind again after a short break because it used stale active start time.

**Solution:** Track `_breakTaken` flag:
- ✅ Idle >= 10 minutes → Sets `_breakTaken = true` → Reminder timer resets
- ✅ Session locked → Sets `_breakTaken = true` → Reminder timer resets  
- ❌ Brief idle < 10 minutes → `_breakTaken = false` → Reminder timer continues

**Result:** Only proper breaks (10+ minutes) reset the reminder timer.

### Active Work Detection

**Special case:** Prevent idle timeout if user is watching Teams/YouTube:
- If process name contains: `ms-teams`, `youtube`, `netflix`
- Window title contains: `teams`, `youtube`
- **AND** idle time >= 10 minutes
- **THEN** don't transition to Idle state

This prevents interrupting video calls or training videos.

## 🧪 Testing

The project includes comprehensive unit tests covering all critical functionality:

```bash
dotnet test StandUpTracker.Tests
```

**Test Coverage (54 tests, 100% pass rate):**
- ✅ SessionStateMachine - State transitions and break tracking
- ✅ ReminderScheduler - Reminder timing and muting
- ✅ ActivityMonitor - Service orchestration
- ✅ ActivityLogger - CSV logging and rotation
- ✅ PomodoroTimer - Pomodoro session management
- ✅ ServiceLogger - Thread-safe logging

### TDD Approach
This project follows **Test-Driven Development**:
1. Write tests first (Red)
2. Implement minimum code to pass (Green)
3. Refactor while keeping tests green

## 📁 Project Structure

```
StandUpTracker/
├── Program.cs                    # Entry point
├── Config/
│   └── AppSettings.cs           # Configuration constants
├── Models/
│   └── ActiveSample.cs          # Window sample model
├── Services/
│   ├── ActivityMonitor.cs       # Main orchestrator
│   ├── SessionStateMachine.cs   # State logic
│   ├── ReminderScheduler.cs     # Reminder timing
│   ├── PomodoroTimer.cs         # Pomodoro sessions
│   ├── ServiceLogger.cs         # Debug logging
│   ├── IdleTracker.cs           # Idle detection
│   ├── WindowTracker.cs         # Window capture
│   ├── DoNotDisturbService.cs   # DND detection
│   └── ActivityLogger.cs        # CSV logging
├── UI/
│   ├── TrayApp.cs               # Tray UI
│   └── TrayApp.resx             # Resources
└── Utils/
    ├── NativeMethods.cs         # Win32 P/Invoke
    └── SortedHelpers.cs         # Utility helpers

StandUpTracker.Tests/
└── Services/
    ├── ActivityLoggerTests.cs
    ├── ActivityMonitorTests.cs
    ├── PomodoroTimerTests.cs
    ├── ReminderSchedulerTests.cs
    ├── ServiceLoggerTests.cs
    └── SessionStateMachineTests.cs
```

## 🔒 Privacy & Security

- ✅ **All data stays local** - Nothing is sent to any server
- ✅ **CSV logs stored locally** - `%LOCALAPPDATA%\StandUpTracker\logs\`
- ✅ **No network access required** - 100% offline application
- ✅ **Open source** - Full transparency, audit the code yourself

## 🐛 Known Issues & Considerations

### Active Work Detection Caveat
Watching YouTube for 2+ hours won't trigger break because it's in `ActiveWorkApplications`. This is intentional but debatable - passive video watching should probably trigger reminders.

**Consider:** Remove YouTube from active work list, or add "passive watching" detection.

### Grace Period Calculation
```csharp
graceStart = max(5, ResetIdleSeconds - GraceBeforeBreakSeconds)
// Example: max(5, 600 - 20) = 580 seconds
// Warns at 580s, breaks at 600s
```

### Tooltip Length Limit
Windows tray tooltips are limited to 63 characters. Text is automatically truncated.

### Thread Safety
- `System.Timers.Timer` runs on thread pool
- Always use `SafeInvoke()` when updating UI from timer thread
- State machine uses `_stateLock` for thread safety
- `ServiceLogger` uses internal `_logLock`

## 🛠️ Development Guidelines

### Adding New Features

**Example: Add "Focus Mode" that disables reminders for 2 hours**

1. **Add state/flag to SessionStateMachine**
2. **Add event** for UI notification
3. **Check in reminder logic**
4. **Add menu item in TrayApp**

See `.github/copilot-instructions.md` for detailed development guidelines.

## 📜 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🤝 Contributing

Contributions are welcome! Please ensure:
- Follow TDD approach (write tests first)
- Maintain event-driven architecture
- Respect separation of concerns
- All tests pass before committing

## 📞 Support

For issues, questions, or suggestions, please open an issue on GitHub.

---

**Last Updated:** January 3, 2026  
**Version:** 1.0  
**Framework:** .NET 8.0 (Windows)
