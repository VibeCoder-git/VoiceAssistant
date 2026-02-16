using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Serilog;
using VoiceAssistant.App.Actions;
using VoiceAssistant.App.Audio;
using VoiceAssistant.App.Config;
using VoiceAssistant.App.Skills;
using VoiceAssistant.App.Stt;
using VoiceAssistant.App.Wake;

namespace VoiceAssistant.App.Core
{
    public class AssistantStateMachine : IDisposable
    {
        private readonly AssistantContext _ctx;
        private readonly Dictionary<string, IActionExecutor> _executors;
        
        private AssistantState _currentState = AssistantState.IDLE;
        private DateTime _stateEnterTime = DateTime.UtcNow;
        private readonly SemaphoreSlim _transitionLock = new SemaphoreSlim(1, 1);

        // Streaming
        private Channel<byte[]> _audioChannel;
        private CancellationTokenSource _audioCts;

        // Logic & Timers
        private CancellationTokenSource _silenceCts;
        private bool _wakeCheckedInCurrentSpeech;
        private bool _wakeConfirmed; 

        public AssistantState CurrentState => _currentState;

        public AssistantStateMachine(AssistantContext ctx, List<IActionExecutor> executors)
        {
            _ctx = ctx;
            _executors = new Dictionary<string, IActionExecutor>();
            foreach (var ex in executors)
            {
                _executors[ex.ActionType] = ex;
            }
        }

        public void Start()
        {
            _ctx.AudioCapture.AudioCaptured += OnAudioCaptured;
            _ctx.Vad.SpeechStarted += OnSpeechStarted;
            _ctx.Vad.SpeechEnded += OnSpeechEnded;
            _ctx.Stt.TranscriptReceived += OnSttTranscript;
            _ctx.Stt.Error += OnSttError;

            _ctx.AudioCapture.Start();
            Log.Information("Assistant Started. Listening...");
            UpdateTrayStatus();
        }

        // --- Event Handlers ---

        private void OnAudioCaptured(object sender, AudioChunk chunk)
        {
            // 1. Buffer & Process
            var pcm = chunk.Data;
            _ctx.RingBuffer.Add(pcm);
            _ctx.Vad.ProcessChunk(chunk); 

            // 2. Stream if ACTIVE
            if (_currentState == AssistantState.ACTIVE)
            {
                _audioChannel?.Writer.TryWrite(pcm);
            }
        }

        private void OnSpeechStarted(object sender, EventArgs e)
        {
            Log.Debug("Speech Started (VAD)");
            
            _silenceCts?.Cancel();
            _silenceCts = null;

            _wakeCheckedInCurrentSpeech = false;

            if (_currentState == AssistantState.IDLE)
            {
                _ = Task.Run(async () => 
                {
                    try 
                    {
                        await Task.Delay(_ctx.Config.Wake.WakeWindowMs);
                        if (_currentState == AssistantState.IDLE)
                        {
                            await CheckWakeWordAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error in delayed wake check");
                    }
                });
            }
        }

        private void OnSpeechEnded(object sender, EventArgs e)
        {
            Log.Debug("Speech Ended (VAD)");
            
            if (_currentState == AssistantState.ACTIVE)
            {
                _silenceCts?.Cancel();
                _silenceCts = new CancellationTokenSource();
                var token = _silenceCts.Token;

                _ = Task.Run(async () => 
                {
                    try
                    {
                        await Task.Delay(_ctx.Config.Timing.SilenceStopMs, token);
                        if (token.IsCanceled) return;

                        Log.Information("Silence timeout (ACTIVE). Stopping stream.");
                        await TransitionToAsync(AssistantState.IDLE);
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error in silence timeout");
                    }
                });
            }
        }

        private async void OnSttTranscript(object sender, TranscriptEvent e)
        {
            try
            {
                 await ProcessTranscriptAsync(e);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing transcript");
            }
        }

        private void OnSttError(object sender, string error)
        {
            Log.Error($"STT Error received: {error}");
            // Optional: Transition to IDLE if critical, but for now just log
        }

        // --- Core Logic ---

        private async Task CheckWakeWordAsync()
        {
            if (_currentState != AssistantState.IDLE) return;
            if (_wakeCheckedInCurrentSpeech) return;

            var window = _ctx.RingBuffer.GetLast(_ctx.Config.Wake.WakeWindowMs);
            if (window.Length == 0) return;

            _wakeCheckedInCurrentSpeech = true;

            bool isWake = false;
            long elapsed = 0;

            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var result = await _ctx.WakeDetector.DetectAsync(window);
                sw.Stop();
                elapsed = sw.ElapsedMilliseconds;
                isWake = result.IsWake;
                if (isWake) Log.Information($"[TIMING] Wake Detected: '{result.Text}' in {elapsed}ms");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Wake detection failed");
            }

            if (isWake)
            {
                await TransitionToAsync(AssistantState.ACTIVE);
            }
        }

