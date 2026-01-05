using System;
using Microsoft.Win32;
using StandUpTracker.Utils;
using static StandUpTracker.Utils.NativeMethods;

namespace StandUpTracker.Services
{
    public sealed class IdleTracker : IDisposable
    {
        // volatile — so timer reads see the actual state after SystemEvents
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

            // 64-bit ticks — no overflow every ~49 days
            ulong tick = GetTickCount64();
            ulong last = lii.dwTime; // DWORD, but difference with 64-bit ticks is semantically correct
            ulong idleMs = tick - last;

            return (int)(idleMs / 1000UL);
        }

        private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
        {
            switch (e.Reason)
            {
                // Treat as "locked/break"
                case SessionSwitchReason.SessionLock:
                case SessionSwitchReason.SessionLogoff:
                case SessionSwitchReason.ConsoleDisconnect:
                case SessionSwitchReason.RemoteDisconnect:
                    _isLocked = true;
                    break;

                // Treat as "unlocked/returned"
                case SessionSwitchReason.SessionUnlock:
                case SessionSwitchReason.SessionLogon:
                case SessionSwitchReason.ConsoleConnect:
                case SessionSwitchReason.RemoteConnect:
                case SessionSwitchReason.SessionRemoteControl:
                    _isLocked = false;
                    break;

                default:
                    // ignore other events
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
