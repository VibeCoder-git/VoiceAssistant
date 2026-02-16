using Microsoft.Extensions.Configuration;
using System.IO;

namespace VoiceAssistant.App.Config
{
    public static class ConfigLoader
    {
        public static AppConfig Load()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            var configuration = builder.Build();

            var config = new AppConfig();
            configuration.Bind(config);

            return config;
        }
    }
}
