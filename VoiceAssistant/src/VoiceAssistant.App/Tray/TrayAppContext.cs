using System;
using System.Drawing;
using System.Windows.Forms;
using Serilog;
using VoiceAssistant.App.Config;

namespace VoiceAssistant.App.Tray
{
    public class TrayAppContext : ApplicationContext
    {
        private readonly NotifyIcon _notifyIcon;
        public TrayMenu TrayMenu { get; } 
        private readonly AppConfig _config;

        public TrayAppContext(AppConfig config)
        {
            _config = config;
            TrayMenu = new TrayMenu(config); 
            
            // Only UI/Exit logic here
            TrayMenu.OnExit += (s, e) => ExitThread();

            _notifyIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application, 
                ContextMenuStrip = TrayMenu.ContextMenu,
                Visible = true,
                Text = "Jarvis Voice Assistant"
            };
            
            Log.Information("Tray context initialized.");
        }

        protected override void ExitThreadCore()
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            base.ExitThreadCore();
        }
    }
}
