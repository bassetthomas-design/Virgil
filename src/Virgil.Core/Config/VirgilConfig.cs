using System;
using System.IO;
using System.Text.Json;

namespace Virgil.Core.Config
{
    public class VirgilConfig
    {
        public int CpuWarn { get; set; } = 80;
        public int GpuWarn { get; set; } = 80;
        public int RamWarn { get; set; } = 85;
        public int TempWarn { get; set; } = 75;
        public int TempAlert { get; set; } = 85;
        public int PunchlineFrequency { get; set; } = 180; // secondes
        public int MoodFrequency { get; set; } = 5;        // secondes
        public string Tone { get; set; } = "humor";        // "humor" | "pro"
        public string Theme { get; set; } = "dark";        // "dark" | "light"

        public static string GetDefaultPath()
        {
            var app = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dir = Path.Combine(app, "Virgil", "config");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "settings.json");
        }

        public static VirgilConfig Load(string? path = null)
        {
            path ??= GetDefaultPath();
            if (!File.Exists(path)) return new VirgilConfig();
            try
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<VirgilConfig>(json, new JsonSerializerOptions{PropertyNameCaseInsensitive=true}) ?? new VirgilConfig();
            }
            catch { return new VirgilConfig(); }
        }

        public void Save(string? path = null)
        {
            path ??= GetDefaultPath();
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions{WriteIndented=true});
            File.WriteAllText(path, json);
        }
    }
}