        private async Task ProcessTranscriptAsync(TranscriptEvent e)
        {
            if (_currentState != AssistantState.ACTIVE) return;

            var norm = _ctx.Router.Normalize(e.Text);
            
            // Unify normalization: use Router to normalize the config string too
            var wakeWord = _ctx.Router.Normalize(_ctx.Config.Wake.RequiresStartsWith); 

            // 1. Anti-False Positive (Continuous Check until Confirmed)
            if (!_wakeConfirmed)
            {
                // Timeout check (3.0s max to confirm wake word for resilience)
                if ((DateTime.UtcNow - _stateEnterTime).TotalSeconds > 3.0)
                {
                     Log.Warning("[ANTI-FALSE] Wake confirmation timeout. Aborting.");
                     await TransitionToAsync(AssistantState.IDLE);
                     return;
                }

                // Ignore very short noise (0-1 chars) unless wake word itself is short
                if (norm.Length <= 1 && wakeWord.Length > 1) return;

                if (norm.StartsWith(wakeWord))
                {
                    _wakeConfirmed = true;
                    Log.Information($"[ANTI-FALSE] Wake confirmed: '{norm}'");
                    // Continue to routing...
                }
                else if (wakeWord.StartsWith(norm))
                {
                    // Partial match (e.g. "джар")
                    Log.Debug($"[ANTI-FALSE] Partial wake: '{norm}'. Waiting...");
                    return; 
                }
                else
                {
                    // Mismatch (e.g. "привет")
                    Log.Warning($"[ANTI-FALSE] Mismatch: '{norm}' vs '{wakeWord}'. Aborting.");
                    await TransitionToAsync(AssistantState.IDLE);
                    return;
                }
            }

            // 2. Routing
            var (skill, action) = _ctx.Router.Route(e.Text);

            if (skill != null)
            {
                var latency = (DateTime.UtcNow - _stateEnterTime).TotalMilliseconds;
                Log.Information($"[TIMING] Command Matched: '{skill.Id}' in {latency}ms. Action: {action.Type}");
                await ExecuteCommandAsync(skill, action);
            }
        }

        private async Task ExecuteCommandAsync(SkillManifest skill, SkillAction action)
        {
            await TransitionToAsync(AssistantState.EXECUTE);

            // Execute Action
            if (_executors.TryGetValue(action.Type, out var executor))
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var result = await executor.ExecuteAsync(action.Args);
                sw.Stop();
                Log.Information($"[TIMING] Action Executed: {result.Success} in {sw.ElapsedMilliseconds}ms");
            }

            // Play Sound
            if (!string.IsNullOrEmpty(skill.ResponseSound))
            {
                 await PlaySoundAsync(skill.ResponseSound);
            }

            await TransitionToAsync(AssistantState.COOLDOWN);
        }

