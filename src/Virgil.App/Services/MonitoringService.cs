using System;
using System.Linq;
using System.Management;

namespace Virgil.App.Services;

public class MonitoringService : IMonitoringService
{
    public Metrics Read()
    {
        var cpu = ReadCpu();
        var ram = ReadRam();
        var temp = ReadCpuTemp();
        return new Metrics(cpu, ram, temp);
    }

    private static double ReadCpu()
    {
        using var searcher = new ManagementObjectSearcher("root/cimv2", "SELECT PercentProcessorTime FROM Win32_PerfFormattedData_PerfOS_Processor WHERE Name='_Total'");
        foreach (ManagementObject obj in searcher.Get())
        {
            if (obj["PercentProcessorTime"] is uint v) return Math.Clamp(v, 0, 100);
        }
        return 0;
    }

    private static double ReadRam()
    {
        using var searcher = new ManagementObjectSearcher("root/cimv2", "SELECT FreePhysicalMemory, TotalVisibleMemorySize FROM Win32_OperatingSystem");
        foreach (ManagementObject obj in searcher.Get())
        {
            double freeKb = Convert.ToDouble(obj["FreePhysicalMemory"]);
            double totalKb = Convert.ToDouble(obj["TotalVisibleMemorySize"]);
            if (totalKb > 0)
            {
                double used = 100.0 - (freeKb / totalKb * 100.0);
                return Math.Clamp(used, 0, 100);
            }
        }
        return 0;
    }

    private static double ReadCpuTemp()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("root/wmi", "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");
            foreach (ManagementObject obj in searcher.Get())
            {
                if (obj["CurrentTemperature"] is uint t)
                {
                    // Kelvin * 10 to Celsius
                    return Math.Round((t / 10.0 - 273.15), 1);
                }
            }
        }
        catch { }
        return 0; // fallback if not available
    }
}