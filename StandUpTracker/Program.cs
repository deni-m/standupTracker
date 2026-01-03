using System;
using System.Windows.Forms;
using StandUpTracker.UI;
using StandUpTracker.Services;

namespace StandUpTracker
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Application.Run(new TrayApp(
                new IdleTracker(),
                new WindowTracker(),
                new DoNotDisturbService(),
                new ActivityLogger()
            ));
        }
    }
}
