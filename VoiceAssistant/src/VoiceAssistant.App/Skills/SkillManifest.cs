using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VoiceAssistant.App.Skills
{
    public class SkillManifest
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("phrases")]
        public List<string> Phrases { get; set; } = new();

        [JsonPropertyName("patterns")]
        public List<string> Patterns { get; set; } = new();

        [JsonPropertyName("action")]
        public SkillAction Action { get; set; } = new();

        [JsonPropertyName("responseSound")]
        public string ResponseSound { get; set; }
    }

    public class SkillAction
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("args")]
        public Dictionary<string, string> Args { get; set; } = new();
    }
}
