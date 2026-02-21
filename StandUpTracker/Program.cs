using System;
using System.Threading;
using System.Windows.Forms;
using StandUpTracker.UI;
using StandUpTracker.Services;

namespace StandUpTracker
{
    internal static class Program
    {
        // Unique GUID for the mutex
        private static readonly string MutexName = "Global\\StandUpTracker_SingleInstance_Mutex";

        [STAThread]
        static void Main()
        {
            using (var mutex = new Mutex(false, MutexName, out bool createdNew))
            {
                if (!createdNew)
                {
                    // App is already running
                    MessageBox.Show("StandUp Tracker is already running! Check your system tray.", 
                        "StandUp Tracker", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                // Run the application
                Application.Run(new TrayApp(
                    new IdleTracker(),
                    new WindowTracker(),
                    new DoNotDisturbService(),
                    new ActivityLogger()
                ));
            }
        }
    }
}
