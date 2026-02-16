namespace VoiceAssistant.App.Wake
{
    public class WakeResult
    {
        public bool IsWake { get; set; }
        public string Text { get; set; }
        public double Confidence { get; set; }
    }
}
