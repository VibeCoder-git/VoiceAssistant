using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.IO;

namespace VoiceAssistant.App.Audio
{
    public class AudioResampler
    {
        private readonly WaveFormat _targetFormat;

        public AudioResampler()
        {
            // Fixed target: 16kHz, Mono, 16-bit
            _targetFormat = new WaveFormat(16000, 16, 1);
        }

        public byte[] Resample(byte[] inputData, WaveFormat inputFormat)
        {
            return Resample(inputData, 0, inputData.Length, inputFormat);
        }

        public byte[] Resample(byte[] buffer, int offset, int count, WaveFormat inputFormat)
        {
            if (count == 0 || buffer == null) return Array.Empty<byte>();

            // Always return a COPY, even if format matches, to prevent buffer reuse issues upstream
            if (inputFormat.SampleRate == 16000 && inputFormat.Channels == 1 && inputFormat.BitsPerSample == 16)
            {
                var copy = new byte[count];
                Array.Copy(buffer, offset, copy, 0, count);
                return copy;
            }

            using var inputStream = new RawSourceWaveStream(buffer, offset, count, inputFormat);
            
            ISampleProvider resampler = inputStream.ToSampleProvider();

            if (inputFormat.SampleRate != 16000)
            {
                resampler = new WdlResamplingSampleProvider(resampler, 16000);
            }

            if (inputFormat.Channels != 1)
            {
                resampler = resampler.ToMono();
            }

            var provider16 = resampler.ToWaveProvider16();
            
            using var outStream = new MemoryStream();
            var outBuffer = new byte[4096];
            int read;
            while ((read = provider16.Read(outBuffer, 0, outBuffer.Length)) > 0)
            {
                outStream.Write(outBuffer, 0, read);
            }

            return outStream.ToArray();
        }
    }
}
