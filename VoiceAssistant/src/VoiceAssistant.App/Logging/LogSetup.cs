using Serilog;
using System;
using System.IO;
using VoiceAssistant.App.Config;

namespace VoiceAssistant.App.Logging
{
    public static class LogSetup
    {
        public static void Initialize(LoggingConfig config)
        {
            var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, config.LogDirectory, "log-.txt");

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.File(logPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
                .CreateLogger();

            Log.Information("Logging initialized.");
        }
    }
}
