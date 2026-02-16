using NAudio.CoreAudioApi;
using NAudio.Wave;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using VoiceAssistant.App.Config;

namespace VoiceAssistant.App.Audio
{
    public class AudioDevice
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }

    public class AudioCaptureService : IDisposable
    {
        private WasapiCapture _capture;
        private readonly AudioConfig _config;
        private readonly AudioResampler _resampler;

        public event EventHandler<AudioChunk> AudioCaptured;
        public event EventHandler<string> OnError;

        private bool _isRecording;
        private readonly MMDeviceEnumerator _enumerator;

        public AudioCaptureService(AudioConfig config)
        {
            _config = config;
            _resampler = new AudioResampler();
            _enumerator = new MMDeviceEnumerator();
        }

        public List<AudioDevice> GetInputDevices()
        {
            var devices = new List<AudioDevice>();
            try
            {
                foreach (var endpoint in _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
                {
                    devices.Add(new AudioDevice { Id = endpoint.ID, Name = endpoint.FriendlyName });
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error enumerating devices");
            }
            return devices;
        }

        public void SetInputDevice(string deviceId)
        {
            if (string.IsNullOrWhiteSpace(deviceId)) return;

            // Restart only if реально меняем устройство
            bool needRestart = _isRecording && !string.Equals(_config.PreferredInputDeviceId, deviceId, StringComparison.Ordinal);

            Log.Information($"Switching input device to ID: {deviceId}");
            _config.PreferredInputDeviceId = deviceId;

            // Update Name for persistence/UI
            try
            {
                var dev = _enumerator.GetDevice(deviceId);
                _config.PreferredInputDeviceName = dev.FriendlyName;
            }
            catch { /* ignore */ }

            if (needRestart)
            {
                Stop();
                Start();
            }
        }

        public void Start()
        {
            if (_isRecording) return;

            WasapiCapture localCapture = null;

            try
            {
                var device = GetDevice();
                if (device == null) throw new Exception("No input device found.");

                // Update config with actual used device
                _config.PreferredInputDeviceId = device.ID;
                _config.PreferredInputDeviceName = device.FriendlyName;

                localCapture = new WasapiCapture(device)
                {
                    ShareMode = AudioClientShareMode.Shared
                };

                localCapture.DataAvailable += OnDataAvailable;
                localCapture.RecordingStopped += OnRecordingStopped;

                localCapture.StartRecording();

                // Publish only after successful start
                _capture = localCapture;
                localCapture = null;

                _isRecording = true;
                Log.Information($"Started recording from device: {device.FriendlyName} ({_capture.WaveFormat})");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to start audio capture.");
                OnError?.Invoke(this, ex.Message);

                // Cleanup partial capture if created
                try
                {
                    if (localCapture != null)
                    {
                        localCapture.DataAvailable -= OnDataAvailable;
                        localCapture.RecordingStopped -= OnRecordingStopped;
                        localCapture.Dispose();
                    }
                }
                catch { /* ignore */ }

                // Also cleanup stored capture if something odd happened
                try
                {
                    if (_capture != null)
                    {
                        _capture.DataAvailable -= OnDataAvailable;
                        _capture.RecordingStopped -= OnRecordingStopped;
                        _capture.Dispose();
                        _capture = null;
                    }
                }
                catch { /* ignore */ }

                _isRecording = false;
            }
        }

        public void Stop()
        {
            // Stop должен чистить даже если _isRecording == false, но _capture не null
            var cap = _capture;
            if (cap == null)
            {
                _isRecording = false;
                return;
            }

            try
            {
                cap.DataAvailable -= OnDataAvailable;
                cap.RecordingStopped -= OnRecordingStopped;

                try { cap.StopRecording(); } catch { /* ignore */ }

                cap.Dispose();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error stopping capture");
            }
            finally
            {
                _capture = null;
                _isRecording = false;
            }
        }

        private MMDevice GetDevice()
        {
            // 1) Try ID
            if (!string.IsNullOrEmpty(_config.PreferredInputDeviceId))
            {
                try
                {
                    return _enumerator.GetDevice(_config.PreferredInputDeviceId);
                }
                catch
                {
                    Log.Warning($"Preferred device ID '{_config.PreferredInputDeviceId}' not found. Falling back.");
                }
            }

            // 2) Try Name
            if (!string.IsNullOrEmpty(_config.PreferredInputDeviceName))
            {
                try
                {
                    var devices = _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
                    var match = devices.FirstOrDefault(d => string.Equals(d.FriendlyName, _config.PreferredInputDeviceName, StringComparison.Ordinal));
                    if (match != null)
                    {
                        Log.Information($"Found device by name: {match.FriendlyName}");
                        return match;
                    }
                }
                catch { /* ignore */ }
            }

            // 3) Default Comm
            var comm = _enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
            if (comm != null) return comm;

            // 4) Default Console
            return _enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Console);
        }

        private void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            if (e.BytesRecorded <= 0) return;

            // Race-guard
            var cap = _capture;
            if (cap == null) return;

            try
            {
                var resampled = _resampler.Resample(e.Buffer, 0, e.BytesRecorded, cap.WaveFormat);
                var chunk = new AudioChunk(resampled);
                AudioCaptured?.Invoke(this, chunk);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing audio data");
            }
        }

        private void OnRecordingStopped(object sender, StoppedEventArgs e)
        {
            Log.Information("Recording stopped.");
            if (e.Exception != null)
            {
                Log.Error(e.Exception, "Recording stopped with error");
                OnError?.Invoke(this, e.Exception.Message);
            }
        }

        public void Dispose()
        {
            Stop();
            _enumerator?.Dispose();
        }
    }
}
