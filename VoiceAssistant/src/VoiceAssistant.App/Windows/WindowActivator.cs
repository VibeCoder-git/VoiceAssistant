using System;
using Serilog;

namespace VoiceAssistant.App.Windows
{
    public static class WindowActivator
    {
        public static bool ActivateWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return false;

            // 1. Restore if minimized
            if (NativeMethods.IsIconic(hWnd))
            {
                NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);
            }
            else
            {
                NativeMethods.ShowWindow(hWnd, NativeMethods.SW_SHOW);
            }

            // 2. Try SetForegroundWindow
            bool active = NativeMethods.SetForegroundWindow(hWnd);

            // Verify
            if (NativeMethods.GetForegroundWindow() == hWnd) return true;

            // 3. Fallback: AttachThreadInput
            Log.Debug("SetForegroundWindow failed, trying AttachThreadInput hack...");
            
            uint foreThread = NativeMethods.GetWindowThreadProcessId(NativeMethods.GetForegroundWindow(), out _);
            uint appThread = NativeMethods.GetCurrentThreadId();
            uint targetThread = NativeMethods.GetWindowThreadProcessId(hWnd, out _);

            bool success = false;
            try
            {
                if (foreThread != appThread)
                {
                    NativeMethods.AttachThreadInput(foreThread, appThread, true);
                    NativeMethods.AttachThreadInput(targetThread, appThread, true);
                }

                // Try force with TopMost toggle
                // Sometimes toggling TopMost helps bring it to front
                // SWP_NOSIZE | SWP_NOMOVE = 0x0001 | 0x0002
                NativeMethods.SetWindowPos(hWnd, new IntPtr(-1), 0, 0, 0, 0, 0x0003); // HWND_TOPMOST
                NativeMethods.SetWindowPos(hWnd, new IntPtr(-2), 0, 0, 0, 0, 0x0003); // HWND_NOTOPMOST
                
                NativeMethods.SetForegroundWindow(hWnd);
                
                if (NativeMethods.GetForegroundWindow() == hWnd) success = true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "AttachThreadInput failed");
            }
            finally
            {
                if (foreThread != appThread)
                {
                    NativeMethods.AttachThreadInput(foreThread, appThread, false);
                    NativeMethods.AttachThreadInput(targetThread, appThread, false);
                }
            }

            return success;
        }
    }
}