        private async Task TransitionToAsync(AssistantState newState)
        {
            await _transitionLock.WaitAsync();
            try
            {
                if (_currentState == newState) return;

                Log.Information($"State Transition: {_currentState} -> {newState}");
                
                if (_currentState == AssistantState.ACTIVE)
                {
                    await StopActiveSessionAsync();
                }

                _currentState = newState;
                _stateEnterTime = DateTime.UtcNow;
                
                _ = Task.Run(() => UpdateTrayStatus());

                switch (newState)
                {
                    case AssistantState.ACTIVE:
                        await StartActiveSessionAsync();
                        break;
                    
                    case AssistantState.IDLE:
                        _silenceCts?.Cancel(); 
                        break;
                    
                    case AssistantState.COOLDOWN:
                         _ = Task.Run(async () => 
                         {
                             await Task.Delay(_ctx.Config.Timing.CooldownAfterExecuteMs);
                             await TransitionToAsync(AssistantState.IDLE);
                         });
                        break;
                }
            }
            finally
            {
                _transitionLock.Release();
            }
        }

        private async Task StartActiveSessionAsync()
        {
             Log.Information($"[TIMING] Stream Start requested");
             _wakeConfirmed = false; // Reset for new session

             var options = new BoundedChannelOptions(50) { FullMode = BoundedChannelFullMode.DropOldest };
             _audioChannel = Channel.CreateBounded<byte[]>(options);
             _audioCts = new CancellationTokenSource();
             
             await _ctx.Stt.StartStreamAsync();
             
             _ = Task.Run(async () => await ProcessAudioQueueAsync(_audioChannel.Reader, _audioCts.Token));

             var preRoll = _ctx.RingBuffer.GetLast(_ctx.Config.Timing.PreRollSendMs);
             if (preRoll.Length > 0)
             {
                 _audioChannel.Writer.TryWrite(preRoll);
             }
        }

        private async Task StopActiveSessionAsync()
        {
            _silenceCts?.Cancel();
            _audioCts?.Cancel();
            _audioChannel?.Writer.TryComplete();
            await _ctx.Stt.StopStreamAsync();
        }

        private async Task ProcessAudioQueueAsync(ChannelReader<byte[]> reader, CancellationToken ct)
        {
            try
            {
                while (await reader.WaitToReadAsync(ct))
                {
                    while (reader.TryRead(out var data))
                    {
                        await _ctx.Stt.SendAudioAsync(data);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in audio sending loop");
            }
        }

        private async Task PlaySoundAsync(string path)
        {
             await Task.Run(() => 
             {
                 try
                 {
                     string fullPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
                     if (System.IO.File.Exists(fullPath))
                     {
                         using var audioFile = new NAudio.Wave.AudioFileReader(fullPath);
                         using var outputDevice = new NAudio.Wave.WaveOutEvent();
                         outputDevice.Init(audioFile);
                         
                         var tcs = new TaskCompletionSource<bool>();
                         outputDevice.PlaybackStopped += (s,e) => tcs.TrySetResult(true);
                         outputDevice.Play();
                         
                         if (!tcs.Task.Wait(TimeSpan.FromSeconds(5))) 
                         {
                             outputDevice.Stop();
                         }
                     }
                 }
                 catch(Exception ex) { Log.Error(ex, "Sound playback failed"); }
             });
        }

        private void UpdateTrayStatus()
        {
            try { _ctx.TrayMenu?.UpdateStatus(_currentState.ToString()); } catch {}
        }

        public void Dispose()
        {
            if (_ctx == null) return;

             // Full unsubscribe
             if (_ctx.AudioCapture != null) _ctx.AudioCapture.AudioCaptured -= OnAudioCaptured;
             if (_ctx.Vad != null)
             {
                 _ctx.Vad.SpeechStarted -= OnSpeechStarted;
                 _ctx.Vad.SpeechEnded -= OnSpeechEnded;
             }
             if (_ctx.Stt != null)
             {
                 _ctx.Stt.TranscriptReceived -= OnSttTranscript;
                 _ctx.Stt.Error -= OnSttError;
             }

             _audioCts?.Cancel();
             _silenceCts?.Cancel();
             _transitionLock?.Dispose();
        }
    }
}
