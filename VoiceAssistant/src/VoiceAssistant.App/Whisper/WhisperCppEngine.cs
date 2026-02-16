using System;
using System.IO;
using System.Threading.Tasks;
using Serilog;
using VoiceAssistant.App.Config;
using Whisper.net;
using Whisper.net.Ggml;

namespace VoiceAssistant.App.Whisper
{
    public class WhisperCppEngine : IWhisperEngine
    {
        private readonly WakeConfig _config;
        private WhisperFactory _factory;
        private WhisperProcessor _processor;
        private readonly string _modelPath;

        public WhisperCppEngine(WakeConfig config)
        {
            _config = config;
            string modelName = $"ggml-{_config.WakeModel}.bin";
             _modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Whisper", "Models", modelName);
        }

        public async Task InitializeAsync()
        {
            if (!File.Exists(_modelPath))
            {
                Log.Information($"Whisper model not found at {_modelPath}. Downloading {_config.WakeModel}...");
                await DownloadModelAsync();
            }

            try 
            {
                _factory = WhisperFactory.FromPath(_modelPath);
                
                // Create processor once
                _processor = _factory.CreateBuilder()
                    .WithLanguage("ru")
                    .Build();
                    
                Log.Information("Whisper engine initialized.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize Whisper engine.");
                throw;
            }
        }

        private async Task DownloadModelAsync()
        {
            // Map config string to GgmlType enum
            GgmlType type = GgmlType.Tiny;
            if (Enum.TryParse<GgmlType>(_config.WakeModel, true, out var parsed))
            {
                type = parsed;
            }

            Log.Information($"Downloading Whisper model to {_modelPath}...");
                    
            var dir = Path.GetDirectoryName(_modelPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            using var modelStream = await WhisperGgmlDownloader.GetGgmlModelAsync(type); // Using 'type' as derived from _config.WakeModel
            using var fileStream = File.OpenWrite(_modelPath);
            await modelStream.CopyToAsync(fileStream);
            Log.Information("Model downloaded successfully.");
        }

        public async Task<string> TranscribeAsync(float[] audioData)
        {
            if (_processor == null) return string.Empty;

            try
            {
                 // Whisper.net processes floats directly
                 var resultText = "";
                 
                 await foreach (var segment in _processor.ProcessAsync(audioData))
                 {
                     resultText += segment.Text;
                 }

                 return resultText.Trim();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Transcribe failed");
                return string.Empty;
            }
        }

        public void Dispose()
        {
            _processor?.Dispose();
            _factory?.Dispose();
        }
    }
}
