using System;
using System.Text;
using static StandUpTracker.Utils.NativeMethods;

namespace StandUpTracker.Services
{
    public class DoNotDisturbService
    {
        public bool IsDoNotDisturb()
        {
            return IsForegroundFullscreen() || IsPowerPointSlideShowHeuristic() || LooksLikeScreenSharingByTitles();
        }

        private static bool IsForegroundFullscreen()
        {
            var h = GetForegroundWindow();
            if (h == IntPtr.Zero) return false;
            if (!GetWindowRect(h, out var r)) return false;

            int sw = GetSystemMetrics(0); // SM_CXSCREEN
            int sh = GetSystemMetrics(1); // SM_CYSCREEN
            int w = r.Right - r.Left;
            int hgt = r.Bottom - r.Top;

            return Math.Abs(w - sw) <= 2 && Math.Abs(hgt - sh) <= 2;
        }

        private static bool IsPowerPointSlideShowHeuristic()
        {
            bool found = false;
            EnumWindows((hwnd, l) =>
            {
                if (!IsWindowVisible(hwnd)) return true;
                int len = GetWindowTextLength(hwnd);
                if (len <= 0) return true;
                var sb = new StringBuilder(len + 1);
                GetWindowText(hwnd, sb, sb.Capacity);
                var title = sb.ToString();
                if (title.Contains("Slide Show", StringComparison.OrdinalIgnoreCase) ||
                    title.Contains("Показ слайдов", StringComparison.OrdinalIgnoreCase) || // Russian: Slideshow
                    title.Contains("Показ слайдів", StringComparison.OrdinalIgnoreCase))  // Ukrainian: Slideshow
                {
                    found = true; return false;
                }
                return true;
            }, IntPtr.Zero);
            return found;
        }

        private static bool LooksLikeScreenSharingByTitles()
        {
            string[] hints =
            {
                "You are screen sharing","You're sharing","Screen sharing",
                "Ві демонструєте екран",  // Ukrainian: You are sharing the screen
                "Ві ділитеся екраном",     // Ukrainian: You are sharing the screen
                "Вы демонстрируете экран"  // Russian: You are sharing the screen
            };
            bool found = false;
            EnumWindows((hwnd, l) =>
            {
                if (!IsWindowVisible(hwnd)) return true;
                int len = GetWindowTextLength(hwnd);
                if (len <= 0) return true;
                var sb = new StringBuilder(len + 1);
                GetWindowText(hwnd, sb, sb.Capacity);
                var title = sb.ToString();
                foreach (var h in hints)
                {
                    if (title.IndexOf(h, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        found = true; return false;
                    }
                }
                return true;
            }, IntPtr.Zero);
            return found;
        }
    }
}
