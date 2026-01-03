using System;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using StandUpTracker.Config;
using StandUpTracker.Services;

namespace StandUpTracker.UI
{
    /// <summary>
    /// System tray application UI. Handles only UI concerns:
    /// - Tray icon and context menu
    /// - Balloon notifications
    /// - Stats dialog
    /// 
    /// All business logic is delegated to ActivityMonitor.
    /// </summary>
    public class TrayApp : Form
    {
        #region Fields

        private readonly ActivityMonitor _monitor;
        private readonly PomodoroTimer _pomodoro;
        private readonly NotifyIcon _tray;

        #endregion

        #region Constructor

        public TrayApp(
            IdleTracker idleTracker,
            WindowTracker windowTracker,
            DoNotDisturbService dndService,
            ActivityLogger activityLogger)
        {
            // Create the activity monitor (orchestrates all business logic)
            _monitor = new ActivityMonitor(idleTracker, windowTracker, dndService, activityLogger);

            // Create Pomodoro timer
            _pomodoro = new PomodoroTimer(new ServiceLogger());

            // Subscribe to monitor events
            _monitor.StateChanged += OnStateChanged;
            _monitor.ReminderDue += OnReminderDue;
            _monitor.GraceWarningDue += OnGraceWarningDue;
            _monitor.TooltipUpdateRequired += OnTooltipUpdateRequired;
            _monitor.PauseStateChanged += OnPauseStateChanged;

            // Subscribe to Pomodoro events
            _pomodoro.Started += OnPomodoroStarted;
            _pomodoro.Stopped += OnPomodoroStopped;
            _pomodoro.Tick += OnPomodoroTick;
            _pomodoro.WarningDue += OnPomodoroWarningDue;
            _pomodoro.Completed += OnPomodoroCompleted;

            // Initialize form (hidden)
            InitializeForm();

            // Initialize tray icon
            _tray = CreateTrayIcon();

            // Start monitoring
            _monitor.Start();
            UpdateTrayTooltip(_monitor.GetTooltipText());
        }

        #endregion

        #region Form Initialization

        private void InitializeForm()
        {
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;
            this.Visible = false;
        }

        private NotifyIcon CreateTrayIcon()
        {
            return new NotifyIcon
            {
                Text = "StandUp: —",
                Icon = System.Drawing.SystemIcons.Information,
                Visible = true,
                ContextMenuStrip = BuildMenu()
            };
        }

        #endregion

        #region Menu Building

        private ContextMenuStrip BuildMenu()
        {
            var menu = new ContextMenuStrip();

            var pauseItem = CreatePauseMenuItem();
            var pomodoroMenu = CreatePomodoroSubMenu();

            menu.Items.Add("Show Today's Statistics", null, (s, e) => ShowTodayStats());
            menu.Items.Add("Open Logs Folder", null, (s, e) => OpenLogsFolder());
            menu.Items.Add("Open Service Logs", null, (s, e) => OpenServiceLogsFolder());
            menu.Items.Add(pomodoroMenu);
            menu.Items.Add(pauseItem);
            menu.Items.Add("Exit", null, (s, e) => ExitApplication());

            return menu;
        }

        private ToolStripMenuItem CreatePomodoroSubMenu()
        {
            var pomodoroMenu = new ToolStripMenuItem($"🍅 Pomodoro ({AppSettings.PomodoroMinutes} min)");
            
            pomodoroMenu.DropDownItems.Add("▶ Start", null, (s, e) => _pomodoro.Start());
            pomodoroMenu.DropDownItems.Add("⏹ Stop", null, (s, e) => _pomodoro.Stop());
            pomodoroMenu.DropDownItems.Add("🔄 Restart", null, (s, e) => _pomodoro.Restart());

            return pomodoroMenu;
        }

        private ToolStripMenuItem CreatePauseMenuItem()
        {
            var pauseItem = new ToolStripMenuItem("Pause") { CheckOnClick = true };
            pauseItem.CheckedChanged += (s, e) =>
            {
                _monitor.SetPaused(pauseItem.Checked);
            };
            return pauseItem;
        }

        #endregion

        #region Menu Handlers

        private void ShowTodayStats()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("Top activities today:");

                var stats = _monitor.ActivityLogger.TotalsToday
                    .OrderByDescending(kv => kv.Value)
                    .Take(20)
                    .ToList();

                foreach (var kv in stats)
                {
                    var parts = kv.Key.Split('|');
                    var proc = parts[0];
                    var title = parts.Length > 1 ? parts[1] : "";
                    sb.AppendLine($"{kv.Value:hh\\:mm\\:ss} — {proc} — {title}");
                }

