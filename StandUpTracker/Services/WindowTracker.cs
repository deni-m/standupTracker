using System;
using System.Diagnostics;
using StandUpTracker.Models;
using static StandUpTracker.Utils.NativeMethods;

namespace StandUpTracker.Services
{
    public class WindowTracker
    {
        public ActiveSample? CaptureActiveSample()
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return null;
            if (!IsWindowVisible(hwnd)) return null;

            string title = StandUpTracker.Utils.NativeMethods.GetWindowTitle(hwnd);

            GetWindowThreadProcessId(hwnd, out var pid);
            string procName = "unknown.exe";
            try { using var p = Process.GetProcessById((int)pid); procName = p.ProcessName + ".exe"; } catch { }

            if (string.IsNullOrWhiteSpace(title) && procName.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase))
                return null;

            return new ActiveSample { Process = procName, Title = title, Start = DateTime.Now };
        }
    }
}