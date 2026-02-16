using System;

namespace VoiceAssistant.App.Audio
{
    public class RingBuffer
    {
        private readonly int _bufferSize;
        private readonly byte[] _buffer;
        private int _position;
        private int _available;
        private readonly object _lock = new object();

        // 16kHz 16bit mono = 32000 bytes/sec
        private const int BytesPerSecond = 32000;
        private const int MinBufferBytes = 2; // at least 1 sample (16-bit)

        public RingBuffer(int capacityMs)
        {
            long size = (long)capacityMs * BytesPerSecond / 1000;
            if (size < MinBufferBytes) size = MinBufferBytes;

            // Align to 16-bit samples (even number of bytes)
            if ((size & 1) == 1) size++;

            _bufferSize = (int)size;
            _buffer = new byte[_bufferSize];
            _position = 0;
            _available = 0;
        }

        // Main API (в проекте используется Add)
        public void Add(byte[] data)
        {
            if (data == null || data.Length == 0) return;

            lock (_lock)
            {
                int len = data.Length;
                int offset = 0;

                // If chunk bigger than buffer -> keep only the tail that fits
                if (len > _bufferSize)
                {
                    offset = len - _bufferSize;
                    len = _bufferSize;
                }

                int bytesToWrite = len;

                while (bytesToWrite > 0)
                {
                    int chunk = Math.Min(bytesToWrite, _bufferSize - _position);
                    Buffer.BlockCopy(data, offset, _buffer, _position, chunk);

                    _position = (_position + chunk) % _bufferSize;
                    offset += chunk;
                    bytesToWrite -= chunk;
                }

                _available = Math.Min(_available + len, _bufferSize);
            }
        }

        // Backward-compat alias (если где-то осталось Write)
        public void Write(byte[] data) => Add(data);

        public byte[] GetLast(int ms)
        {
            if (ms <= 0) return Array.Empty<byte>();

            lock (_lock)
            {
                int bytesRequested = (int)((long)ms * BytesPerSecond / 1000);
                if (bytesRequested <= 0) return Array.Empty<byte>();

                // Align to 16-bit samples
                if ((bytesRequested & 1) == 1) bytesRequested--;

                int bytesToRead = Math.Min(bytesRequested, _available);
                if (bytesToRead <= 0) return Array.Empty<byte>();

                var result = new byte[bytesToRead];

                int startPos = _position - bytesToRead;
                if (startPos < 0) startPos += _bufferSize;

                int firstChunk = Math.Min(bytesToRead, _bufferSize - startPos);
                Buffer.BlockCopy(_buffer, startPos, result, 0, firstChunk);

                int secondChunk = bytesToRead - firstChunk;
                if (secondChunk > 0)
                {
                    Buffer.BlockCopy(_buffer, 0, result, firstChunk, secondChunk);
                }

                return result;
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _available = 0;
                _position = 0;
            }
        }
    }
}
