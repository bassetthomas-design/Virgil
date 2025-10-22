using System;
using System.IO;
using System.Text.Json;
using Virgil.Core.Config;

namespace Virgil.Core.Services
{
    public sealed class ConfigService
    {
        public AppConfig Current { get; private set; } = new();
        public event EventHandler? Changed;

        public ConfigService()
        {
            EnsureDirs();
            Load();
        }

        private static void EnsureDirs()
        {
            Directory.CreateDirectory(AppPaths.ProgramDataRoot);
            Directory.CreateDirectory(AppPaths.UserDataRoot);
            Directory.CreateDirectory(AppPaths.LogsDir);
        }

        public void Load()
        {
            try
            {
                if (File.Exists(AppPaths.UserConfig))
                {
                    var json = File.ReadAllText(AppPaths.UserConfig);
                    Current = JsonSerializer.Deserialize<AppConfig>(json, AppConfig.JsonOptions) ?? new AppConfig();
                }
                else if (File.Exists(AppPaths.ProgramDataConfig))
                {
                    var json = File.ReadAllText(AppPaths.ProgramDataConfig);
                    Current = JsonSerializer.Deserialize<AppConfig>(json, AppConfig.JsonOptions) ?? new AppConfig();
                }
            }
            catch
            {
                Current = new AppConfig();
            }
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void SaveUser()
        {
            EnsureDirs();
            var json = JsonSerializer.Serialize(Current, AppConfig.JsonOptions);
            File.WriteAllText(AppPaths.UserConfig, json);
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
