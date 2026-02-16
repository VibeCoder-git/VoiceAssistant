using Serilog;
using System.Linq;
using VoiceAssistant.App.Config;
using VoiceAssistant.App.Skills;

namespace VoiceAssistant.App.Core
{
    public class CommandRouter
    {
        private readonly SkillRegistry _registry;
        private readonly WakeConfig _wakeConfig;
        private readonly TextNormalizer _normalizer;
        
        private readonly string _cachedWakeWord;

        public CommandRouter(SkillRegistry registry, WakeConfig wakeConfig)
        {
            _registry = registry;
            _wakeConfig = wakeConfig;
            _normalizer = new TextNormalizer();
            
            // Normalize and cache wake word at startup
            _cachedWakeWord = _normalizer.Normalize(_wakeConfig.RequiresStartsWith);
            Log.Debug($"Wake Word Normalized: '{_cachedWakeWord}'");
        }

        public string Normalize(string text) => _normalizer.Normalize(text);

        public (SkillManifest Skill, SkillAction Action) Route(string text)
        {
            var normalized = _normalizer.Normalize(text);
            
            if (!normalized.StartsWith(_cachedWakeWord))
            {
                return (null, null);
            }

            var commandText = normalized.Substring(_cachedWakeWord.Length).Trim();
            
            if (string.IsNullOrEmpty(commandText))
            {
                return (null, null);
            }

            Log.Debug($"Routing command: '{commandText}'");

            var skill = _registry.FindByPhrase(commandText);

            if (skill != null)
            {
                 return (skill, skill.Action);
            }

            return (null, null);
        }
    }
}
