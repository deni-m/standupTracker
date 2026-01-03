# StandUpTracker - Copilot Context

## Overview

**StandUpTracker** is a Windows system tray application (.NET 8 WinForms) that reminds users to take breaks after prolonged computer activity. It tracks keyboard/mouse activity, logs active windows, and intelligently mutes notifications during presentations or screen sharing.

**Development Approach:** We follow **Test-Driven Development (TDD)**. Write tests first, then implement the feature. Tests document expected behavior and ensure reliability.

## Architecture

### Event-Driven Architecture with Clear Separation of Concerns

```
┌─────────────────────────────────────────────────────────────┐
│                        TrayApp (UI)                         │
│  - Tray icon, menu, balloon notifications                   │
│  - Subscribes to ActivityMonitor events                     │
└─────────────────────────┬───────────────────────────────────┘
                          │ events
                          ▼
┌─────────────────────────────────────────────────────────────┐
│                    ActivityMonitor                          │
│  - Orchestrates all services via timer tick (5s)            │
│  - Coordinates state machine, reminders, window tracking    │
│  - Emits events for UI consumption                          │
└───────┬─────────────────┬───────────────────┬───────────────┘
        │                 │                   │
        ▼                 ▼                   ▼
┌───────────────┐ ┌───────────────┐ ┌─────────────────────────┐
│ SessionState  │ │   Reminder    │ │   Low-Level Services    │
│   Machine     │ │   Scheduler   │ │ - IdleTracker           │
│               │ │               │ │ - WindowTracker         │
│ State logic   │ │ Timing logic  │ │ - DoNotDisturbService   │
│ + Events      │ │ + Events      │ │ - ActivityLogger        │
└───────────────┘ └───────────────┘ └─────────────────────────┘
```

### Key Components

#### 1. **TrayApp** (`UI/TrayApp.cs`) - 258 lines
**Responsibility:** UI only - no business logic
- Creates and manages system tray icon
- Builds context menu (stats, logs, pause, exit)
- Displays balloon notifications
- Subscribes to `ActivityMonitor` events

#### 2. **ActivityMonitor** (`Services/ActivityMonitor.cs`) - 475 lines
**Responsibility:** Orchestrates all activity monitoring
- Runs timer tick every 5 seconds
- Processes state transitions via `SessionStateMachine`
- Checks reminders via `ReminderScheduler`
- Tracks window changes and logs activity
- Emits high-level events for UI

**Events emitted:**
- `StateChanged` - when app state changes
- `ReminderDue` - when 55-min reminder should be shown
- `GraceWarningDue` - when 20s grace period warning is needed
- `TooltipUpdateRequired` - when tray tooltip should update
- `PauseStateChanged` - when user toggles pause

#### 3. **SessionStateMachine** (`Services/SessionStateMachine.cs`) - 285 lines
**Responsibility:** Manages application state transitions
- **States:** `Idle`, `Active`, `Locked`, `Paused`
- Tracks `_breakTaken` flag to fix break calculation bug
- Handles idle timeout, session lock, user activity
- Emits `StateChanged`, `SessionStarted`, `BreakStarted` events

**Key Logic:**
```csharp
// Break tracking fix: only reset reminder timer if user took a proper break
_breakTaken = true;  // Set when: idle >= 600s OR session locked
StartNewActiveSession(resetReminderSchedule: _breakTaken);
_breakTaken = false; // Reset after use
```

#### 4. **ReminderScheduler** (`Services/ReminderScheduler.cs`) - 175 lines
**Responsibility:** Reminder timing and muting logic
- Checks if 55-minute reminder is due
- Shows grace period warning (20s before break)
- Checks DND mode (fullscreen, PowerPoint, screen sharing)
- Emits `ReminderDue`, `GraceWarningDue` events

#### 5. **Low-Level Services**
- **IdleTracker** - Win32 API for idle time + session lock events
- **WindowTracker** - Captures active window (process + title)
- **DoNotDisturbService** - Detects fullscreen/presentations/screen sharing
- **ActivityLogger** - Logs window activity to daily CSV files
- **ServiceLogger** - Thread-safe debug logging to `Service/` folder

## State Machine Logic

### State Transitions

