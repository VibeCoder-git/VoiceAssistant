using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Serilog;
using VoiceAssistant.App.Windows;

namespace VoiceAssistant.App.Actions
{
    public class BringToFrontExecutor : IActionExecutor
    {
        public string ActionType => "bring_to_front";

        public Task<ActionResult> ExecuteAsync(Dictionary<string, string> args)
        {
            if (!args.TryGetValue("process", out var processName))
            {
                return Task.FromResult(new ActionResult { Success = false, Message = "Missing 'process' argument" });
            }

            try
            {
                var processes = Process.GetProcessesByName(processName);
                if (processes.Length == 0)
                {
                    return Task.FromResult(new ActionResult { Success = false, Message = $"Process {processName} not found" });
                }

                // Pick first with window
                foreach (var p in processes)
                {
                    if (p.MainWindowHandle != IntPtr.Zero)
                    {
                        bool success = WindowActivator.ActivateWindow(p.MainWindowHandle);
                        if (success) return Task.FromResult(new ActionResult { Success = true, Message = $"Activated {processName}" });
                    }
                }

                return Task.FromResult(new ActionResult { Success = false, Message = "No window handle found" });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "BringToFront failed");
                return Task.FromResult(new ActionResult { Success = false, Message = ex.Message });
            }
        }
    }
}
