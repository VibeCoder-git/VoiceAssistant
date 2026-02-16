using System;

namespace VoiceAssistant.App.Config
{
    public class AppConfig
    {
        public GoogleConfig Google { get; set; } = new();
        public AudioConfig Audio { get; set; } = new();
        public WakeConfig Wake { get; set; } = new();
        public TimingConfig Timing { get; set; } = new();
        public SkillsConfig Skills { get; set; } = new();
        public LoggingConfig Logging { get; set; } = new();
        public AppGeneralConfig App { get; set; } = new();
    }

    public class GoogleConfig
    {
        public string LanguageCode { get; set; } = "ru-RU";
        public string CredentialsOptionalPath { get; set; } = "";
    }

    public class AudioConfig
    {
        public string PreferredInputDeviceId { get; set; } = "";
        public string PreferredInputDeviceName { get; set; } = "";
        public int SampleRate { get; set; } = 16000;
        public string AudioMode { get; set; } = "Headphones"; // Headphones | Speakers
        public double VadThreshold { get; set; } = 0.01;
        public int VadSilenceMs { get; set; } = 500;
    }

    public class WakeConfig
    {
        public string WakeModel { get; set; } = "tiny";
        public int WakeWindowMs { get; set; } = 1200;
        public string RequiresStartsWith { get; set; } = "джарвис";
        public bool LogWakeText { get; set; } = true;
    }

    public class TimingConfig
    {
        public int RingBufferMs { get; set; } = 2500;
        public int PreRollSendMs { get; set; } = 2000;
        public int CommandMaxDurationMs { get; set; } = 6000;
        public int SilenceStopMs { get; set; } = 900;
        public int CooldownAfterExecuteMs { get; set; } = 500;
        public int ChunkMs { get; set; } = 50;
    }

    public class SkillsConfig
    {
        public string Directory { get; set; } = "skills";
    }

    public class LoggingConfig
    {
        public string LogDirectory { get; set; } = "logs";
        public bool LogTranscripts { get; set; } = true;
    }

    public class AppGeneralConfig
    {
        public bool AutoStart { get; set; } = true;
    }
}