```
    ┌──────────────────────────────────────────────┐
    │                                              │
    ▼                                              │
┌────────┐  user active    ┌────────┐  idle>=600s │
│  Idle  │ ──────────────► │ Active │ ────────────┘
└────────┘                 └────────┘
                               │  ▲
        session lock           │  │  session unlock
                               ▼  │
                           ┌────────┐
                           │ Locked │
                           └────────┘
        user toggle
                           ┌────────┐
                           │ Paused │
                           └────────┘
```

### Break Tracking Logic

**Problem Solved:** Previously, the app would immediately remind again after a short break because it used stale `_activeStart` time.

**Solution:** Track `_breakTaken` flag:
```csharp
// When entering Idle state (idle >= 600s):
_breakTaken = true;

// When entering Locked state (session lock):
_breakTaken = true;

// When returning to Active state:
StartNewActiveSession(resetReminderSchedule: _breakTaken);
_breakTaken = false; // Reset for next cycle
```

**Result:** 
- User takes 10+ min break → Reminder timer resets to fresh 55 min
- User briefly idle < 10 min → Reminder timer continues countdown

### Active Work Detection

**Special case:** Prevent idle timeout if user is watching Teams/YouTube:
```csharp
if (idleSeconds >= 600 && IsActiveWorkWindow())
    return; // Don't transition to Idle
```

Checks:
- Process name contains "ms-teams", "youtube", "netflix"
- Window title contains "teams", "youtube"

## Configuration (`Config/AppSettings.cs`)

```csharp
BreakAfterMinutes = 55;          // Remind after 55 min continuous work
ResetIdleSeconds = 600;          // 10 min idle = break
GraceBeforeBreakSeconds = 20;   // Warn 20s before break
ReminderRepeatMinutes = 5;      // Repeat reminder every 5 min
TickSeconds = 5;                // Process tick every 5 seconds
MuteWhenPresenting = true;      // Mute during fullscreen/sharing
```

## CSV Logging

**Format:** `%LOCALAPPDATA%\StandUpTracker\logs\YYYY-MM-DD.csv`

```csv
start,end,duration_sec,process,title
12:05:01,12:07:36,155,chrome.exe,"YouTube — Video Title"
12:07:36,12:07:36,0,SESSION_START,""
12:30:00,12:30:00,0,BREAK_START,""
#TOTAL_ACTIVE_SEC,3600
```

**Daily rotation:** Automatically creates new file at midnight.

## Coding Guidelines

### 1. Respect Separation of Concerns
- **TrayApp:** UI only, never add business logic
- **ActivityMonitor:** Orchestration only, delegates to specialized services
- **State machine:** State transitions only, no UI code
- **Services:** Single responsibility, composable

### 2. Event-Driven Communication
- Use events for cross-layer communication
- Never call UI methods directly from services
- UI subscribes to monitor events, monitor subscribes to service events

### 3. Thread Safety
- Use `SafeInvoke()` when updating UI from timer thread
- Lock `_stateLock` when accessing state machine fields
- `ServiceLogger` uses internal `_logLock`

### 4. Testing Considerations
- State machine is testable without UI
- Mock `IdleTracker`, `WindowTracker`, etc. for unit tests
- Events make it easy to verify behavior

### 5. Adding New Features

**Example: Add "Focus Mode" that disables reminders for 2 hours**

1. Add state/flag to `SessionStateMachine`:
```csharp
private bool _focusModeEnabled = false;
private DateTime _focusModeUntil;

public void EnableFocusMode(int hours)
{
    _focusModeEnabled = true;
    _focusModeUntil = DateTime.Now.AddHours(hours);
}
```

2. Add event:
```csharp
public event EventHandler<FocusModeEventArgs>? FocusModeChanged;
```

3. Check in reminder logic:
```csharp
if (_focusModeEnabled && DateTime.Now < _focusModeUntil)
    return; // Skip reminder
```

4. Add menu item in `TrayApp`:
```csharp
menu.Items.Add("Focus Mode (2h)", null, (s, e) => 
    _monitor.EnableFocusMode(2));
```

## Common Tasks

### Task: Change reminder interval from 55 to 60 minutes
**File:** `Config/AppSettings.cs`
```csharp
public const int BreakAfterMinutes = 60;
```

### Task: Add new "active work" application
**File:** `Config/AppSettings.cs`
```csharp
public static readonly string[] ActiveWorkApplications = new[]
{
    "ms-teams", 
    "youtube", 
    "netflix",
    "spotify"  // Add here
};
```

