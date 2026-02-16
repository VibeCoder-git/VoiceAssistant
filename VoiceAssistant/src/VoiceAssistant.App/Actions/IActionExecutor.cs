using System.Collections.Generic;
using System.Threading.Tasks;

namespace VoiceAssistant.App.Actions
{
    public class ActionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
    }

    public interface IActionExecutor
    {
        string ActionType { get; }
        Task<ActionResult> ExecuteAsync(Dictionary<string, string> args);
    }
}
