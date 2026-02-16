using Google.Apis.Auth.OAuth2;
using Google.Cloud.Speech.V1;
using Grpc.Auth;
using Grpc.Core;
using Serilog;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using VoiceAssistant.App.Config;

namespace VoiceAssistant.App.Stt
{
    public sealed class GoogleStreamingSttService : ISttService
    {
        private readonly AppConfig _config;

        private SpeechClient? _speechClient;
        private Channel? _channel;

        private readonly object _lock = new object();
        private SpeechClient.StreamingRecognizeStream? _call;
        private CancellationTokenSource? _cts;
        private Task? _responseTask;
        private bool _isStreaming;

        public event EventHandler<TranscriptEvent>? TranscriptReceived;
        public event EventHandler<string>? Error;

        public GoogleStreamingSttService(AppConfig config)
        {
            _config = config;
        }

        private async Task EnsureClientAsync()
        {
            if (_speechClient != null) return;

            var credentialPath = FindCredentialFile();
            if (string.IsNullOrWhiteSpace(credentialPath))
                throw new FileNotFoundException("Google credentials (google-stt.json) not found.");

            Log.Information("Using Google credentials: {Path}", credentialPath);

            var credential = GoogleCredential.FromFile(credentialPath);
            _channel = new Channel(
                SpeechClient.DefaultEndpoint.Host,
                SpeechClient.DefaultEndpoint.Port,
                credential.ToChannelCredentials()
            );

            _speechClient = await SpeechClient.CreateAsync(_channel);
        }

        private string? FindCredentialFile()
        {
            // 1) рядом с exe
            var local = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "google-stt.json");
            if (File.Exists(local)) return local;

            // 2) AppData (пользовательский)
            var appData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "JarvisAssistant",
                "google-stt.json"
            );
            if (File.Exists(appData)) return appData;

            // 3) путь из конфига (опционально)
            if (!string.IsNullOrWhiteSpace(_config.Google.CredentialsOptionalPath) &&
                File.Exists(_config.Google.CredentialsOptionalPath))
            {
                return _config.Google.CredentialsOptionalPath;
            }

            return null;
        }

        public async Task StartStreamAsync()
        {
            await EnsureClientAsync();

            SpeechClient.StreamingRecognizeStream localCall;
            CancellationToken token;

            lock (_lock)
            {
                if (_isStreaming) return;

                _isStreaming = true;

                _cts = new CancellationTokenSource();
                token = _cts.Token;

                _call = _speechClient!.StreamingRecognize();
                localCall = _call;
            }

            try
            {
                // конфиг стрима
                await localCall.WriteAsync(new StreamingRecognizeRequest
                {
                    StreamingConfig = new StreamingRecognitionConfig
                    {
                        Config = new RecognitionConfig
                        {
                            Encoding = RecognitionConfig.Types.AudioEncoding.Linear16,
                            SampleRateHertz = 16000,
                            LanguageCode = _config.Google.LanguageCode,
                            EnableAutomaticPunctuation = false
                        },
                        InterimResults = true,
                        SingleUtterance = false
                    }
                });

                // читаем ответы в фоне (важно: используем localCall, а не поле _call)
                _responseTask = Task.Run(() => ResponseLoopAsync(localCall, token), token);

                Log.Information("Google STT stream started.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to start Google STT stream");

                // откат состояния
                lock (_lock)
                {
                    _isStreaming = false;
                    _call = null;

                    try { _cts?.Cancel(); } catch { }
                    _cts?.Dispose();
                    _cts = null;
                }

                Error?.Invoke(this, ex.Message);
            }
        }

        private async Task ResponseLoopAsync(SpeechClient.StreamingRecognizeStream call, CancellationToken token)
        {
            try
            {
                while (await call.ResponseStream.MoveNext(token))
                {
                    var response = call.ResponseStream.Current;

                    foreach (var result in response.Results)
                    {
                        if (result.Alternatives.Count == 0) continue;

                        var alt = result.Alternatives[0];

                        TranscriptReceived?.Invoke(this, new TranscriptEvent
                        {
                            Text = alt.Transcript ?? "",
                            IsFinal = result.IsFinal,
                            Confidence = alt.Confidence
                        });
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // ожидаемо при Stop()
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
            {
                // ожидаемо при Stop()
            }
            catch (Exception ex)
            {
                bool active;
                lock (_lock) active = _isStreaming;

                if (active)
                {
                    Log.Error(ex, "Google STT response loop error");
                    Error?.Invoke(this, ex.Message);
                }
            }
        }

        public async Task SendAudioAsync(byte[] data)
        {
            if (data == null || data.Length == 0) return;

            SpeechClient.StreamingRecognizeStream? call;

            lock (_lock)
            {
                if (!_isStreaming) return;
                call = _call;
            }

            if (call == null) return;

            try
            {
                await call.WriteAsync(new StreamingRecognizeRequest
                {
                    AudioContent = Google.Protobuf.ByteString.CopyFrom(data)
                });
            }
            catch (Exception ex)
            {
                // не валим всё сразу — пусть state machine решает
                Log.Error(ex, "Error sending audio to Google");
            }
        }

        public async Task StopStreamAsync()
        {
            SpeechClient.StreamingRecognizeStream? call;
            CancellationTokenSource? cts;
            Task? responseTask;

            lock (_lock)
            {
                if (!_isStreaming) return;

                _isStreaming = false;

                call = _call;
                cts = _cts;
                responseTask = _responseTask;

                _call = null;
                _cts = null;
                _responseTask = null;
            }

            try { cts?.Cancel(); } catch { }

            if (call != null)
            {
                try { await call.WriteCompleteAsync(); }
                catch { /* ignore */ }
            }

            // best-effort дождаться завершения чтения (без подвисаний)
            if (responseTask != null)
            {
                try { await Task.WhenAny(responseTask, Task.Delay(500)); }
                catch { /* ignore */ }
            }

            cts?.Dispose();

            Log.Information("Google STT stream stopped.");
        }

        public void Dispose()
        {
            // Важно: Dispose не должен фризить UI — делаем best-effort в фоне
            Task.Run(async () =>
            {
                try { await StopStreamAsync(); } catch { }
                try
                {
                    if (_channel != null)
                    {
                        await _channel.ShutdownAsync();
                        _channel = null;
                    }
                }
                catch { /* ignore */ }
            });
        }
    }
}