### Task: Debug state transitions
**File:** `%LOCALAPPDATA%\StandUpTracker\logs\Service\service_YYYY-MM-DD.log`

Search for:
- `[STATE]` - State changes
- `[REMINDER]` - Reminder events
- `[GRACE]` - Grace period warnings
- `[IDLE]` - Idle time changes
- `[WINDOW]` - Window tracking

### Task: Customize balloon notification text
**File:** `UI/TrayApp.cs`
```csharp
private void OnReminderDue(object? sender, ReminderEventArgs e)
{
    ShowBalloonTip(
        "Custom Title",
        "Custom message here",
        4000
    );
}
```

## Known Issues & Decisions

### 1. ⚠️ Active Work Detection Caveat
Watching YouTube for 2 hours won't trigger break because it's in `ActiveWorkApplications`. This is intentional but debatable - passive video watching should probably trigger reminders.

**Consider:** Remove YouTube from active work list, or add "passive watching" detection.

### 2. Grace Period Calculation
```csharp
graceStart = max(5, ResetIdleSeconds - GraceBeforeBreakSeconds)
// Example: max(5, 600 - 20) = 580 seconds
// Warns at 580s, breaks at 600s
```

### 3. Tooltip Length Limit
Windows tray tooltips are limited to 63 characters. Text is truncated:
```csharp
if (text.Length > 63)
    text = text.Substring(0, 63);
```

### 4. Timer Thread vs UI Thread
`System.Timers.Timer` runs on thread pool. Always use `SafeInvoke()` when updating UI:
```csharp
SafeInvoke(() => _tray.Text = "Updated");
```

## Dependencies

- **.NET 8 Windows** - WinForms, Win32 APIs
- **No external NuGet packages** - Pure framework code
- **Windows-only** - Uses `GetLastInputInfo`, session events

## File Structure

```
StandUpTracker/
├── Program.cs                 # Entry point
├── Config/
│   └── AppSettings.cs        # Configuration constants
├── Models/
│   └── ActiveSample.cs       # Window sample model
├── Services/
│   ├── ActivityMonitor.cs    # Main orchestrator
│   ├── SessionStateMachine.cs # State logic
│   ├── ReminderScheduler.cs  # Reminder timing
│   ├── ServiceLogger.cs      # Debug logging
│   ├── IdleTracker.cs        # Idle detection
│   ├── WindowTracker.cs      # Window capture
│   ├── DoNotDisturbService.cs # DND detection
│   └── ActivityLogger.cs     # CSV logging
├── UI/
│   ├── TrayApp.cs            # Tray UI
│   └── TrayApp.resx          # Resources
└── Utils/
    ├── NativeMethods.cs      # Win32 P/Invoke
    └── SortedHelpers.cs      # Utility helpers
```

## Unit Test Coverage Plan

**Approach:** Test-Driven Development (TDD) - Write tests first, then implement.

### Priority 1: SessionStateMachine (Critical - Core Business Logic)

**File:** `Tests/SessionStateMachineTests.cs`

| Test Case | What to Test | Why Critical |
|-----------|-------------|--------------|
| `InitialState_ShouldBeIdle()` | State starts as `Idle` | Baseline assumption |
| `Idle_UserActivity_TransitionsToActive()` | `Idle` → `Active` on user activity | Core flow |
| `Active_IdleTimeout_TransitionsToIdle()` | `Active` → `Idle` after 600s idle | Break detection |
| `Active_IdleTimeout_SetsBreakTakenFlag()` | `_breakTaken = true` when idle timeout | Bug fix validation ✅ |
| `Active_SessionLock_TransitionsToLocked()` | `Active` → `Locked` on session lock | OS integration |
| `Locked_SessionUnlock_TransitionsToActive()` | `Locked` → `Active` on unlock | Resume flow |
| `Locked_SetsBreakTakenFlag()` | `_breakTaken = true` when locked | Bug fix validation ✅ |
| `BreakTaken_ResetsReminderSchedule()` | Reminder timer resets when `_breakTaken = true` | Fix for immediate reminders |
| `NoBreak_KeepsReminderSchedule()` | Reminder timer preserved when `_breakTaken = false` | Continuity after short idle |
| `SetPaused_TransitionsToParused()` | User pause → `Paused` state | UI control |
| `Paused_UserUnpause_ResumesFromPreviousState()` | Unpause → back to `Active`/`Idle` | State memory |
| `StateChanged_EventFired()` | Event fires on state transitions | Event contract |
| `SessionStarted_EventFired()` | Event fires when session starts | Logging hook |
| `BreakStarted_EventFired()` | Event fires when break starts | Logging hook |
| `ActiveWorkWindow_PreventsIdleTransition()` | Teams/YouTube watching prevents `Idle` | Special case |

