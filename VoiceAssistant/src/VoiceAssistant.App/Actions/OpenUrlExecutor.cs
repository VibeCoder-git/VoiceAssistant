using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Serilog;

namespace VoiceAssistant.App.Actions
{
    public class OpenUrlExecutor : IActionExecutor
    {
        public string ActionType => "open_url";

        public Task<ActionResult> ExecuteAsync(Dictionary<string, string> args)
        {
            if (!args.TryGetValue("url", out var url))
            {
                return Task.FromResult(new ActionResult { Success = false, Message = "Missing 'url' argument" });
            }

            try
            {
                Log.Information($"Executing open_url: {url}");
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
                return Task.FromResult(new ActionResult { Success = true, Message = $"Opened {url}" });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to open URL");
                return Task.FromResult(new ActionResult { Success = false, Message = ex.Message });
            }
        }
    }
}
