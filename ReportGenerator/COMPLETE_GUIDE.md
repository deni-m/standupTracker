# StandUpTracker Report Generator - Complete Guide

## ✨ New Features Added

### 1. **Beautiful Work/Break Reports**
   - Modern dark-themed UI matching your screenshot
   - Visual 24-hour timeline with color-coded work/break periods
   - Summary statistics: Start time, End time, Total work time, Total break time
   - Responsive design that works in any browser

### 2. **Multiple Access Methods**
   - **System Tray Menu**: Right-click tray icon → "📊 Work/Break Reports"
     - View Today's Report
     - View Yesterday's Report
   - **Command Line**: For automation and custom dates
   - **Windows Task Scheduler**: Automated daily report generation

### 3. **Timeline Visualization**
   - 🟢 **Green bars**: Active work sessions
   - 🟠 **Orange bars**: Break periods
   - Hourly time markers (00:00 to 24:00)
   - Hover tooltips showing exact start/end times

---

## 🚀 Quick Start

### Option 1: From System Tray (Easiest)

1. Run StandUpTracker
2. Right-click the coffee cup icon in system tray
3. Click **📊 Work/Break Reports** → **View Today's Report**
4. Report opens automatically in your browser!

### Option 2: Command Line

```bash
# Today's report
cd ReportGenerator
python report_generator.py

# Yesterday's report
python report_generator.py --yesterday

# Specific date
python report_generator.py --date 2026-01-04
```

### Option 3: Automated Daily Reports

**Setup once, get reports every day:**

```powershell
# Run as Administrator
cd ReportGenerator
.\setup_scheduled_task.ps1
```

This creates a Windows Task that runs at 11:59 PM daily to generate your report silently.

---

## 📋 Installation

### Prerequisites
- Python 3.8 or later
- pip (Python package manager)

### Setup

```bash
cd c:\epam\standupTracker\ReportGenerator
pip install -r requirements.txt
```

**That's it!** Only requires `pandas` (for potential future CSV analysis).

---

## 🎨 Report Screenshot

The report displays:

```
┌─────────────────────────────────────────────────────────────────┐
│ Work / Break Report                                             │
├─────────────────────────────────────────────────────────────────┤
│  Start Time    End Time      Total Work Time   Total Break Time │
│  07:32:20      17:48:38      10:12:18          00:48:00         │
├─────────────────────────────────────────────────────────────────┤
│ [Timeline visualization with colored bars]                      │
│ 00 01 02 03 04 05 06 07 08 09 10 11 12 13 14 15 16 17 18 19... │
│       ░░░░░░░░░████████████████░░░░████████████████░░░░        │
│       (orange)    (green bars = work periods)    (orange)      │
└─────────────────────────────────────────────────────────────────┘
```

---

## 📂 File Locations

### Input (Activity Logs)
```
%LOCALAPPDATA%\StandUpTracker\logs\YYYY-MM-DD.csv
```
Example: `C:\Users\YourName\AppData\Local\StandUpTracker\logs\2026-01-05.csv`

### Output (HTML Reports)
```
%LOCALAPPDATA%\StandUpTracker\reports\report_YYYY-MM-DD.html
```
Example: `C:\Users\YourName\AppData\Local\StandUpTracker\reports\report_2026-01-05.html`

### Log Format (CSV)
```csv
start,end,duration_sec,process,title
07:32:20,07:32:25,5,chrome.exe,"Gmail - Google Chrome"
00:23:16,00:23:16,0,SESSION_START,""
00:37:36,00:37:36,0,BREAK_START,""
#TOTAL_ACTIVE_SEC,36789
```

---

## ⚙️ Windows Task Scheduler Setup (Detailed)

### Automatic Setup (Recommended)

```powershell
# Run as Administrator
cd c:\epam\standupTracker\ReportGenerator
.\setup_scheduled_task.ps1
```

### Manual Setup

1. Open **Task Scheduler** (Win + R → `taskschd.msc`)
2. Click **Create Basic Task**
3. **Name**: "StandUpTracker Daily Report"
4. **Trigger**: Daily at 11:59 PM
5. **Action**: Start a program
   - **Program**: `pythonw.exe` (or full path like `C:\Python312\pythonw.exe`)
   - **Arguments**: `"C:\epam\standupTracker\ReportGenerator\report_generator.py" --no-open`
   - **Start in**: `C:\epam\standupTracker\ReportGenerator`
6. Check **Run whether user is logged on or not**
7. Click **Finish**

