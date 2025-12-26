using System;
using System.IO;
using System.Text.Json;
using Virgil.App.Models;

namespace Virgil.App.Services
{
    public class SettingsService
    {
        private static readonly string SettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Virgil", "settings.json");

        public AppSettings Settings { get; private set; }

        public SettingsService()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            Load();
        }

        public void Save()
        {
            var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }

        /// <summary>
        /// Recharge les paramètres depuis le fichier de configuration.
        /// </summary>
        public void Reload()
        {
            Load();
        }

        private void Load()
        {
            if (File.Exists(SettingsPath))
            {
                try { Settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? new AppSettings(); }
                catch { Settings = new AppSettings(); }
            }
            else
            {
                Settings = new AppSettings();
            }
        }
    }
}
