using System.Collections.Generic;
using System.Linq;

namespace VoiceAssistant.App.Skills
{
    public class SkillRegistry
    {
        private readonly Dictionary<string, SkillManifest> _skills = new();

        public void Register(SkillManifest manifest)
        {
            if (manifest == null) return;
            var normalizer = new TextNormalizer(); 

            foreach (var phrase in manifest.Phrases)
            {
                var normalized = normalizer.Normalize(phrase);
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    _skills[normalized] = manifest;
                }
            }
        }

        public void Clear()
        {
            _skills.Clear();
        }

        // Distinct by Id
        public IReadOnlyList<SkillManifest> GetAll() 
        {
            return _skills.Values
                .GroupBy(s => s.Id)
                .Select(g => g.First())
                .ToList()
                .AsReadOnly();
        }

        public SkillManifest FindByPhrase(string phrase)
        {
            return _skills.TryGetValue(phrase, out var skill) ? skill : null;
        }
    }
}
