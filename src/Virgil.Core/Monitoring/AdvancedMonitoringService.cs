using System;
using System.Diagnostics;
using System.Management;
using System.Threading.Tasks;

namespace Virgil.Core.Monitoring;

public sealed class AdvancedMonitoringService
{
    public async Task<HardwareSnapshot> GetSnapshotAsync()
    {
        var snapshot = new HardwareSnapshot();
        await Task.Run(() =>
        {
            snapshot.CpuUsage = UtilProbe.GetCpuUsage();
            snapshot.GpuUsage = UtilProbe.GetGpuUsage();
            snapshot.MemUsage = UtilProbe.GetMemoryUsage();
            snapshot.DiskUsage = UtilProbe.GetDiskUsage();

            snapshot.CpuTemp = UtilProbe.GetTemperature("CPU");
            snapshot.GpuTemp = UtilProbe.GetTemperature("GPU");
            snapshot.DiskTemp = UtilProbe.GetTemperature("Disk");
        });
        return snapshot;
    }
}

public sealed class HardwareSnapshot
{
    public double CpuUsage { get; set; }
    public double GpuUsage { get; set; }
    public double MemUsage { get; set; }
    public double DiskUsage { get; set; }

    public double CpuTemp { get; set; }
    public double GpuTemp { get; set; }
    public double DiskTemp { get; set; }
}

internal static class UtilProbe
{
    public static double GetCpuUsage() => GetPerformanceCounter("Processor Information", "% Processor Utility", "_Total");
    public static double GetGpuUsage() => GetPerformanceCounter("GPU Engine", "Utilization Percentage", "_Total");
    public static double GetMemoryUsage() => 100 - GetPerformanceCounter("Memory", "Available MBytes") / (GetTotalMemoryMb() / 100.0);
    public static double GetDiskUsage() => GetPerformanceCounter("PhysicalDisk", "% Disk Time", "_Total");

    public static double GetTemperature(string type)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM MSAcpi_ThermalZoneTemperature");
            foreach (var obj in searcher.Get())
            {
                double tempK = Convert.ToDouble(obj["CurrentTemperature"]);
                double celsius = (tempK / 10.0) - 273.15;
                if (celsius > 0 && celsius < 120) return celsius;
            }
        }
        catch { }
        return double.NaN;
    }

    private static double GetTotalMemoryMb()
    {
        var ci = new Microsoft.VisualBasic.Devices.ComputerInfo();
        return ci.TotalPhysicalMemory / 1024.0 / 1024.0;
    }

    private static double GetPerformanceCounter(string category, string counter, string? instance = null)
    {
        try
        {
            using var pc = new PerformanceCounter(category, counter, instance ?? string.Empty);
            _ = pc.NextValue();
            System.Threading.Thread.Sleep(200);
            return pc.NextValue();
        }
        catch { return double.NaN; }
    }
}