**Mock Dependencies:** `ServiceLogger`

---

### Priority 2: ReminderScheduler (Important - User Experience)

**File:** `Tests/ReminderSchedulerTests.cs`

| Test Case | What to Test | Why Important |
|-----------|--------------|---------------|
| `CheckReminder_Before55Min_ReturnsFalse()` | No reminder before 55 min | Timing accuracy |
| `CheckReminder_After55Min_ReturnsTrue()` | Reminder at 55 min | Core feature |
| `CheckReminder_FiresReminderDueEvent()` | Event fires when reminder due | Event contract |
| `CheckReminder_Muted_EventHasMutedFlag()` | Muted flag set when DND active | Intelligent muting |
| `CheckGraceWarning_At580Seconds_ShowsWarning()` | Grace warning at 580s (20s before 600s) | Grace period timing |
| `CheckGraceWarning_Before580Seconds_NoWarning()` | No warning too early | Avoid spam |
| `CheckGraceWarning_After600Seconds_NoWarning()` | No warning after break starts | One-time only |
| `CheckGraceWarning_AlreadyShown_DoesNotRepeat()` | Grace balloon shown once | No repetition |
| `ResetGraceBalloon_UserActivity_ResetsFlag()` | Grace flag reset on user activity | Allow re-trigger |
| `CheckMuteStatus_FullscreenActive_ReturnsMuted()` | Muted during fullscreen | DND detection |
| `CheckMuteStatus_PowerPointActive_ReturnsMuted()` | Muted during PowerPoint | DND detection |
| `CheckMuteStatus_ScreenSharing_ReturnsMuted()` | Muted during screen sharing | DND detection |
| `CheckMuteStatus_NormalWindow_ReturnsNotMuted()` | Not muted normally | Default behavior |

**Mock Dependencies:** `ServiceLogger`, `DoNotDisturbService`

---

### Priority 3: ActivityLogger (Data Integrity)

**File:** `Tests/ActivityLoggerTests.cs`

| Test Case | What to Test | Why Important |
|-----------|--------------|---------------|
| `Append_ValidSample_WritesCsvLine()` | Sample logged to CSV | Core functionality |
| `Append_ShortDuration_Ignored()` | < 1s samples not logged | Noise filtering |
| `Append_EscapesCsvSpecialChars()` | Quotes/commas escaped | CSV format integrity |
| `Append_UpdatesTotalsToday()` | Daily totals incremented | Stats accuracy |
| `LogSessionStart_WritesMarker()` | `SESSION_START` marker written | Session tracking |
| `LogBreakStart_WritesMarker()` | `BREAK_START` marker written | Break tracking |
| `LogSessionEndAndDailyTotal_WritesTotalLine()` | `#TOTAL_ACTIVE_SEC` written | Daily summary |
| `RotateIfNeeded_DateChanged_CreatesNewFile()` | New CSV at midnight | Daily rotation |
| `RotateIfNeeded_DateChanged_ClosesOldSession()` | Old session ended before rotation | Clean state |
| `RotateIfNeeded_DateChanged_ResetsTotals()` | Daily totals reset | Fresh start |

**Mock Dependencies:** File system (use temporary test directories)

---

### Priority 4: ActivityMonitor (Integration)

**File:** `Tests/ActivityMonitorTests.cs`

