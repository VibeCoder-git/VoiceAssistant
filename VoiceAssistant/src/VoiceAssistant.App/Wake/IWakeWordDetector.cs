using System.Threading.Tasks;
using VoiceAssistant.App.Audio;

namespace VoiceAssistant.App.Wake
{
    public interface IWakeWordDetector
    {
        Task<WakeResult> DetectAsync(byte[] audioData);
    }
}
