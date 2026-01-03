using System;
using Microsoft.Win32;
using StandUpTracker.Utils;
using static StandUpTracker.Utils.NativeMethods;

namespace StandUpTracker.Services
{
    public sealed class IdleTracker : IDisposable
    {
        // volatile — щоб читання з таймера бачили актуальний стан після подій SystemEvents
        private volatile bool _isLocked;
        public bool IsLocked => _isLocked;

        public IdleTracker()
        {
            SystemEvents.SessionSwitch += OnSessionSwitch;
        }

        public int GetIdleSeconds()
        {
            var lii = new LASTINPUTINFO
            {
                cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(LASTINPUTINFO))
            };
            if (!GetLastInputInfo(ref lii))
                return 0;

            // 64-бітні тики — без переповнення кожні ~49 днів
            ulong tick = GetTickCount64();
            ulong last = lii.dwTime; // DWORD, але різниця з 64-бітними тиками коректна семантично
            ulong idleMs = tick - last;

            return (int)(idleMs / 1000UL);
        }

        private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
        {
            switch (e.Reason)
            {
                // Трактуємо як "заблоковано/перерва"
                case SessionSwitchReason.SessionLock:
                case SessionSwitchReason.SessionLogoff:
                case SessionSwitchReason.ConsoleDisconnect:
                case SessionSwitchReason.RemoteDisconnect:
                    _isLocked = true;
                    break;

                // Трактуємо як "розблоковано/повернувся"
                case SessionSwitchReason.SessionUnlock:
                case SessionSwitchReason.SessionLogon:
                case SessionSwitchReason.ConsoleConnect:
                case SessionSwitchReason.RemoteConnect:
                case SessionSwitchReason.SessionRemoteControl:
                    _isLocked = false;
                    break;

                default:
                    // інші події ігноруємо
                    break;
            }
        }

        public void Dispose()
        {
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            GC.SuppressFinalize(this);
        }
    }
}
