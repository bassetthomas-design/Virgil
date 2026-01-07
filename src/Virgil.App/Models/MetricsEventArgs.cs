using System;

namespace Virgil.App.Models
{
    public class MetricsEventArgs : EventArgs
    {
        // New explicit properties expected by MonitoringViewModel
        public double CpuUsage { get; }
        public double CpuTemp  { get; }
        public double GpuUsage { get; }
        public double GpuTemp  { get; }
        public double RamUsage { get; }
        public double DiskUsage { get; }
        public double DiskTemp  { get; }
        public bool CpuUsageIsStale { get; }
        public bool GpuUsageIsStale { get; }
        public bool RamUsageIsStale { get; }
        public bool CpuTempIsStale { get; }
        public bool DiskUsageIsStale { get; }
        public bool GpuTempIsStale { get; }
        public bool DiskTempIsStale { get; }

        // Legacy aliases kept for backward compatibility (if used somewhere)
        public double Cpu => CpuUsage;
        public double Gpu => GpuUsage;
        public double Ram => RamUsage;
        public double Temp => CpuTemp;

        public MetricsEventArgs(
            double cpuUsage, double gpuUsage, double ramUsage, double cpuTemp,
            double diskUsage = 0, double gpuTemp = 0, double diskTemp = 0,
            bool cpuUsageIsStale = false, bool gpuUsageIsStale = false, bool ramUsageIsStale = false,
            bool cpuTempIsStale = false, bool diskUsageIsStale = false, bool gpuTempIsStale = false, bool diskTempIsStale = false)
        {
            CpuUsage = cpuUsage;
            GpuUsage = gpuUsage;
            RamUsage = ramUsage;
            CpuTemp  = cpuTemp;
            DiskUsage = diskUsage;
            GpuTemp  = gpuTemp;
            DiskTemp = diskTemp;
            CpuUsageIsStale = cpuUsageIsStale;
            GpuUsageIsStale = gpuUsageIsStale;
            RamUsageIsStale = ramUsageIsStale;
            CpuTempIsStale = cpuTempIsStale;
            DiskUsageIsStale = diskUsageIsStale;
            GpuTempIsStale = gpuTempIsStale;
            DiskTempIsStale = diskTempIsStale;
        }
    }
}
