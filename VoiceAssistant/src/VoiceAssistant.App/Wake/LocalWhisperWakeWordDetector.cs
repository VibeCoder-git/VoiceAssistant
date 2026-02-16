using Serilog;
using System;
using System.Linq;
using System.Threading.Tasks;
using VoiceAssistant.App.Config;
using VoiceAssistant.App.Core; // Assuming TextNormalizer is here
using VoiceAssistant.App.Whisper;

namespace VoiceAssistant.App.Wake
{
    public class LocalWhisperWakeWordDetector : IWakeWordDetector
    {
        private readonly IWhisperEngine _whisper;
        private readonly WakeConfig _config;
        private readonly TextNormalizer _normalizer;

        public LocalWhisperWakeWordDetector(IWhisperEngine whisper, WakeConfig config)
        {
            _whisper = whisper;
            _config = config;
            _normalizer = new TextNormalizer();
        }

        public async Task<WakeResult> DetectAsync(byte[] audioData)
        {
            // Convert byte[] PCM 16-bit to float[] for Whisper
            var floats = BytesToFloats(audioData);

            var text = await _whisper.TranscribeAsync(floats);
            var normalized = _normalizer.Normalize(text);

            if (_config.LogWakeText && !string.IsNullOrWhiteSpace(normalized))
            {
               Log.Debug($"Wake check: '{normalized}'");
            }

            // Check StartsWith
            bool detected = normalized.StartsWith(_config.RequiresStartsWith.ToLower());

            return new WakeResult 
            { 
                IsWake = detected, 
                Text = normalized 
            };
        }

        private float[] BytesToFloats(byte[] bytes)
        {
            var floats = new float[bytes.Length / 2];
            for (int i = 0; i < bytes.Length; i += 2)
            {
                short sample = BitConverter.ToInt16(bytes, i);
                floats[i / 2] = sample / 32768f;
            }
            return floats;
        }
    }
}
