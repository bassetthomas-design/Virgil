using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Virgil.Core.Monitoring
{
    public sealed class AdvancedMonitoringService
    {
        private readonly PerformanceCounter? _cpu;
        private readonly PerformanceCounter? _disk;
        private readonly PerformanceCounter[] _gpuCounters;

        public AdvancedMonitoringService()
        {
            try { _cpu = new PerformanceCounter("Processor", "% Processor Time", "_Total", true); _cpu.NextValue(); } catch { }
            try
            {
                // % Disk Time (fallback si indispo: null -> NaN)
                _disk = new PerformanceCounter("PhysicalDisk", "% Disk Time", "_Total", true);
                _disk.NextValue();
            }
            catch { }

            try
            {
                // GPU Engine (sum des instances 3D)
                var cat = new PerformanceCounterCategory("GPU Engine");
                var instances = cat.GetInstanceNames()
                    .Where(n => n.IndexOf("engtype_3D", StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToArray();
                _gpuCounters = instances
                    .SelectMany(inst => cat.GetCounters(inst))
                    .Where(c => c.CounterName.Equals("Utilization Percentage", StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                // prime les compteurs
                foreach (var c in _gpuCounters) { try { c.NextValue(); } catch { } }
            }
            catch { _gpuCounters = Array.Empty<PerformanceCounter>(); }
        }

        public async Task<HardwareSnapshot> GetSnapshotAsync()
        {
            // On attend un court délai pour stabiliser les compteurs
            await Task.Delay(400).ConfigureAwait(false);

            double cpu = GetSafe(_cpu);
            double disk = GetSafe(_disk);
            double mem = GetMemoryUsagePercent();
            double gpu = GetGpuUsagePercent();

            return new HardwareSnapshot
            {
                CpuUsage = Clamp(cpu),
                GpuUsage = Clamp(gpu),
                MemUsage = Clamp(mem),
                DiskUsage = Clamp(disk),

                CpuTemp = double.NaN,
                GpuTemp = double.NaN,
                DiskTemp = double.NaN
            };
        }

        private static double GetSafe(PerformanceCounter? c)
        {
            if (c == null) return double.NaN;
            try { return c.NextValue(); } catch { return double.NaN; }
        }

        private double GetGpuUsagePercent()
        {
            if (_gpuCounters.Length == 0) return double.NaN;
            double sum = 0;
            foreach (var c in _gpuCounters)
            {
                try { sum += c.NextValue(); } catch { }
            }
            // Les compteurs sont en %, la somme des 3D engines donne une bonne approx d’occupation GPU
            return sum;
        }

        private static double Clamp(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return double.NaN;
            if (v < 0) return 0; if (v > 100) return 100;
            return v;
        }

        // MEM via GlobalMemoryStatusEx (Win32)
        private static double GetMemoryUsagePercent()
        {
            MEMORYSTATUSEX s = new MEMORYSTATUSEX();
            if (GlobalMemoryStatusEx(ref s))
            {
                ulong used = s.ullTotalPhys - s.ullAvailPhys;
                return (double)used / s.ullTotalPhys * 100.0;
            }
            return double.NaN;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
            public MEMORYSTATUSEX() { dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX)); }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
    }
}
