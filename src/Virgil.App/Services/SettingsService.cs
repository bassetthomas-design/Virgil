using System;
using System.IO;
using System.Text.Json;

namespace Virgil.App.Services
{
    public class SettingsService
    {
        private static readonly string SettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Virgil", "settings.json");

        public AppSettings Settings { get; private set; }

        public SettingsService()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            if (File.Exists(SettingsPath))
            {
                try { Settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? new AppSettings(); }
                catch { Settings = new AppSettings(); }
            }
            else Settings = new AppSettings();
        }

        public void Save()
        {
            var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
    }

    public class AppSettings
    {
        public int MonitoringIntervalMs { get; set; } = 2000;
        public int DefaultMessageTtlMs { get; set; } = 60000;
        public MoodThreshold Mood { get; set; } = new();
        public bool ShowMiniHud { get; set; } = true;
    }

    public class MoodThreshold
    {
        public double WarnTemp { get; set; } = 70;
        public double AlertTemp { get; set; } = 85;
        public double WarnCpu { get; set; } = 85;
    }
}
