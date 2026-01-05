@echo off
REM Daily Work/Break Report Generator for Windows Task Scheduler
REM Schedule this script to run daily at your preferred time

REM Change to script directory
cd /d "%~dp0"

REM Run Python report generator (silently)
pythonw.exe report_generator.py --no-open

REM Optional: Log execution
echo %DATE% %TIME% - Report generated >> scheduler.log
