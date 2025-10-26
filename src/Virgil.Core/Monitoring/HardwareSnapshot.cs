using System;

namespace Virgil.Core.Monitoring
{
    public sealed class HardwareSnapshot
    {
        // Usages en %
        public double CpuUsage { get; set; }
        public double GpuUsage { get; set; }
        public double MemUsage { get; set; }
        public double DiskUsage { get; set; }

        // Températures en °C (NaN si non dispo)
        public double CpuTemp { get; set; } = double.NaN;
        public double GpuTemp { get; set; } = double.NaN;
        public double DiskTemp { get; set; } = double.NaN;
    }
}
