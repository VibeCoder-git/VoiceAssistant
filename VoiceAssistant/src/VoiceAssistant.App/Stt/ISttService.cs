using System;
using System.Threading.Tasks;

namespace VoiceAssistant.App.Stt
{
    public sealed class TranscriptEvent : EventArgs
    {
        public string Text { get; init; } = "";
        public bool IsFinal { get; init; }
        public float Confidence { get; init; }
    }

    public interface ISttService : IDisposable
    {
        event EventHandler<TranscriptEvent> TranscriptReceived;
        event EventHandler<string> Error;

        Task StartStreamAsync();
        Task StopStreamAsync();
        Task SendAudioAsync(byte[] data);
    }
}
