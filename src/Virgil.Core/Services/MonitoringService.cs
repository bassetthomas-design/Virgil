// File: src/Virgil.Core/Services/MonitoringService.cs
#nullable enable
using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Virgil.Core.Services
{
    public sealed class MonitoringSnapshot
    {
        public double CpuUsage { get; init; }
        public double GpuUsage { get; init; }
        public double MemoryUsage { get; init; }
        public double DiskUsage { get; init; }
        public double? CpuTempC { get; init; }
        public double? GpuTempC { get; init; }
        public double? DiskTempC { get; init; }
    }

    public sealed class MonitoringService : IDisposable
    {
        private readonly Random _rnd = new();

        public MonitoringSnapshot ReadInstant()
        {
            double? cpuTemp = null, gpuTemp = null, diskTemp = null;

            try
            {
                // Récupère les températures via ton AdvancedMonitoringService existant
                using var adv = new AdvancedMonitoringService();
                var hw = adv.Read();
                cpuTemp = hw.CpuTempC;
                gpuTemp = hw.GpuTempC;
                diskTemp = hw.DiskTempC;
            }
            catch { }

            // Simule ou lit les charges (ici, juste simulé pour éviter crash)
            double CpuUsage = Wiggle(15, 10);
            double GpuUsage = Wiggle(5, 5);
            double MemoryUsage = Wiggle(40, 10);
            double DiskUsage = Wiggle(7, 8);

            return new MonitoringSnapshot
            {
                CpuUsage = CpuUsage,
                GpuUsage = GpuUsage,
                MemoryUsage = MemoryUsage,
                DiskUsage = DiskUsage,
                CpuTempC = cpuTemp,
                GpuTempC = gpuTemp,
                DiskTempC = diskTemp
            };
        }

        private double Wiggle(double center, double range)
        {
            return Math.Max(0, Math.Min(100, center + (_rnd.NextDouble() - 0.5) * range));
        }

        public void Dispose() { }
    }
}