                MessageBox.Show(sb.ToString(), "StandUp Tracker — Today",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "StandUp Tracker",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OpenLogsFolder()
        {
            try
            {
                System.Diagnostics.Process.Start("explorer.exe", AppSettings.LogsFolder);
            }
            catch { }
        }

        private void OpenServiceLogsFolder()
        {
            try
            {
                var folder = System.IO.Path.Combine(AppSettings.LogsFolder, "Service");
                System.Diagnostics.Process.Start("explorer.exe", folder);
            }
            catch { }
        }

        private void ExitApplication()
        {
            Application.Exit();
        }

        #endregion

        #region Event Handlers (from ActivityMonitor)

        private void OnStateChanged(object? sender, StateChangedEventArgs e)
        {
            // Could add UI feedback for state changes if needed
        }

        private void OnReminderDue(object? sender, ReminderEventArgs e)
        {
            if (e.IsMuted) return;

            SafeInvoke(() =>
            {
                ShowBalloonTip(
                    "Time to Stand Up",
                    "2-3 min stretch for your back and eyes 🙂",
                    4000
                );

                try
                {
                    Console.Beep(750, 300);
                }
                catch { }
            });
        }

        private void OnGraceWarningDue(object? sender, GraceWarningEventArgs e)
        {
            if (e.IsMuted) return;

            SafeInvoke(() =>
            {
                ShowBalloonTip(
                    "Almost Break Time",
                    "Move your mouse or press a key to continue the session.",
                    5000
                );
            });
        }

        private void OnTooltipUpdateRequired(object? sender, TooltipUpdateEventArgs e)
        {
            // Process Pomodoro tick on each activity monitor tick
            _pomodoro.ProcessTick();

            SafeInvoke(() => UpdatePomodoroTooltip());
        }

        private void OnPauseStateChanged(object? sender, bool isPaused)
        {
            SafeInvoke(() =>
            {
                ShowBalloonTip(
                    isPaused ? "Paused" : "Resumed",
                    isPaused ? "Logging and reminders suspended" : "Logging and reminders resumed",
                    2000
                );
            });
        }

        #endregion

        #region Pomodoro Event Handlers

        private void OnPomodoroStarted(object? sender, EventArgs e)
        {
            SafeInvoke(() =>
            {
                ShowBalloonTip(
                    "🍅 Pomodoro Started",
                    $"Work for {AppSettings.PomodoroMinutes} minutes, then take a break!",
                    3000
                );
                UpdatePomodoroTooltip();
            });
        }

        private void OnPomodoroStopped(object? sender, EventArgs e)
        {
            SafeInvoke(() =>
            {
                ShowBalloonTip(
                    "🍅 Pomodoro Stopped",
                    "Timer reset.",
                    2000
                );
                UpdatePomodoroTooltip();
            });
        }

        private void OnPomodoroTick(object? sender, PomodoroTickEventArgs e)
        {
            SafeInvoke(() => UpdatePomodoroTooltip());
        }

        private void OnPomodoroWarningDue(object? sender, EventArgs e)
        {
            SafeInvoke(() =>
            {
                ShowBalloonTip(
                    "🍅 Break Soon!",
                    $"{AppSettings.PomodoroWarningMinutes} minutes until end of session. Finish your current task.",
                    5000
                );
            });
        }

        private void OnPomodoroCompleted(object? sender, EventArgs e)
        {
            SafeInvoke(() =>
            {
                // Beep to alert user
                try
                {
                    Console.Beep(1000, 500);
                    System.Threading.Thread.Sleep(200);
                    Console.Beep(1000, 500);
                }
                catch { }

                ShowBalloonTip(
                    "🍅 Pomodoro Completed!",
                    "Break time! Stand up, stretch, drink water 💧",
                    6000
                );
                UpdatePomodoroTooltip();
            });
        }

        private void UpdatePomodoroTooltip()
        {
            var pomodoroText = _pomodoro.GetTooltipText();
            var baseText = _monitor.GetTooltipText();

            string tooltip;
            if (!string.IsNullOrEmpty(pomodoroText))
            {
                tooltip = $"{pomodoroText} | {baseText}";
            }
            else
            {
                tooltip = baseText;
            }

            UpdateTrayTooltip(tooltip);
        }

        #endregion

        #region UI Helpers

        private void ShowBalloonTip(string title, string text, int timeout)
        {
            _tray.BalloonTipTitle = title;
            _tray.BalloonTipText = text;
            _tray.ShowBalloonTip(timeout);
        }

        private void UpdateTrayTooltip(string text)
        {
            if (text.Length > 63)
                text = text.Substring(0, 63);

            try
            {
                if (_tray.Text != text)
                {
                    _tray.Text = text;
                }
            }
            catch { }
        }

        private void SafeInvoke(Action action)
        {
            if (InvokeRequired)
            {
                BeginInvoke(action);
            }
            else
            {
                action();
            }
        }

        #endregion

        #region Disposal

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);

            // Shutdown the monitor
            _monitor.Shutdown();

            // Unsubscribe from events
            _monitor.StateChanged -= OnStateChanged;
            _monitor.ReminderDue -= OnReminderDue;
            _monitor.GraceWarningDue -= OnGraceWarningDue;
            _monitor.TooltipUpdateRequired -= OnTooltipUpdateRequired;
            _monitor.PauseStateChanged -= OnPauseStateChanged;

            // Unsubscribe from Pomodoro events
            _pomodoro.Started -= OnPomodoroStarted;
            _pomodoro.Stopped -= OnPomodoroStopped;
            _pomodoro.Tick -= OnPomodoroTick;
            _pomodoro.WarningDue -= OnPomodoroWarningDue;
            _pomodoro.Completed -= OnPomodoroCompleted;

            // Dispose resources
            _monitor.Dispose();
            _tray.Visible = false;
            _tray.Dispose();
        }

        #endregion
    }
}