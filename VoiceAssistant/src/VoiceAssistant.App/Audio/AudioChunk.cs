using System;

namespace VoiceAssistant.App.Audio
{
    public sealed class AudioChunk
    {
        public byte[] Data { get; }
        public DateTime TimestampUtc { get; }

        public AudioChunk(byte[] data)
        {
            Data = data ?? Array.Empty<byte>();
            TimestampUtc = DateTime.UtcNow;
        }
    }
}
