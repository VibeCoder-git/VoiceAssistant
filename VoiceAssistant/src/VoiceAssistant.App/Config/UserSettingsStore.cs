using System;
using System.IO;
using System.Text.Json;
using Serilog;

namespace VoiceAssistant.App.Config
{
    public static class UserSettingsStore
    {
        private static string GetSettingsPath()
        {
             var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "JarvisAssistant");
             Directory.CreateDirectory(appData);
             return Path.Combine(appData, "user.settings.json");
        }

        public class UserSettings
        {
            public string PreferredInputDeviceId { get; set; }
            public string PreferredInputDeviceName { get; set; }
            public string AudioMode { get; set; }
            public bool LogTranscripts { get; set; }
        }

        public static void LoadAndApply(AppConfig config)
        {
            try
            {
                var path = GetSettingsPath();
                if (!File.Exists(path)) return;

                var json = File.ReadAllText(path);
                var settings = JsonSerializer.Deserialize<UserSettings>(json);

                if (settings != null)
                {
                    if (!string.IsNullOrEmpty(settings.PreferredInputDeviceId))
                        config.Audio.PreferredInputDeviceId = settings.PreferredInputDeviceId;
                    
                    if (!string.IsNullOrEmpty(settings.PreferredInputDeviceName))
                        config.Audio.PreferredInputDeviceName = settings.PreferredInputDeviceName;

                    if (!string.IsNullOrEmpty(settings.AudioMode))
                        config.Audio.AudioMode = settings.AudioMode;

                    config.Logging.LogTranscripts = settings.LogTranscripts;
                    
                    Log.Information("User settings loaded and applied.");
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"Failed to load user settings: {ex.Message}");
            }
        }

        public static void Save(AppConfig config)
        {
            try
            {
                var settings = new UserSettings
                {
                    PreferredInputDeviceId = config.Audio.PreferredInputDeviceId,
                    PreferredInputDeviceName = config.Audio.PreferredInputDeviceName,
                    AudioMode = config.Audio.AudioMode,
                    LogTranscripts = config.Logging.LogTranscripts
                };

                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(GetSettingsPath(), json);
                // Log.Debug("User settings saved.");
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to save user settings: {ex.Message}");
            }
        }
    }
}
