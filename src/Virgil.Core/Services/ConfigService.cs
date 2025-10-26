using System;
using System.IO;
using System.Text.Json;

namespace Virgil.Core.Services
{
    public sealed class ConfigService
    {
        private const string AppFolderName = "Virgil";
        private const string MachineConfigFile = "config.json";
        private const string UserConfigFile = "user.json";

        public VirgilConfig Current { get; private set; }

        private readonly string _machineConfigPath;
        private readonly string _userConfigPath;

        public ConfigService()
        {
            var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            var machineDir = Path.Combine(programData, AppFolderName);
            var userDir = Path.Combine(appData, AppFolderName);

            Directory.CreateDirectory(machineDir);
            Directory.CreateDirectory(userDir);

            _machineConfigPath = Path.Combine(machineDir, MachineConfigFile);
            _userConfigPath = Path.Combine(userDir, UserConfigFile);

            // Charger machine + user (user override)
            var machine = LoadOrCreateDefault(_machineConfigPath);
            var user = File.Exists(_userConfigPath) ? Load(_userConfigPath) : new VirgilConfig();

            Current = Merge(machine, user);
        }

        public void SaveUser()
        {
            var json = JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_userConfigPath, json);
        }

        private static VirgilConfig Load(string path)
        {
            try
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<VirgilConfig>(json) ?? new VirgilConfig();
            }
            catch
            {
                return new VirgilConfig();
            }
        }

        private static VirgilConfig LoadOrCreateDefault(string path)
        {
            if (!File.Exists(path))
            {
                var def = new VirgilConfig(); // valeurs par défaut
                var json = JsonSerializer.Serialize(def, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
                return def;
            }
            return Load(path);
        }

        private static VirgilConfig Merge(VirgilConfig machine, VirgilConfig user)
        {
            // user override si défini, sinon valeur machine
            return new VirgilConfig
            {
                CpuWarn = user.CpuWarn ?? machine.CpuWarn,
                CpuAlert = user.CpuAlert ?? machine.CpuAlert,
                MemWarn = user.MemWarn ?? machine.MemWarn,
                MemAlert = user.MemAlert ?? machine.MemAlert,
                CpuTempWarn = user.CpuTempWarn ?? machine.CpuTempWarn,
                CpuTempAlert = user.CpuTempAlert ?? machine.CpuTempAlert,
                GpuTempWarn = user.GpuTempWarn ?? machine.GpuTempWarn,
                GpuTempAlert = user.GpuTempAlert ?? machine.GpuTempAlert
            };
        }
    }

    public sealed class VirgilConfig
    {
        // Seuils CPU/MEM en %
        public float? CpuWarn { get; set; } = 65f;
        public float? CpuAlert { get; set; } = 85f;
        public float? MemWarn { get; set; } = 70f;
        public float? MemAlert { get; set; } = 90f;

        // Seuils températures en °C
        public float? CpuTempWarn { get; set; } = 80f;
        public float? CpuTempAlert { get; set; } = 90f;
        public float? GpuTempWarn { get; set; } = 80f;
        public float? GpuTempAlert { get; set; } = 90f;
    }
}
