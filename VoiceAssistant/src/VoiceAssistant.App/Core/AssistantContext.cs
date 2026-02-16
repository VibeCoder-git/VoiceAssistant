using VoiceAssistant.App.Audio;
using VoiceAssistant.App.Config;
using VoiceAssistant.App.Skills;
using VoiceAssistant.App.Stt;
using VoiceAssistant.App.Tray;
using VoiceAssistant.App.Wake;

namespace VoiceAssistant.App.Core
{
    public class AssistantContext
    {
        public AppConfig Config { get; set; }
        public AudioCaptureService AudioCapture { get; set; }
        public AudioResampler Resampler { get; set; } // May not be needed if capture handles it
        public VadService Vad { get; set; }
        public RingBuffer RingBuffer { get; set; }
        public IWakeWordDetector WakeDetector { get; set; }
        public ISttService Stt { get; set; }
        public SkillRegistry Skills { get; set; }
        public CommandRouter Router { get; set; }
        public TrayMenu TrayMenu { get; set; }
    }
}
