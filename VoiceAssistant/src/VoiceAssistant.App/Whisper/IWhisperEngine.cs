using System;
using System.Threading.Tasks;
using VoiceAssistant.App.Audio;

namespace VoiceAssistant.App.Whisper
{
    public interface IWhisperEngine : IDisposable
    {
        Task InitializeAsync();
        Task<string> TranscribeAsync(float[] audioData);
    }
}
