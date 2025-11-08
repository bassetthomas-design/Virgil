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
        var (r,w) = ReadDisk();
        var (up,down) = ReadNet();
        return new Metrics(cpu, ram, temp, r, w, up, down);
    }

    private static double ReadCpu()
    {
        using var s = new ManagementObjectSearcher("root/cimv2", "SELECT PercentProcessorTime FROM Win32_PerfFormattedData_PerfOS_Processor WHERE Name='_Total'");
        foreach (ManagementObject o in s.Get()) if (o["PercentProcessorTime"] is uint v) return Math.Clamp(v, 0, 100);
        return 0;
    }

    private static double ReadRam()
    {
        using var s = new ManagementObjectSearcher("root/cimv2", "SELECT FreePhysicalMemory, TotalVisibleMemorySize FROM Win32_OperatingSystem");
        foreach (ManagementObject o in s.Get())
        {
            double freeKb = Convert.ToDouble(o["FreePhysicalMemory"]);
            double totalKb = Convert.ToDouble(o["TotalVisibleMemorySize"]);
            if (totalKb > 0) return Math.Clamp(100.0 - (freeKb/totalKb*100.0), 0, 100);
        }
        return 0;
    }

    private static double ReadCpuTemp()
    {
        try
        {
            using var s = new ManagementObjectSearcher("root/wmi", "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");
            foreach (ManagementObject o in s.Get()) if (o["CurrentTemperature"] is uint t) return Math.Round((t/10.0 - 273.15), 1);
        } catch { }
        return 0;
    }

    private static (double readMBs, double writeMBs) ReadDisk()
    {
        try
        {
            using var s = new ManagementObjectSearcher("root/cimv2", "SELECT DiskReadBytesPersec, DiskWriteBytesPersec FROM Win32_PerfFormattedData_PerfDisk_LogicalDisk WHERE Name='_Total'");
            foreach (ManagementObject o in s.Get())
            {
                double r = Convert.ToDouble(o["DiskReadBytesPersec"]) / (1024*1024.0);
                double w = Convert.ToDouble(o["DiskWriteBytesPersec"]) / (1024*1024.0);
                return (Math.Round(r,2), Math.Round(w,2));
            }
        } catch { }
        return (0,0);
    }

    private static (double upMbps, double downMbps) ReadNet()
    {
        try
        {
            using var s = new ManagementObjectSearcher("root/cimv2", "SELECT BytesReceivedPersec, BytesSentPersec FROM Win32_PerfFormattedData_Tcpip_NetworkInterface");
            double up=0, down=0;
            foreach (ManagementObject o in s.Get())
            {
                down += Convert.ToDouble(o["BytesReceivedPersec"]);
                up   += Convert.ToDouble(o["BytesSentPersec"]);
            }
            return (Math.Round(up*8/(1024*1024.0),2), Math.Round(down*8/(1024*1024.0),2));
        } catch { }
        return (0,0);
    }
}
