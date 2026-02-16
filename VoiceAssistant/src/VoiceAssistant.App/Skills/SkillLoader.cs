using System;
using System.IO;
using System.Text.Json;
using Serilog;
using VoiceAssistant.App.Config;

namespace VoiceAssistant.App.Skills
{
    public class SkillLoader
    {
        private readonly SkillsConfig _config;
        private readonly SkillRegistry _registry;

        public SkillLoader(SkillsConfig config, SkillRegistry registry)
        {
            _config = config;
            _registry = registry;
        }

        public void LoadSkills()
        {
            _registry.Clear();
            var skillsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _config.Directory);

            if (!Directory.Exists(skillsDir))
            {
                Log.Warning($"Skills directory not found: {skillsDir}");
                return;
            }

            foreach (var dir in Directory.GetDirectories(skillsDir))
            {
                var manifestPath = Path.Combine(dir, "manifest.json");
                if (File.Exists(manifestPath))
                {
                    try
                    {
                        var json = File.ReadAllText(manifestPath);
                        var manifest = JsonSerializer.Deserialize<SkillManifest>(json);

                        if (Validate(manifest))
                        {
                            _registry.Register(manifest);
                            Log.Information($"Loaded skill: {manifest.Id} ({manifest.Phrases.Count} phrases)");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, $"Failed to load skill from {dir}");
                    }
                }
            }
        }

        private bool Validate(SkillManifest manifest)
        {
            if (string.IsNullOrWhiteSpace(manifest.Id)) return false;
            if (manifest.Action == null || string.IsNullOrWhiteSpace(manifest.Action.Type)) return false;
            return true;
        }
    }
}
