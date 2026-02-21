@echo off
REM Daily Work/Break Report Generator for Windows Task Scheduler
REM Configured to generate YESTERDAY's report (intended for 9:00 AM schedule)

REM Change to script directory
cd /d "%~dp0"

REM Run Python report generator for previous day (silently)
pythonw.exe report_generator.py --yesterday --no-open

REM Optional: Log execution
echo %DATE% %TIME% - Report generated >> scheduler.log
