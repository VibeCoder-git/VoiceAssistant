using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Serilog;
using VoiceAssistant.App.Actions;
using VoiceAssistant.App.Audio;
using VoiceAssistant.App.Config;
using VoiceAssistant.App.Core;
using VoiceAssistant.App.Logging;
using VoiceAssistant.App.Skills;
using VoiceAssistant.App.Stt;
using VoiceAssistant.App.Tray;
using VoiceAssistant.App.Wake;
using VoiceAssistant.App.Whisper;

namespace VoiceAssistant.App
{
    static class Program
    {
        private static AssistantStateMachine _stateMachine;
        private static AssistantContext _context;

        [STAThread]
        static void Main()
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // 1. Load Config & Settings
            var config = ConfigLoader.Load();
            UserSettingsStore.LoadAndApply(config);
            
            LogSetup.Initialize(config.Logging);

            try
            {
                Log.Information("Initializing Jarvis Voice Assistant components...");

                // 2. Build Context Services
                _context = new AssistantContext
                {
                    Config = config,
                    RingBuffer = new RingBuffer(config.Timing.RingBufferMs),
                    AudioCapture = new AudioCaptureService(config.Audio),
                    Resampler = new AudioResampler(),
                    Vad = new VadService(config.Audio),
                    Skills = new SkillRegistry(),
                    Stt = new GoogleStreamingSttService(config)
                };

                // 3. Load Skills
                var skillLoader = new SkillLoader(config.Skills, _context.Skills);
                skillLoader.LoadSkills();

                // 4. Initialize Wake Engine
                var whisperEngine = new WhisperCppEngine(config.Wake);
                whisperEngine.InitializeAsync().Wait();
                _context.WakeDetector = new LocalWhisperWakeWordDetector(whisperEngine, config.Wake);

                // 5. Router
                _context.Router = new CommandRouter(_context.Skills, config.Wake);

                // 6. Init Executors
                var executors = new List<IActionExecutor>
                {
                    new OpenUrlExecutor(),
                    new BringToFrontExecutor()
                };

                // 7. Create Tray UI
                var appContext = new TrayAppContext(config);
                _context.TrayMenu = appContext.TrayMenu;

                // 8. Wire Events & Settings Persistence
                _context.TrayMenu.OnReloadSkills += (s, e) =>
                {
                    Log.Information("Reloading skills...");
                    skillLoader.LoadSkills();
                };
                
                RefreshDevices();
                
                _context.TrayMenu.OnDeviceSelected += (s, deviceId) =>
                {
                    _context.AudioCapture.SetInputDevice(deviceId);
                    RefreshDevices();
                    UserSettingsStore.Save(_context.Config); // Save on change
                };

                // Save on toggles (need to expose events or hook existing)
                // Assuming TrayMenu directly toggles Config bools, we might need a hook
                // But for now, user asked just for LoadAndApply and Save on change.
                _context.TrayMenu.OnToggleAudioMode += (s, e) =>
                {
                     // Toggle logic
                     _context.Config.Audio.AudioMode = _context.Config.Audio.AudioMode == "Headphones" ? "Speakers" : "Headphones";
                     UserSettingsStore.Save(_context.Config);
                };

                // 9. State Machine
                _stateMachine = new AssistantStateMachine(_context, executors);
                
                // 10. Start
                _stateMachine.Start();

                Application.Run(appContext);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application fatal error");
                MessageBox.Show($"Fatal Error: {ex.Message}", "Jarvis Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _stateMachine?.Dispose();
                _context?.AudioCapture?.Dispose();
                _context?.Stt?.Dispose();
                Log.CloseAndFlush();
            }
        }

        private static void RefreshDevices()
        {
            try
            {
                var devices = _context.AudioCapture.GetInputDevices();
                _context.TrayMenu.UpdateDeviceList(devices, _context.Config.Audio.PreferredInputDeviceId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to refresh devices");
            }
        }
    }
}