**Test**: `Start-ScheduledTask -TaskName "StandUpTracker-DailyReport"`

---

## 🔧 Command Line Options

```
python report_generator.py [OPTIONS]

Options:
  --yesterday          Generate yesterday's report
  --date YYYY-MM-DD    Generate report for specific date
  --output PATH        Save to custom location (default: %LOCALAPPDATA%\StandUpTracker\reports\)
  --no-open            Don't open report in browser after generation
  -h, --help           Show help message
```

### Examples

```bash
# Today (auto-opens in browser)
python report_generator.py

# Yesterday (auto-opens)
python report_generator.py --yesterday

# Specific date (auto-opens)
python report_generator.py --date 2026-01-04

# Save to desktop without opening
python report_generator.py --output "C:\Users\YourName\Desktop\my_report.html" --no-open

# Silent mode for Task Scheduler
pythonw.exe report_generator.py --no-open
```

---

## 🛠️ Troubleshooting

### "Python not found" error in tray menu

**Solution 1**: Install Python from [python.org](https://www.python.org/downloads/)

**Solution 2**: Add Python to PATH:
```powershell
# Find Python
where.exe python

# Add to PATH (System Properties → Environment Variables)
C:\Python312
C:\Python312\Scripts
```

**Solution 3**: Run manually from command line:
```bash
cd c:\epam\standupTracker\ReportGenerator
python report_generator.py
```

### "Report generator script not found"

The app looks for the script at:
```
<exe directory>\..\..\..\..\..\ReportGenerator\report_generator.py
```

**Solution**: Ensure your folder structure is:
```
standupTracker/
├── StandUpTracker/         (main app)
│   └── bin/Debug/...
└── ReportGenerator/        (report tool)
    └── report_generator.py
```

### "No log file found for YYYY-MM-DD"

This means StandUpTracker wasn't running on that date, or logs were deleted.

**Check logs folder**:
```powershell
explorer "$env:LOCALAPPDATA\StandUpTracker\logs"
```

### Report shows "0:00:00" work time for today

This is expected behavior - the `#TOTAL_ACTIVE_SEC` line is only written at:
- End of day (midnight)
- App exit
- Session lock

**Solution**: The parser automatically calculates work time from `SESSION_START`/`BREAK_START` markers for ongoing days.

### Task Scheduler task not running

1. Open Task Scheduler → Find "StandUpTracker-DailyReport"
2. Right-click → **Run** (test manually)
3. Check **Last Run Result** (should be `0x0` for success)
4. Check **History** tab for detailed logs
5. Ensure Python path is correct in the action

---

## 📊 Technical Details

### How Work/Break Calculation Works

1. **Work Sessions**: Time between `SESSION_START` and `BREAK_START` markers
2. **Break Time**: Total time from first activity to last activity, minus work time
3. **Ongoing Day**: Uses current time as end time for active session

### CSV Markers
- `SESSION_START` - User became active (after 10+ min break)
- `BREAK_START` - User went idle (10+ min idle)
- `SESSION_END` - Explicit session end (day ended)
- `#TOTAL_ACTIVE_SEC` - End-of-day summary line

### Timeline Rendering
- Uses HTML5 Canvas-like absolute positioning
- Each period is calculated as `(start_minutes / 1440) * 100%` for position
- Width is `((end_minutes - start_minutes) / 1440) * 100%`
- 1440 = minutes in 24 hours

---

## 🎯 Future Enhancements

Potential improvements:
- [ ] Weekly/monthly summary reports
- [ ] Export to PDF
- [ ] Productivity insights (most productive hours)
- [ ] Break frequency analysis
- [ ] Application usage statistics
- [ ] Comparison charts (today vs. yesterday)
- [ ] Email report delivery

---

## 📝 License

Part of the StandUpTracker project. See main repository for license details.

---

## 🤝 Contributing

Found a bug or have a feature request?
1. Check the logs folder for errors
2. Run `python report_generator.py --no-open` and check console output
3. Report issues with log samples (anonymize sensitive window titles)

---

## ✅ Checklist: Is Everything Working?

- [ ] Python installed and in PATH
- [ ] `pip install -r requirements.txt` completed
- [ ] StandUpTracker running in system tray
- [ ] Can see "📊 Work/Break Reports" in tray menu
- [ ] Clicking "View Today's Report" opens browser with report
- [ ] Report shows correct start time and work duration
- [ ] Timeline visualization displays colored bars
- [ ] (Optional) Task Scheduler set up for daily reports

**All green?** You're all set! Enjoy your automated work/break reports! ☕📊

