"""
StandUpTracker Report Generator

Generates beautiful Work/Break reports from activity logs.
Can be run standalone or from the system tray application.

Usage:
    python report_generator.py              # Generate today's report
    python report_generator.py --yesterday  # Generate yesterday's report
    python report_generator.py --date 2026-01-04  # Specific date
"""

import os
import sys
import csv
import argparse
import webbrowser
from datetime import datetime, timedelta, time
from pathlib import Path
from typing import List, Tuple, Optional
import json


class ActivityReport:
    """Parses CSV logs and generates work/break reports"""
    
    def __init__(self, logs_folder: Path):
        self.logs_folder = logs_folder
    
    def get_log_path(self, date: datetime) -> Path:
        """Get the CSV log file path for a specific date"""
        return self.logs_folder / f"{date.strftime('%Y-%m-%d')}.csv"
    
    def parse_log(self, date: datetime) -> dict:
        """Parse a daily log file and extract work/break periods"""
        log_path = self.get_log_path(date)
        
        if not log_path.exists():
            return {
                'date': date,
                'exists': False,
                'start_time': None,
                'end_time': None,
                'total_work_seconds': 0,
                'total_break_seconds': 0,
                'periods': [],
                'error': f"No log file found for {date.strftime('%Y-%m-%d')}"
            }
        
        periods = []  # List of (start_time, end_time, type) where type is 'work' or 'break'
        first_activity = None
        last_activity = None
        total_work_seconds = 0
        app_stats = {}  # Dictionary to track time per application: {app_name: {total: seconds, windows: {title: seconds}}}
        
        current_session_start = None
        in_work_session = False
        session_started = False  # Track if we've seen a SESSION_START for today
        current_process_date = date
        
        with open(log_path, 'r', encoding='utf-8-sig') as f:  # utf-8-sig handles BOM automatically
            reader = csv.DictReader(f)
            
            for row in reader:
                if row is None or not row:
                    continue
                    
                start_str = (row.get('start') or '').strip()
                process = (row.get('process') or '').strip()
                
                if not start_str:
                    continue
                
                # Parse time
                try:
                    parsed_time = datetime.strptime(start_str, '%H:%M:%S').time()
                except ValueError:
                    continue
                
                # Handle midnight crossover (if time goes backwards, we moved to next day)
                # This assumes log entries are chronological
                if last_activity:
                    last_time = last_activity.time()
                    if parsed_time < last_time:
                         # We crossed midnight, increment the base date for this and subsequent entries
                         # but only if we haven't already moved to the next day for this specific crossover
                         # (Logic: maintain current_process_date state)
                         pass

                # We need to track the date for the current entry.
                # Since we process sequentially, we can detect date changes.
                # Initialize current_process_date at start of function.
                
                # REWRITE STRATEGY:
                # We can't rely just on last_activity because last_activity is a datetime.
                # we need to track the "current day offset" from the start date.
                
                # Let's use a simpler approach:
                # 1. current_entry_dt = datetime.combine(current_process_date, parsed_time)
                # 2. if current_entry_dt < last_activity: 
                #       current_process_date += timedelta(days=1)
                #       current_entry_dt = datetime.combine(current_process_date, parsed_time)
                
                current_entry_dt = datetime.combine(current_process_date, parsed_time)
                
                if last_activity and current_entry_dt < last_activity:
                    # Time went backwards. Check if it's a significant jump (indicating next day)
                    # or just minor jitter due to race conditions in logging (e.g. threads writing out of order)
                    time_diff = last_activity - current_entry_dt
                    if time_diff > timedelta(hours=12):
                        # Significant jump back in time -> Midnight crossover -> Next Day
                        current_process_date += timedelta(days=1)
                        current_entry_dt = datetime.combine(current_process_date, parsed_time)
                    else:
                        # Minor jitter/out-of-order write. Keep timeline monotonic to avoid
                        # negative durations when closing periods later.
                        current_entry_dt = last_activity

                # Skip all entries until we see the first SESSION_START marker
                # This handles midnight crossover - entries before SESSION_START are from previous day
                if not session_started and process != 'SESSION_START':
                    continue
                
                # Handle special markers
                if process == 'SESSION_START':
                    session_started = True  # Mark that we've started tracking today's session
                    # Start of work session
                    session_dt = current_entry_dt
                    
                    if current_session_start:
                        if in_work_session:
                            # Close previous work period (shouldn't happen, but be safe)
                            periods.append((current_session_start, session_dt, 'work'))
                        else:
                            # Close previous break period
                            periods.append((current_session_start, session_dt, 'break'))
                    
                    current_session_start = session_dt
                    in_work_session = True
                    
                    if first_activity is None:
                        first_activity = session_dt
                    last_activity = session_dt
                    
                elif process == 'BREAK_START':
                    # End of work session, start of break
                    break_dt = current_entry_dt
                    
                    if current_session_start and in_work_session:
                        periods.append((current_session_start, break_dt, 'work'))
                        current_session_start = break_dt
                        in_work_session = False
                    else:
                        # Break without session start (implicit break)
                        current_session_start = break_dt
                        in_work_session = False
                    
                    last_activity = break_dt
                    
                elif process == 'SESSION_END':
                    # Explicit end of session
                    end_dt = current_entry_dt
                    
                    if current_session_start:
                        if in_work_session:
                            periods.append((current_session_start, end_dt, 'work'))
                        # No need to track break periods explicitly
                    
                    current_session_start = None
                    in_work_session = False
                    last_activity = end_dt
                    
                elif start_str.startswith('#TOTAL_ACTIVE_SEC') or process.startswith('#TOTAL_ACTIVE_SEC'):
                    # End of day summary
                    try:
                        end_value = (row.get('end') or '0').strip()
                        total_work_seconds = int(end_value)
                    except (ValueError, AttributeError):
                        pass
                    
                else:
                    # Regular activity entry
                    try:
                        start_dt = current_entry_dt
                        
                        duration_str = (row.get('duration_sec') or '0').strip()
                        duration = int(duration_str)
                        
                        # Track application statistics with window titles (only if session has started)
                        if session_started and duration > 0:
                            app_name = process.replace('.exe', '')  # Remove .exe extension
                            title = (row.get('title') or '').strip()
                            
                            if app_name not in app_stats:
                                app_stats[app_name] = {'total': 0, 'windows': {}}
                            
                            app_stats[app_name]['total'] += duration
                            
                            if title:
                                if title not in app_stats[app_name]['windows']:
                                    app_stats[app_name]['windows'][title] = 0
                                app_stats[app_name]['windows'][title] += duration
                        
                        if first_activity is None:
                            first_activity = start_dt
                        if last_activity is None or start_dt > last_activity:
                            last_activity = start_dt
                        
                    except (ValueError, TypeError, AttributeError):
                        continue
        
        # Close any open session at end of file
        if current_session_start:
            end_of_day = last_activity or datetime.combine(date, time(23, 59, 59))
            # For ongoing day, use current time as end
            if date.date() == datetime.now().date():
                end_of_day = datetime.now()
            
            if in_work_session:
                periods.append((current_session_start, end_of_day, 'work'))
            else:
                # Still in break at end of day - this is normal
                periods.append((current_session_start, end_of_day, 'break'))
        
        # Drop invalid/negative periods that may appear from out-of-order writes
        periods = [
            (start, end, period_type)
            for start, end, period_type in periods
            if end > start
        ]

        # If no explicit total from #TOTAL_ACTIVE_SEC (ongoing day), calculate from periods
        if total_work_seconds == 0 and periods:
            for start, end, period_type in periods:
                if period_type == 'work':
                    total_work_seconds += int((end - start).total_seconds())
        
        # Calculate total break time based on gaps between work sessions (excluding huge gaps like night)
        # 1. Identify all work intervals
        work_intervals = sorted([
            (p[0], p[1]) for p in periods if p[2] == 'work'
        ], key=lambda x: x[0])

        total_break_seconds = 0
        
        if work_intervals:
            # Update first/last activity based on actual work sessions
            first_activity = work_intervals[0][0]
            last_activity = work_intervals[-1][1]

            # Calculate gaps between consecutive work sessions
            for i in range(len(work_intervals) - 1):
                end_current = work_intervals[i][1]
                start_next = work_intervals[i+1][0]
                
                gap = (start_next - end_current).total_seconds()
                
                # If gap is huge (e.g. > 4 hours), treat as "Off work" (Night), don't count as break
                if 0 < gap < (4 * 3600):
                    total_break_seconds += int(gap)
                else:
                    # Treat > 4h gap as non-working time, not break
                    pass
        else:
             # Fallback if no work periods found (should be rare)
             if first_activity and last_activity:
                 # For ongoing day, use current time as last_activity
                 if date.date() == datetime.now().date():
                     last_activity = max(last_activity, datetime.now())
                 
                 total_day_seconds = (last_activity - first_activity).total_seconds()
                 total_break_seconds = max(0, int(total_day_seconds - total_work_seconds))

        # Reconcile app totals with total work time.
        # App stats are based on flushed window samples, while total work time may include
        # still-open active work that has not been flushed to CSV yet.
        total_app_seconds = sum(app_data['total'] for app_data in app_stats.values())
        unattributed_seconds = max(0, total_work_seconds - total_app_seconds)
        if unattributed_seconds > 0:
            unattributed_key = 'Unattributed active time'
            if unattributed_key not in app_stats:
                app_stats[unattributed_key] = {'total': 0, 'windows': {}}
            app_stats[unattributed_key]['total'] += unattributed_seconds

            # Use a synthetic window title to make the source of this time explicit in UI.
            note_title = 'Open active window (not flushed to log yet)'
            if note_title not in app_stats[unattributed_key]['windows']:
                app_stats[unattributed_key]['windows'][note_title] = 0
            app_stats[unattributed_key]['windows'][note_title] += unattributed_seconds
        
        return {
            'date': date,
            'exists': True,
            'start_time': first_activity,
            'end_time': last_activity,
            'total_work_seconds': total_work_seconds,
            'total_break_seconds': total_break_seconds,
            'periods': periods,
            'app_stats': app_stats,
            'error': None
        }
    
    def format_duration(self, seconds: int) -> str:
        """Format seconds as HH:MM:SS"""
        hours = seconds // 3600
        minutes = (seconds % 3600) // 60
        secs = seconds % 60
        return f"{hours:02d}:{minutes:02d}:{secs:02d}"
    
    def parse_week(self, start_date: datetime) -> dict:
        """Parse logs for a week (7 days starting from start_date)"""
        daily_reports = []
        weekly_app_stats = {}
        total_work_seconds = 0
        total_break_seconds = 0
        
        for day_offset in range(7):
            current_date = start_date + timedelta(days=day_offset)
            day_data = self.parse_log(current_date)
            daily_reports.append(day_data)
            
            if day_data['exists'] and not day_data.get('error'):
                total_work_seconds += day_data['total_work_seconds']
                total_break_seconds += day_data['total_break_seconds']
                
                # Aggregate app statistics
                if day_data.get('app_stats'):
                    for app_name, app_data in day_data['app_stats'].items():
                        if app_name not in weekly_app_stats:
                            weekly_app_stats[app_name] = {'total': 0, 'windows': {}}
                        
                        weekly_app_stats[app_name]['total'] += app_data['total']
                        
                        # Aggregate window titles
                        for title, seconds in app_data['windows'].items():
                            if title not in weekly_app_stats[app_name]['windows']:
                                weekly_app_stats[app_name]['windows'][title] = 0
                            weekly_app_stats[app_name]['windows'][title] += seconds
        
        return {
            'start_date': start_date,
            'end_date': start_date + timedelta(days=6),
            'daily_reports': daily_reports,
            'total_work_seconds': total_work_seconds,
            'total_break_seconds': total_break_seconds,
            'app_stats': weekly_app_stats
        }
    
    def generate_html_report(self, report_data: dict, output_path: Path):
        """Generate an HTML report matching the screenshot design"""
        
        # Prepare timeline data (logical day: 03:00 -> 03:00 next day)
        timeline_data = []
        timeline_start_hour = 3
        timeline_window_start = datetime.combine(report_data['date'].date(), time(hour=timeline_start_hour))
        timeline_window_end = timeline_window_start + timedelta(days=1)

        if report_data['periods']:
            for start, end, period_type in report_data['periods']:
                # Keep only the portion that belongs to the selected calendar day.
                clipped_start = max(start, timeline_window_start)
                clipped_end = min(end, timeline_window_end)

                if clipped_end <= clipped_start:
                    continue

                timeline_data.append({
                    'start': clipped_start.strftime('%H:%M:%S'),
                    'end': clipped_end.strftime('%H:%M:%S'),
                    'start_minutes': (clipped_start - timeline_window_start).total_seconds() / 60,
                    'end_minutes': (clipped_end - timeline_window_start).total_seconds() / 60,
                    'duration': self.format_duration(int((clipped_end - clipped_start).total_seconds())),
                    'type': period_type
                })
        
        # Prepare app statistics data
        app_stats_list = []
        if report_data.get('app_stats'):
            # Sort by time descending
            sorted_apps = sorted(report_data['app_stats'].items(), key=lambda x: x[1]['total'], reverse=True)
            total_app_time = sum(app['total'] for app in report_data['app_stats'].values())
            
            for app_name, app_data in sorted_apps:
                seconds = app_data['total']
                if seconds > 0:  # Only show apps with recorded time
                    percentage = (seconds / total_app_time * 100) if total_app_time > 0 else 0
                    
                    # Sort window titles by time
                    windows = []
                    for title, title_seconds in sorted(app_data['windows'].items(), key=lambda x: x[1], reverse=True):
                        title_percentage = (title_seconds / seconds * 100) if seconds > 0 else 0
                        windows.append({
                            'title': title,
                            'seconds': title_seconds,
                            'formatted_time': self.format_duration(title_seconds),
                            'percentage': title_percentage
                        })
                    
                    app_stats_list.append({
                        'name': app_name,
                        'seconds': seconds,
                        'formatted_time': self.format_duration(seconds),
                        'percentage': percentage,
                        'windows': windows
                    })
        
        # Generate HTML
        html_content = f"""<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Work / Break Report - {report_data['date'].strftime('%Y-%m-%d')}</title>
    <style>
        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }}
        
        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            background: linear-gradient(135deg, #1e1e2e 0%, #2a2a3e 100%);
            color: #e0e0e0;
            padding: 20px;
            min-height: 100vh;
        }}
        
        .container {{
            max-width: 1400px;
            margin: 0 auto;
        }}
        
        h1 {{
            font-size: 28px;
            margin-bottom: 30px;
            color: #ffffff;
            font-weight: 400;
        }}
        
        .stats-grid {{
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
            gap: 20px;
            margin-bottom: 40px;
        }}
        
        .stat-card {{
            background: rgba(40, 40, 60, 0.6);
            border: 1px solid rgba(100, 100, 120, 0.3);
            border-radius: 8px;
            padding: 20px;
            backdrop-filter: blur(10px);
        }}
        
        .stat-label {{
            font-size: 13px;
            color: #a0a0b0;
            margin-bottom: 8px;
            text-transform: uppercase;
            letter-spacing: 0.5px;
        }}
        
        .stat-value {{
            font-size: 32px;
            color: #ffffff;
            font-weight: 300;
            font-family: 'Consolas', 'Courier New', monospace;
        }}
        
        .timeline-container {{
            background: rgba(40, 40, 60, 0.6);
            border: 1px solid rgba(100, 100, 120, 0.3);
            border-radius: 8px;
            padding: 30px;
            backdrop-filter: blur(10px);
        }}
        
        .timeline-wrapper {{
            position: relative;
            height: 60px;
            margin-top: 20px;
        }}
        
        .timeline-bar {{
            position: relative;
            width: 100%;
            height: 40px;
            background: rgba(30, 30, 40, 0.8);
            border-radius: 4px;
            overflow: visible;
        }}
        
        .timeline-period {{
            position: absolute;
            height: 100%;
            top: 0;
            border-radius: 2px;
            transition: all 0.3s ease;
        }}
        
        .timeline-period.work {{
            background: linear-gradient(90deg, #4ade80 0%, #22c55e 100%);
            box-shadow: 0 2px 8px rgba(34, 197, 94, 0.3);
        }}
        
        .timeline-period.break {{
            background: linear-gradient(90deg, #fbbf24 0%, #f59e0b 100%);
            box-shadow: 0 2px 8px rgba(251, 191, 36, 0.3);
        }}
        
        .timeline-period:hover {{
            transform: scaleY(1.1);
            z-index: 10;
        }}
        
        .timeline-labels {{
            position: relative;
            width: 100%;
            height: 16px;
            margin-top: 8px;
            font-size: 11px;
            color: #808090;
            font-family: 'Consolas', monospace;
        }}
        
        .timeline-label {{
            position: absolute;
            transform: translateX(-50%);
            white-space: nowrap;
        }}

        .timeline-label.start {{
            transform: none;
        }}

        .timeline-label.end {{
            transform: translateX(-100%);
        }}
        
        .note {{
            margin-top: 20px;
            font-size: 12px;
            color: #808090;
            font-style: italic;
        }}
        
        .error {{
            background: rgba(220, 38, 38, 0.1);
            border: 1px solid rgba(220, 38, 38, 0.3);
            color: #fca5a5;
            padding: 20px;
            border-radius: 8px;
            margin-top: 20px;
        }}
        
        .legend {{
            display: flex;
            gap: 30px;
            margin-top: 20px;
            font-size: 13px;
        }}
        
        .legend-item {{
            display: flex;
            align-items: center;
            gap: 8px;
        }}
        
        .legend-color {{
            width: 20px;
            height: 12px;
            border-radius: 2px;
        }}
        
        .legend-color.work {{
            background: linear-gradient(90deg, #4ade80 0%, #22c55e 100%);
        }}
        
        .legend-color.break {{
            background: linear-gradient(90deg, #fbbf24 0%, #f59e0b 100%);
        }}
        
        .app-stats {{
            margin-top: 40px;
        }}
        
        .app-stats h2 {{
            font-size: 20px;
            color: #ffffff;
            font-weight: 400;
            margin-bottom: 20px;
        }}
        
        .app-list {{
            display: grid;
            gap: 8px;
            overflow: hidden;
        }}
        
        .app-item {{
            display: flex;
            align-items: center;
            background: rgba(50, 50, 70, 0.4);
            border-radius: 6px;
            padding: 12px 16px;
            transition: background 0.2s ease;
            cursor: pointer;
            user-select: none;
            min-width: 0;
            max-width: 100%;
            overflow: hidden;
        }}
        
        .app-item:hover {{
            background: rgba(60, 60, 80, 0.6);
        }}
        
        .app-item.expanded {{
            border-bottom-left-radius: 0;
            border-bottom-right-radius: 0;
        }}
        
        .expand-icon {{
            margin-right: 12px;
            color: #808090;
            transition: transform 0.2s ease;
            font-size: 12px;
            width: 12px;
            flex-shrink: 0;
        }}
        
        .app-item.expanded .expand-icon {{
            transform: rotate(90deg);
        }}
        
        .app-name {{
            flex: 1;
            color: #e0e0e0;
            font-size: 14px;
            overflow: hidden;
            text-overflow: ellipsis;
            white-space: nowrap;
            min-width: 0;
            max-width: 600px;
            flex-basis: 0;
        }}
        
        .app-time {{
            color: #a0a0b0;
            font-family: 'Consolas', monospace;
            font-size: 13px;
            margin-right: 16px;
            flex-shrink: 0;
            min-width: 70px;
            width: 70px;
        }}
        
        .app-bar {{
            width: 200px;
            height: 20px;
            background: rgba(30, 30, 40, 0.8);
            border-radius: 10px;
            overflow: hidden;
            position: relative;
            flex-shrink: 0;
        }}
        
        .app-bar-fill {{
            height: 100%;
            background: linear-gradient(90deg, #6366f1 0%, #8b5cf6 100%);
            border-radius: 10px;
            transition: width 0.3s ease;
        }}
        
        .app-percentage {{
            color: #808090;
            font-size: 12px;
            margin-left: 12px;
            min-width: 50px;
            width: 50px;
            text-align: right;
            flex-shrink: 0;
        }}
        
        .window-details {{
            display: none;
            background: rgba(40, 40, 60, 0.6);
            border-bottom-left-radius: 6px;
            border-bottom-right-radius: 6px;
            border-top: 1px solid rgba(100, 100, 120, 0.2);
            padding: 8px 16px 12px 16px;
            margin-top: -6px;
            overflow: hidden;
        }}
        
        .window-details.visible {{
            display: block;
        }}
        
        .window-item {{
            display: flex;
            align-items: center;
            padding: 8px 12px;
            margin-top: 4px;
            background: rgba(30, 30, 50, 0.4);
            border-radius: 4px;
            min-width: 0;
            max-width: 100%;
            overflow: hidden;
        }}
        
        .window-title {{
            flex: 1;
            color: #b0b0c0;
            font-size: 13px;
            margin-left: 36px;
            overflow: hidden;
            text-overflow: ellipsis;
            white-space: nowrap;
            min-width: 0;
            max-width: 500px;
            flex-basis: 0;
        }}
        
        .window-time {{
            color: #808090;
            font-family: 'Consolas', monospace;
            font-size: 12px;
            margin-right: 12px;
            flex-shrink: 0;
            min-width: 65px;
            width: 65px;
        }}
        
        .window-bar {{
            width: 150px;
            height: 16px;
            background: rgba(20, 20, 30, 0.8);
            border-radius: 8px;
            overflow: hidden;
            flex-shrink: 0;
        }}
        
        .window-bar-fill {{
            height: 100%;
            background: linear-gradient(90deg, #06b6d4 0%, #0891b2 100%);
            border-radius: 8px;
        }}
        
        .window-percentage {{
            color: #707080;
            font-size: 11px;
            margin-left: 10px;
            min-width: 45px;
            width: 45px;
            text-align: right;
            flex-shrink: 0;
        }}
        
        @media print {{
            body {{
                background: white;
                color: black;
            }}
            .stat-card, .timeline-container {{
                background: white;
                border: 1px solid #ccc;
            }}
        }}
    </style>
</head>
<body>
    <div class="container">
        <h1>Work / Break Report</h1>
        
        {"<div class='error'>" + report_data.get('error', '') + "</div>" if report_data.get('error') else ""}
        
        {"" if report_data.get('error') else f'''
        <div class="stats-grid">
            <div class="stat-card">
                <div class="stat-label">Start Time</div>
                <div class="stat-value">{report_data['start_time'].strftime('%H:%M:%S') if report_data['start_time'] else '—'}</div>
            </div>
            
            <div class="stat-card">
                <div class="stat-label">End Time</div>
                <div class="stat-value">{report_data['end_time'].strftime('%H:%M:%S') if report_data['end_time'] else '—'}</div>
            </div>
            
            <div class="stat-card">
                <div class="stat-label">Total Work Time</div>
                <div class="stat-value">{self.format_duration(report_data['total_work_seconds'])}</div>
            </div>
            
            <div class="stat-card">
                <div class="stat-label">Total Break Time</div>
                <div class="stat-value">{self.format_duration(report_data['total_break_seconds'])}</div>
            </div>
        </div>
        
        <div class="timeline-container">
            <div class="timeline-wrapper">
                <div class="timeline-bar">
                    {"".join([
                        f'<div class="timeline-period {period["type"]}" '
                        f'style="left: {(period["start_minutes"] / 1440) * 100:.2f}%; '
                        f'width: {((period["end_minutes"] - period["start_minutes"]) / 1440) * 100:.2f}%;" '
                        f'title="{period["type"].title()}: {period["start"]} - {period["end"]} ({period["duration"]})"></div>'
                        for period in timeline_data
                    ])}
                </div>
            </div>
            
            <div class="timeline-labels">
                {"".join([
                    (
                        f'<div class="timeline-label start" style="left: {(h / 24) * 100:.4f}%;">{((timeline_start_hour + h) % 24):02d}:00</div>'
                        if h == 0 else
                        f'<div class="timeline-label end" style="left: {(h / 24) * 100:.4f}%;">{((timeline_start_hour + h) % 24):02d}:00</div>'
                        if h == 24 else
                        f'<div class="timeline-label" style="left: {(h / 24) * 100:.4f}%;">{((timeline_start_hour + h) % 24):02d}:00</div>'
                    )
                    for h in range(0, 25, 1)
                ])}
            </div>
            
            <div class="legend">
                <div class="legend-item">
                    <div class="legend-color work"></div>
                    <span>Work Time</span>
                </div>
                <div class="legend-item">
                    <div class="legend-color break"></div>
                    <span>Break Time</span>
                </div>
            </div>
            
            <div class="note">
                Report generated: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}
                {'• Capped at now: ' + datetime.now().strftime('%H:%M:%S') + ' • Breaks after the last break start are excluded (day ended).' if report_data['end_time'] and report_data['end_time'].date() == datetime.now().date() else ''}
            </div>
        </div>
        
        {"" if not app_stats_list else f'''
        <div class="timeline-container app-stats">
            <h2>📊 Application Usage Statistics</h2>
            <div class="app-list">
                {"".join([
                    f'''<div>
                    <div class="app-item" onclick="toggleAppDetails(this)">
                        <span class="expand-icon">▶</span>
                        <div class="app-name" title="{app["name"]}">{app["name"]}</div>
                        <div class="app-time">{app["formatted_time"]}</div>
                        <div class="app-bar">
                            <div class="app-bar-fill" style="width: {app["percentage"]:.1f}%;"></div>
                        </div>
                        <div class="app-percentage">{app["percentage"]:.1f}%</div>
                    </div>
                    {"" if not app["windows"] else f'''<div class="window-details">
                        {"".join([
                            f'''<div class="window-item">
                                <div class="window-title" title="{window["title"]}">{window["title"]}</div>
                                <div class="window-time">{window["formatted_time"]}</div>
                                <div class="window-bar">
                                    <div class="window-bar-fill" style="width: {window["percentage"]:.1f}%;"></div>
                                </div>
                                <div class="window-percentage">{window["percentage"]:.1f}%</div>
                            </div>'''
                            for window in app["windows"]
                        ])}
                    </div>'''}
                </div>'''
                    for app in app_stats_list
                ])}
            </div>
        </div>
        
        <script>
            function toggleAppDetails(element) {{
                element.classList.toggle('expanded');
                const details = element.nextElementSibling;
                if (details && details.classList.contains('window-details')) {{
                    details.classList.toggle('visible');
                }}
            }}
        </script>
        '''}
        '''}
    </div>
</body>
</html>
"""
        
        # Write HTML file
        output_path.parent.mkdir(parents=True, exist_ok=True)
        with open(output_path, 'w', encoding='utf-8') as f:
            f.write(html_content)
        
        return output_path
    
    def generate_weekly_html_report(self, week_data: dict, output_path: Path):
        """Generate a weekly HTML report"""
        
        # Prepare daily summary data
        daily_summaries = []
        for day_data in week_data['daily_reports']:
            if day_data['exists'] and not day_data.get('error'):
                daily_summaries.append({
                    'date': day_data['date'].strftime('%Y-%m-%d'),
                    'weekday': day_data['date'].strftime('%A'),
                    'work_time': self.format_duration(day_data['total_work_seconds']),
                    'break_time': self.format_duration(day_data['total_break_seconds']),
                    'work_seconds': day_data['total_work_seconds'],
                    'break_seconds': day_data['total_break_seconds']
                })
        
        # Prepare app statistics data
        app_stats_list = []
        if week_data.get('app_stats'):
            sorted_apps = sorted(week_data['app_stats'].items(), key=lambda x: x[1]['total'], reverse=True)
            total_app_time = sum(app['total'] for app in week_data['app_stats'].values())
            
            for app_name, app_data in sorted_apps:
                seconds = app_data['total']
                if seconds > 0:
                    percentage = (seconds / total_app_time * 100) if total_app_time > 0 else 0
                    
                    windows = []
                    for title, title_seconds in sorted(app_data['windows'].items(), key=lambda x: x[1], reverse=True):
                        title_percentage = (title_seconds / seconds * 100) if seconds > 0 else 0
                        windows.append({
                            'title': title,
                            'seconds': title_seconds,
                            'formatted_time': self.format_duration(title_seconds),
                            'percentage': title_percentage
                        })
                    
                    app_stats_list.append({
                        'name': app_name,
                        'seconds': seconds,
                        'formatted_time': self.format_duration(seconds),
                        'percentage': percentage,
                        'windows': windows
                    })
        
        # Generate HTML (reuse styles from daily report)
        html_content = f"""<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Weekly Work/Break Report - {week_data['start_date'].strftime('%Y-%m-%d')} to {week_data['end_date'].strftime('%Y-%m-%d')}</title>
    <style>
        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }}
        
        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            background: linear-gradient(135deg, #1e1e2e 0%, #2a2a3e 100%);
            color: #e0e0e0;
            padding: 20px;
            min-height: 100vh;
        }}
        
        .container {{
            max-width: 1400px;
            margin: 0 auto;
        }}
        
        h1 {{
            font-size: 28px;
            margin-bottom: 10px;
            color: #ffffff;
            font-weight: 400;
        }}
        
        .week-range {{
            font-size: 16px;
            color: #a0a0b0;
            margin-bottom: 30px;
        }}
        
        .stats-grid {{
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
            gap: 20px;
            margin-bottom: 40px;
        }}
        
        .stat-card {{
            background: rgba(40, 40, 60, 0.6);
            border: 1px solid rgba(100, 100, 120, 0.3);
            border-radius: 8px;
            padding: 20px;
            backdrop-filter: blur(10px);
        }}
        
        .stat-label {{
            font-size: 13px;
            color: #a0a0b0;
            margin-bottom: 8px;
            text-transform: uppercase;
            letter-spacing: 0.5px;
        }}
        
        .stat-value {{
            font-size: 32px;
            color: #ffffff;
            font-weight: 300;
            font-family: 'Consolas', 'Courier New', monospace;
        }}
        
        .daily-container {{
            background: rgba(40, 40, 60, 0.6);
            border: 1px solid rgba(100, 100, 120, 0.3);
            border-radius: 8px;
            padding: 30px;
            backdrop-filter: blur(10px);
            margin-bottom: 40px;
        }}
        
        h2 {{
            font-size: 20px;
            color: #ffffff;
            font-weight: 400;
            margin-bottom: 20px;
        }}
        
        .daily-table {{
            width: 100%;
            border-collapse: collapse;
        }}
        
        .daily-table th {{
            text-align: left;
            padding: 12px;
            color: #a0a0b0;
            font-size: 13px;
            font-weight: 400;
            border-bottom: 1px solid rgba(100, 100, 120, 0.3);
        }}
        
        .daily-table td {{
            padding: 12px;
            color: #e0e0e0;
            font-size: 14px;
            border-bottom: 1px solid rgba(100, 100, 120, 0.2);
        }}
        
        .daily-table td.mono {{
            font-family: 'Consolas', monospace;
            color: #a0a0b0;
        }}
        
        .daily-table tr:hover {{
            background: rgba(50, 50, 70, 0.3);
        }}
        
        .app-stats {{
            margin-top: 0;
        }}
        
        .app-list {{
            display: grid;
            gap: 8px;
            overflow: hidden;
        }}
        
        .app-item {{
            display: flex;
            align-items: center;
            background: rgba(50, 50, 70, 0.4);
            border-radius: 6px;
            padding: 12px 16px;
            transition: background 0.2s ease;
            cursor: pointer;
            user-select: none;
            min-width: 0;
            max-width: 100%;
            overflow: hidden;
        }}
        
        .app-item:hover {{
            background: rgba(60, 60, 80, 0.6);
        }}
        
        .app-item.expanded {{
            border-bottom-left-radius: 0;
            border-bottom-right-radius: 0;
        }}
        
        .expand-icon {{
            margin-right: 12px;
            color: #808090;
            transition: transform 0.2s ease;
            font-size: 12px;
            width: 12px;
            flex-shrink: 0;
        }}
        
        .app-item.expanded .expand-icon {{
            transform: rotate(90deg);
        }}
        
        .app-name {{
            flex: 1;
            color: #e0e0e0;
            font-size: 14px;
            overflow: hidden;
            text-overflow: ellipsis;
            white-space: nowrap;
            min-width: 0;
            max-width: 600px;
            flex-basis: 0;
        }}
        
        .app-time {{
            color: #a0a0b0;
            font-family: 'Consolas', monospace;
            font-size: 13px;
            margin-right: 16px;
            flex-shrink: 0;
            min-width: 70px;
            width: 70px;
        }}
        
        .app-bar {{
            width: 200px;
            height: 20px;
            background: rgba(30, 30, 40, 0.8);
            border-radius: 10px;
            overflow: hidden;
            position: relative;
            flex-shrink: 0;
        }}
        
        .app-bar-fill {{
            height: 100%;
            background: linear-gradient(90deg, #6366f1 0%, #8b5cf6 100%);
            border-radius: 10px;
            transition: width 0.3s ease;
        }}
        
        .app-percentage {{
            color: #808090;
            font-size: 12px;
            margin-left: 12px;
            min-width: 50px;
            width: 50px;
            text-align: right;
            flex-shrink: 0;
        }}
        
        .window-details {{
            display: none;
            background: rgba(40, 40, 60, 0.6);
            border-bottom-left-radius: 6px;
            border-bottom-right-radius: 6px;
            border-top: 1px solid rgba(100, 100, 120, 0.2);
            padding: 8px 16px 12px 16px;
            margin-top: -6px;
            overflow: hidden;
        }}
        
        .window-details.visible {{
            display: block;
        }}
        
        .window-item {{
            display: flex;
            align-items: center;
            padding: 8px 12px;
            margin-top: 4px;
            background: rgba(30, 30, 50, 0.4);
            border-radius: 4px;
            min-width: 0;
            max-width: 100%;
            overflow: hidden;
        }}
        
        .window-title {{
            flex: 1;
            color: #b0b0c0;
            font-size: 13px;
            margin-left: 36px;
            overflow: hidden;
            text-overflow: ellipsis;
            white-space: nowrap;
            min-width: 0;
            max-width: 500px;
            flex-basis: 0;
        }}
        
        .window-time {{
            color: #808090;
            font-family: 'Consolas', monospace;
            font-size: 12px;
            margin-right: 12px;
            flex-shrink: 0;
            min-width: 65px;
            width: 65px;
        }}
        
        .window-bar {{
            width: 150px;
            height: 16px;
            background: rgba(20, 20, 30, 0.8);
            border-radius: 8px;
            overflow: hidden;
            flex-shrink: 0;
        }}
        
        .window-bar-fill {{
            height: 100%;
            background: linear-gradient(90deg, #06b6d4 0%, #0891b2 100%);
            border-radius: 8px;
        }}
        
        .window-percentage {{
            color: #707080;
            font-size: 11px;
            margin-left: 10px;
            min-width: 45px;
            width: 45px;
            text-align: right;
            flex-shrink: 0;
        }}
        
        .note {{
            margin-top: 20px;
            font-size: 12px;
            color: #808090;
            font-style: italic;
        }}
    </style>
</head>
<body>
    <div class="container">
        <h1>📅 Weekly Work/Break Report</h1>
        <div class="week-range">{week_data['start_date'].strftime('%B %d')} - {week_data['end_date'].strftime('%B %d, %Y')}</div>
        
        <div class="stats-grid">
            <div class="stat-card">
                <div class="stat-label">Total Work Time</div>
                <div class="stat-value">{self.format_duration(week_data['total_work_seconds'])}</div>
            </div>
            
            <div class="stat-card">
                <div class="stat-label">Total Break Time</div>
                <div class="stat-value">{self.format_duration(week_data['total_break_seconds'])}</div>
            </div>
            
            <div class="stat-card">
                <div class="stat-label">Avg Work/Day</div>
                <div class="stat-value">{self.format_duration(week_data['total_work_seconds'] // 7)}</div>
            </div>
            
            <div class="stat-card">
                <div class="stat-label">Days Tracked</div>
                <div class="stat-value">{len(daily_summaries)}</div>
            </div>
        </div>
        
        <div class="daily-container">
            <h2>📊 Daily Breakdown</h2>
            <table class="daily-table">
                <thead>
                    <tr>
                        <th>Date</th>
                        <th>Day</th>
                        <th>Work Time</th>
                        <th>Break Time</th>
                    </tr>
                </thead>
                <tbody>
                    {"".join([
                        f'''<tr>
                        <td>{day['date']}</td>
                        <td>{day['weekday']}</td>
                        <td class="mono">{day['work_time']}</td>
                        <td class="mono">{day['break_time']}</td>
                    </tr>'''
                        for day in daily_summaries
                    ])}
                </tbody>
            </table>
        </div>
        
        {"" if not app_stats_list else f'''
        <div class="daily-container app-stats">
            <h2>📱 Application Usage Statistics (Week Total)</h2>
            <div class="app-list">
                {"".join([
                    f'''<div>
                    <div class="app-item" onclick="toggleAppDetails(this)">
                        <span class="expand-icon">▶</span>
                        <div class="app-name" title="{app["name"]}">{app["name"]}</div>
                        <div class="app-time">{app["formatted_time"]}</div>
                        <div class="app-bar">
                            <div class="app-bar-fill" style="width: {app["percentage"]:.1f}%;"></div>
                        </div>
                        <div class="app-percentage">{app["percentage"]:.1f}%</div>
                    </div>
                    {"" if not app["windows"] else f'''<div class="window-details">
                        {"".join([
                            f'''<div class="window-item">
                                <div class="window-title" title="{window["title"]}">{window["title"]}</div>
                                <div class="window-time">{window["formatted_time"]}</div>
                                <div class="window-bar">
                                    <div class="window-bar-fill" style="width: {window["percentage"]:.1f}%;"></div>
                                </div>
                                <div class="window-percentage">{window["percentage"]:.1f}%</div>
                            </div>'''
                            for window in app["windows"]
                        ])}
                    </div>'''}
                </div>'''
                    for app in app_stats_list
                ])}
            </div>
        </div>
        '''}
        
        <div class="note">
            Report generated: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}
        </div>
        
        <script>
            function toggleAppDetails(element) {{
                element.classList.toggle('expanded');
                const details = element.nextElementSibling;
                if (details && details.classList.contains('window-details')) {{
                    details.classList.toggle('visible');
                }}
            }}
        </script>
    </div>
</body>
</html>
"""
        
        # Write HTML file
        output_path.parent.mkdir(parents=True, exist_ok=True)
        with open(output_path, 'w', encoding='utf-8') as f:
            f.write(html_content)
        
        return output_path


