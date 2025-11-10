using System;
using System.IO;
using System.Text.Json;
using Virgil.App.Models;

namespace Virgil.App.Services
{
    public class SettingsService
    {
        private readonly string _path;
        public AppSettings Settings { get; private set; } = new();

        public SettingsService(string? folder = null)
        {
            var baseDir = folder ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Virgil");
            Directory.CreateDirectory(baseDir);
            _path = Path.Combine(baseDir, "settings.json");
            Load();
        }

        public void Load()
        {
            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                Settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            else
            {
                Save();
            }
        }

        public void Save()
        {
            var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_path, json);
        }
    }
}
