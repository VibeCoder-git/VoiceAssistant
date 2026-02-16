using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;
using VoiceAssistant.App.Audio;
using VoiceAssistant.App.Config;

namespace VoiceAssistant.App.Tray
{
    public class TrayMenu
    {
        private readonly ContextMenuStrip _contextMenu;
        private readonly AppConfig _config;

        public event EventHandler OnReloadSkills;
        public event EventHandler<string> OnDeviceSelected; // Return Device ID
        public event EventHandler OnToggleAudioMode;
        public event EventHandler OnExit;

        private ToolStripMenuItem _deviceMenuItem;

        public ContextMenuStrip ContextMenu => _contextMenu;

        public TrayMenu(AppConfig config)
        {
            _config = config;
            _contextMenu = new ContextMenuStrip();
            BuildMenu();
        }

        private void BuildMenu()
        {
            // Status Item
            var statusItem = new ToolStripMenuItem("Status: IDLE");
            statusItem.Enabled = false;
            _contextMenu.Items.Add(statusItem);
            _contextMenu.Items.Add(new ToolStripSeparator());

            // Reload Skills
            var reloadItem = new ToolStripMenuItem("Reload skills", null, (s, e) => OnReloadSkills?.Invoke(this, EventArgs.Empty));
            _contextMenu.Items.Add(reloadItem);

            // Select Input Device (Submenu)
            _deviceMenuItem = new ToolStripMenuItem("Input Device");
            _contextMenu.Items.Add(_deviceMenuItem);

            _contextMenu.Items.Add(new ToolStripSeparator());

            // Toggles
            var logTranscriptsItem = new ToolStripMenuItem("Log Transcripts");
            logTranscriptsItem.Checked = _config.Logging.LogTranscripts;
            logTranscriptsItem.Click += (s, e) =>
            {
                _config.Logging.LogTranscripts = !_config.Logging.LogTranscripts;
                logTranscriptsItem.Checked = _config.Logging.LogTranscripts;
            };
            _contextMenu.Items.Add(logTranscriptsItem);

            var audioModeItem = new ToolStripMenuItem($"Mode: {_config.Audio.AudioMode}");
            audioModeItem.Click += (s, e) =>
            {
                OnToggleAudioMode?.Invoke(this, EventArgs.Empty);
            };
            _contextMenu.Items.Add(audioModeItem);

            _contextMenu.Items.Add(new ToolStripSeparator());

            // Open Folders
            _contextMenu.Items.Add("Open app folder", null, (s, e) => Process.Start("explorer.exe", AppDomain.CurrentDomain.BaseDirectory));
            _contextMenu.Items.Add("Open logs folder", null, (s, e) => 
            {
                var logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _config.Logging.LogDirectory);
                if (System.IO.Directory.Exists(logPath))
                    Process.Start("explorer.exe", logPath);
            });

            _contextMenu.Items.Add(new ToolStripSeparator());

            // Exit
            _contextMenu.Items.Add("Exit", null, (s, e) => OnExit?.Invoke(this, EventArgs.Empty));
        }

        public void UpdateDeviceList(List<AudioDevice> devices, string currentId)
        {
            _deviceMenuItem.DropDownItems.Clear();
            if (devices == null) return;

            foreach (var dev in devices)
            {
                var item = new ToolStripMenuItem(dev.Name);
                item.Checked = dev.Id == currentId || (string.IsNullOrEmpty(currentId) && _deviceMenuItem.DropDownItems.Count == 0); // Mark first if unknown?
                if (dev.Id == currentId) item.Checked = true;
                
                item.Click += (s, e) => OnDeviceSelected?.Invoke(this, dev.Id);
                _deviceMenuItem.DropDownItems.Add(item);
            }
        }

        public void UpdateStatus(string status)
        {
             if (_contextMenu.Items[0] is ToolStripMenuItem item)
             {
                 item.Text = $"Status: {status}";
             }
        }
    }
}