def main():
    """Main entry point for the report generator"""
    # Fix encoding for Windows console
    import sys
    if sys.platform == 'win32':
        try:
            sys.stdout.reconfigure(encoding='utf-8')
        except:
            pass
    
    parser = argparse.ArgumentParser(
        description='Generate Work/Break reports from StandUpTracker logs'
    )
    parser.add_argument(
        '--yesterday',
        action='store_true',
        help='Generate report for yesterday'
    )
    parser.add_argument(
        '--date',
        type=str,
        help='Generate report for specific date (YYYY-MM-DD)'
    )
    parser.add_argument(
        '--week',
        action='store_true',
        help='Generate weekly report for current week (Monday-Sunday)'
    )
    parser.add_argument(
        '--last-week',
        action='store_true',
        help='Generate weekly report for last week'
    )
    parser.add_argument(
        '--output',
        type=str,
        help='Output HTML file path (default: <LOCALAPPDATA>/StandUpTracker/reports/<date>.html)'
    )
    parser.add_argument(
        '--no-open',
        action='store_true',
        help='Do not open the report in browser'
    )
    
    args = parser.parse_args()
    
    # Get logs folder
    logs_folder = Path(os.getenv('LOCALAPPDATA')) / 'StandUpTracker' / 'logs'
    
    if not logs_folder.exists():
        print(f"Error: Logs folder not found: {logs_folder}")
        print("Make sure StandUpTracker has been running and creating logs.")
        sys.exit(1)
    
    reporter = ActivityReport(logs_folder)
    
    # Handle weekly reports
    if args.week or args.last_week:
        # Calculate Monday of target week
        today = datetime.now()
        days_since_monday = (today.weekday()) % 7
        this_monday = today - timedelta(days=days_since_monday)
        
        if args.last_week:
            start_date = this_monday - timedelta(days=7)
        else:
            start_date = this_monday
        
        print(f"Generating weekly report for {start_date.strftime('%Y-%m-%d')} to {(start_date + timedelta(days=6)).strftime('%Y-%m-%d')}...")
        
        week_data = reporter.parse_week(start_date)
        
        # Determine output path
        if args.output:
            output_path = Path(args.output)
        else:
            reports_folder = Path(os.getenv('LOCALAPPDATA')) / 'StandUpTracker' / 'reports'
            output_path = reports_folder / f"weekly_report_{start_date.strftime('%Y-%m-%d')}.html"
        
        # Generate HTML
        output_file = reporter.generate_weekly_html_report(week_data, output_path)
        
        print(f"[OK] Weekly report generated: {output_file}")
        print(f"  Period: {week_data['start_date'].strftime('%Y-%m-%d')} to {week_data['end_date'].strftime('%Y-%m-%d')}")
        print(f"  Total Work: {reporter.format_duration(week_data['total_work_seconds'])}")
        print(f"  Total Break: {reporter.format_duration(week_data['total_break_seconds'])}")
        print(f"  Days Tracked: {sum(1 for day in week_data['daily_reports'] if day['exists'] and not day.get('error'))}")
        
        # Open in browser
        if not args.no_open:
            print(f"\nOpening report in browser...")
            webbrowser.open(output_file.as_uri())
        
        return 0
    
    # Handle daily reports
    # Determine target date
    if args.date:
        try:
            target_date = datetime.strptime(args.date, '%Y-%m-%d')
        except ValueError:
            print(f"Error: Invalid date format '{args.date}'. Use YYYY-MM-DD")
            sys.exit(1)
    elif args.yesterday:
        target_date = datetime.now() - timedelta(days=1)
    else:
        target_date = datetime.now()
    
    # Generate report
    print(f"Generating report for {target_date.strftime('%Y-%m-%d')}...")
    
    report_data = reporter.parse_log(target_date)
    
    # Determine output path
    if args.output:
        output_path = Path(args.output)
    else:
        reports_folder = Path(os.getenv('LOCALAPPDATA')) / 'StandUpTracker' / 'reports'
        output_path = reports_folder / f"report_{target_date.strftime('%Y-%m-%d')}.html"
    
    # Generate HTML
    output_file = reporter.generate_html_report(report_data, output_path)
    
    print(f"[OK] Report generated: {output_file}")
    
    if report_data.get('error'):
        print(f"[!] Warning: {report_data['error']}")
    else:
        print(f"  Start: {report_data['start_time'].strftime('%H:%M:%S') if report_data['start_time'] else 'N/A'}")
        print(f"  End: {report_data['end_time'].strftime('%H:%M:%S') if report_data['end_time'] else 'N/A'}")
        print(f"  Work: {reporter.format_duration(report_data['total_work_seconds'])}")
        print(f"  Break: {reporter.format_duration(report_data['total_break_seconds'])}")
    
    # Open in browser
    if not args.no_open:
        print(f"\nOpening report in browser...")
        webbrowser.open(output_file.as_uri())
    
    return 0


if __name__ == '__main__':
    sys.exit(main())
