using System;
using System.IO;
using System.Text.Json;

namespace Virgil.Core.Config
{
    public sealed class Thresholds
    {
        public int CpuWarn { get; set; } = 75;
        public int CpuAlert { get; set; } = 90;
        public int GpuWarn { get; set; } = 75;
        public int GpuAlert { get; set; } = 90;
        public int MemWarn { get; set; } = 80;
        public int MemAlert { get; set; } = 95;
        public int DiskWarn { get; set; } = 80;
        public int DiskAlert { get; set; } = 95;

        public int CpuTempWarn { get; set; } = 75;
        public int CpuTempAlert { get; set; } = 90;
        public int GpuTempWarn { get; set; } = 80;
        public int GpuTempAlert { get; set; } = 95;
        public int DiskTempWarn { get; set; } = 55;
        public int DiskTempAlert { get; set; } = 65;
    }

    public sealed class VirgilConfig
    {
        public Thresholds Thresholds { get; set; } = new();
    }

    public interface IConfigService
    {
        VirgilConfig Get();
    }

    public sealed class ConfigService : IConfigService
    {
        private readonly VirgilConfig _cfg;

        public ConfigService()
        {
            _cfg = LoadMerged();
        }

        public VirgilConfig Get() => _cfg;

        private static VirgilConfig LoadMerged()
        {
            var machine = LoadFrom(MachinePath) ?? new VirgilConfig();
            var user = LoadFrom(UserPath);
            if (user != null) Merge(machine, user);
            return machine;
        }

        private static VirgilConfig? LoadFrom(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    return JsonSerializer.Deserialize<VirgilConfig>(
                        File.ReadAllText(path),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );
                }
            }
            catch { /* safe */ }
            return null;
        }

        private static void Merge(VirgilConfig dst, VirgilConfig src)
        {
            if (src.Thresholds != null)
            {
                var d = dst.Thresholds; var s = src.Thresholds;
                if (s.CpuWarn != default) d.CpuWarn = s.CpuWarn;
                if (s.CpuAlert != default) d.CpuAlert = s.CpuAlert;
                if (s.GpuWarn != default) d.GpuWarn = s.GpuWarn;
                if (s.GpuAlert != default) d.GpuAlert = s.GpuAlert;
                if (s.MemWarn != default) d.MemWarn = s.MemWarn;
                if (s.MemAlert != default) d.MemAlert = s.MemAlert;
                if (s.DiskWarn != default) d.DiskWarn = s.DiskWarn;
                if (s.DiskAlert != default) d.DiskAlert = s.DiskAlert;
                if (s.CpuTempWarn != default) d.CpuTempWarn = s.CpuTempWarn;
                if (s.CpuTempAlert != default) d.CpuTempAlert = s.CpuTempAlert;
                if (s.GpuTempWarn != default) d.GpuTempWarn = s.GpuTempWarn;
                if (s.GpuTempAlert != default) d.GpuTempAlert = s.GpuTempAlert;
                if (s.DiskTempWarn != default) d.DiskTempWarn = s.DiskTempWarn;
                if (s.DiskTempAlert != default) d.DiskTempAlert = s.DiskTempAlert;
            }
        }

        private static string MachinePath
            => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Virgil", "config.json");

        private static string UserPath
            => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Virgil", "user.json");
    }
}
