# StandUpTracker Report Generator

Beautiful Work/Break reports for your StandUpTracker activity logs.

## Features

- **Daily Reports**: Generate reports for today, yesterday, or any specific date
- **Beautiful UI**: Dark-themed, modern interface matching your screenshot
- **Timeline Visualization**: Visual timeline showing work (green) and break (orange) periods
- **Summary Statistics**: Start/end times, total work time, total break time
- **Multiple Launch Methods**: 
  - Command line (for automation/Task Scheduler)
  - System tray context menu
- **Auto-open**: Automatically opens report in your default browser

## Installation

```bash
cd ReportGenerator
pip install -r requirements.txt
```

## Usage

### Command Line

```bash
# Generate today's report
python report_generator.py

# Generate yesterday's report
python report_generator.py --yesterday

# Generate report for specific date
python report_generator.py --date 2026-01-04

# Save to custom location
python report_generator.py --output "C:\Reports\my_report.html"

# Generate without opening browser
python report_generator.py --no-open
```

### From System Tray

Right-click the StandUpTracker tray icon:
- **View Today's Report** - Opens today's activity report
- **View Yesterday's Report** - Opens yesterday's activity report

### Windows Task Scheduler

To automatically generate daily reports:

1. Open **Task Scheduler** (Win + R → `taskschd.msc`)
2. Click **Create Basic Task**
3. Name: "Daily StandUp Report"
4. Trigger: Daily, 11:59 PM
5. Action: Start a program
   - Program: `pythonw.exe` (or full path: `C:\Python3X\pythonw.exe`)
   - Arguments: `"C:\epam\standupTracker\ReportGenerator\report_generator.py" --no-open`
   - Start in: `C:\epam\standupTracker\ReportGenerator`

> **Note**: Use `pythonw.exe` to run silently without a console window.

## Report Format

The report includes:

- **Start Time**: First recorded activity of the day (or after 6 AM for completed days)
- **End Time**: Last recorded activity of the day (capped at 23:59:59 for dates with next-day overlap)
- **Total Work Time**: Sum of all active work sessions
- **Total Break Time**: Time between first and last activity, minus work time
- **Timeline**: 24-hour visual timeline with hourly markers

### Timeline Colors

- 🟢 **Green**: Active work sessions
- 🟠 **Orange**: Break periods

### Note on Day Boundaries

CSV logs can contain entries from the previous day (before midnight) and next day (after midnight). The report:
- Skips entries before 6 AM at the start of the log (previous day)
- Caps entries after midnight at 23:59:59 (next day overflow)
- This ensures each report represents a complete 24-hour period

## Output Location

Reports are saved to:
```
%LOCALAPPDATA%\StandUpTracker\reports\report_YYYY-MM-DD.html
```

Example: `C:\Users\YourName\AppData\Local\StandUpTracker\reports\report_2026-01-05.html`

## Data Source

The report generator reads CSV logs from:
```
%LOCALAPPDATA%\StandUpTracker\logs\YYYY-MM-DD.csv
```

These logs are automatically created by the StandUpTracker application.

## Troubleshooting

### "Logs folder not found"
- Make sure StandUpTracker has been running and creating logs
- Check that the logs folder exists: `%LOCALAPPDATA%\StandUpTracker\logs`

### "No log file found for YYYY-MM-DD"
- The report will show an error message if no data exists for the requested date
- Make sure StandUpTracker was running on that date

### Report doesn't open in browser
- Use `--no-open` flag and open the HTML file manually
- Check your default browser settings

## Technical Details

- **Language**: Python 3.8+
- **Dependencies**: pandas, jinja2 (optional, not used in current version)
- **Output Format**: Standalone HTML with embedded CSS/JS
- **Browser Compatibility**: Modern browsers (Chrome, Edge, Firefox)

## License

Part of the StandUpTracker project.
