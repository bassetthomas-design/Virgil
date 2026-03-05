using System;
using System.Collections.Generic;

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
        public DateTime SampledAtUtc { get; init; }
        public TimeSpan? DataAge { get; init; }
        public DateTime? CpuUsageLastUpdatedUtc { get; init; }
        public TimeSpan? CpuUsageDataAge { get; init; }
        public DateTime? GpuUsageLastUpdatedUtc { get; init; }
        public TimeSpan? GpuUsageDataAge { get; init; }
        public DateTime? RamUsageLastUpdatedUtc { get; init; }
        public TimeSpan? RamUsageDataAge { get; init; }
        public DateTime? DiskUsageLastUpdatedUtc { get; init; }
        public TimeSpan? DiskUsageDataAge { get; init; }
        public DateTime? CpuTempLastUpdatedUtc { get; init; }
        public TimeSpan? CpuTempDataAge { get; init; }
        public DateTime? GpuTempLastUpdatedUtc { get; init; }
        public TimeSpan? GpuTempDataAge { get; init; }
        public DateTime? DiskTempLastUpdatedUtc { get; init; }
        public TimeSpan? DiskTempDataAge { get; init; }
        public IReadOnlyList<GpuAdapterMetric> GpuAdapters { get; init; } = Array.Empty<GpuAdapterMetric>();
        public string? ActiveGpuName { get; init; }
        public double? ActiveGpuPercent { get; init; }
        public double? ActiveGpuTemp { get; init; }

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
