# Setup Windows Task Scheduler for Daily Reports
# Run this script as Administrator to create the scheduled task

$TaskName = "StandUpTracker-DailyReport"
$ScriptPath = Join-Path $PSScriptRoot "report_generator.py"
$PythonExe = (Get-Command pythonw.exe -ErrorAction SilentlyContinue).Source

if (-not $PythonExe) {
    $PythonExe = (Get-Command python.exe -ErrorAction SilentlyContinue).Source
}

if (-not $PythonExe) {
    Write-Host "ERROR: Python not found. Please install Python 3.8+ first." -ForegroundColor Red
    exit 1
}

Write-Host "Setting up Windows Task Scheduler..." -ForegroundColor Green
Write-Host "  Task Name: $TaskName"
Write-Host "  Python: $PythonExe"
Write-Host "  Script: $ScriptPath"
Write-Host ""

# Define the action
$Action = New-ScheduledTaskAction -Execute $PythonExe -Argument "`"$ScriptPath`" --no-open" -WorkingDirectory $PSScriptRoot

# Define the trigger (daily at 11:59 PM)
$Trigger = New-ScheduledTaskTrigger -Daily -At "23:59"

# Define settings
$Settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable

# Register the task
try {
    Register-ScheduledTask -TaskName $TaskName -Action $Action -Trigger $Trigger -Settings $Settings -Description "Generate daily Work/Break report for StandUpTracker" -Force
    
    Write-Host "✓ Task created successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "The task will run daily at 11:59 PM to generate your work/break report." -ForegroundColor Cyan
    Write-Host "You can customize the schedule in Task Scheduler (taskschd.msc)." -ForegroundColor Cyan
    Write-Host ""
    Write-Host "To test now, run:" -ForegroundColor Yellow
    Write-Host "  Start-ScheduledTask -TaskName '$TaskName'" -ForegroundColor Yellow
}
catch {
    Write-Host "ERROR: Failed to create scheduled task." -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}