| Test Case | What to Test | Why Important |
|-----------|--------------|---------------|
| `Start_StartsTimer()` | Timer starts when `Start()` called | Lifecycle |
| `Stop_StopsTimer()` | Timer stops when `Stop()` called | Lifecycle |
| `ProcessTick_Paused_SkipsProcessing()` | No processing when paused | Pause behavior |
| `ProcessTick_Locked_ClosesCurrentSample()` | Sample closed on lock | Window tracking |
| `ProcessTick_IdleTimeout_TriggersStateChange()` | State machine called on idle | Integration |
| `ProcessTick_ActiveState_TracksWindowChanges()` | Window changes logged | Window tracking |
| `ProcessTick_ActiveState_ChecksReminders()` | Reminders checked each tick | Reminder flow |
| `SetPaused_ClosesCurrentSample()` | Sample closed on pause | Clean pause |
| `SetPaused_FiresPauseStateChangedEvent()` | Event fires on pause toggle | UI notification |
| `StateChanged_PropagatedFromStateMachine()` | State machine events bubbled up | Event propagation |
| `ReminderDue_PropagatedFromScheduler()` | Scheduler events bubbled up | Event propagation |
| `Dispose_StopsTimerAndCleansUp()` | Resources cleaned on dispose | Memory management |

**Mock Dependencies:** `IdleTracker`, `WindowTracker`, `DoNotDisturbService`, `ActivityLogger`

---

### Priority 5: ServiceLogger (Infrastructure)

**File:** `Tests/ServiceLoggerTests.cs`

| Test Case | What to Test | Why Important |
|-----------|--------------|---------------|
| `Log_CreatesLogFile()` | File created on first log | File creation |
| `Log_AppendsToExistingFile()` | Multiple logs to same file | Append mode |
| `Log_ThreadSafe()` | Concurrent logs don't corrupt file | Thread safety |
| `Log_FormatsCorrectly()` | Timestamp, level, category, message | Format consistency |
| `Info_Warning_Error_Debug_LogCorrectLevel()` | Helper methods log correct level | Convenience methods |

**Mock Dependencies:** File system (use temporary test directories)

---

## TDD Workflow

### When Adding a New Feature:

1. **Write Test First**
   ```csharp
   [Fact]
   public void FeatureName_Scenario_ExpectedBehavior()
   {
       // Arrange
       var sut = new SessionStateMachine(mockLogger);
       
       // Act
       var result = sut.ProcessTick(idleSeconds: 0, isLocked: false);
       
       // Assert
       Assert.Equal(AppState.Active, sut.CurrentState);
       Assert.True(result.ShouldContinue);
   }
   ```

2. **Run Test - Watch it Fail (Red)**
   ```bash
   dotnet test
   ```

3. **Implement Minimum Code to Pass (Green)**
   ```csharp
   public StateTransitionResult ProcessTick(int idleSeconds, bool isLocked)
   {
       // Implementation
   }
   ```

4. **Refactor While Keeping Tests Green**
   - Extract methods
   - Improve naming
   - Optimize

5. **Commit with Test**
   ```bash
   git add Tests/SessionStateMachineTests.cs Services/SessionStateMachine.cs
   git commit -m "feat: add feature X with tests"
   ```

---

## Testing Scenarios

### Manual Testing Checklist

1. **Basic Flow:**
   - Start app → See tray icon
   - Work for 55 min → Get reminder
   - Go idle 10 min → Timer resets
   - Work 55 min → Get new reminder ✓

2. **Edge Cases:**
   - Lock computer → Should log break
   - Unlock → Timer should reset ✓
   - Toggle pause → Logging stops, no reminders
   - Quick idle (< 10 min) → Timer should NOT reset ✓

3. **Active Work Detection:**
   - Open Teams, go idle 10 min → Should NOT break
   - Open Notepad, go idle 10 min → Should break

4. **DND Mode:**
   - Enter fullscreen → Reminders muted
   - Start PowerPoint slideshow → Reminders muted
   - Screen share in Teams → Reminders muted

## Future Enhancements

### Suggested Improvements

1. **Settings UI** - Replace hardcoded constants with configurable settings
2. **Statistics Dashboard** - Rich visualization of activity patterns
3. **Focus Sessions** - Pomodoro-style work intervals
4. **Break Activities** - Suggest exercises, eye rest, hydration
5. **Weekly Reports** - Email summary of activity patterns
6. **Multi-Monitor Awareness** - Detect which monitor is active
7. **Sync to Cloud** - Optional backup of activity logs

### Architectural Considerations

- State machine is already testable - add unit tests
- Event-driven design makes it easy to add new features
- Consider moving to MAUI for cross-platform support
- Could extract core logic to class library, keep UI separate

---

**Last Updated:** January 3, 2026  
**Refactored:** Extracted from 789-line monolithic TrayApp to event-driven architecture
