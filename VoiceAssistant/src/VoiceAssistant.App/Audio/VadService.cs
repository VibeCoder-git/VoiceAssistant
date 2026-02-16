using System;
using VoiceAssistant.App.Config;

namespace VoiceAssistant.App.Audio
{
    public class VadService
    {
        private readonly AudioConfig _config;

        private bool _isSpeaking;
        private DateTime _lastSpeechTime;

        public event EventHandler SpeechStarted;
        public event EventHandler SpeechEnded;

        public bool IsSpeaking => _isSpeaking;

        public VadService(AudioConfig config)
        {
            _config = config;
        }

        public void ProcessChunk(AudioChunk chunk)
        {
            if (chunk?.Data == null || chunk.Data.Length < 2)
                return;

            double energy = CalculateRms(chunk.Data);

            // Threshold читаем из конфига каждый раз — чтобы изменения в рантайме работали сразу
            bool active = energy > _config.VadThreshold;

            if (active)
            {
                if (!_isSpeaking)
                {
                    _isSpeaking = true;
                    SpeechStarted?.Invoke(this, EventArgs.Empty);
                }
                _lastSpeechTime = DateTime.UtcNow;
            }
            else
            {
                if (_isSpeaking)
                {
                    if ((DateTime.UtcNow - _lastSpeechTime).TotalMilliseconds > _config.VadSilenceMs)
                    {
                        _isSpeaking = false;
                        SpeechEnded?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
        }

        private static double CalculateRms(byte[] buffer)
        {
            // 16-bit PCM => 2 bytes per sample
            int sampleCount = buffer.Length / 2;
            if (sampleCount <= 0) return 0;

            double sum = 0;

            // читаем только полные семплы, игнорируя хвостовой 1 байт (если вдруг прилетел)
            int len = sampleCount * 2;

            for (int i = 0; i < len; i += 2)
            {
                short sample = BitConverter.ToInt16(buffer, i);
                double normalized = sample / 32768.0;
                sum += normalized * normalized;
            }

            return Math.Sqrt(sum / sampleCount);
        }
    }
}
