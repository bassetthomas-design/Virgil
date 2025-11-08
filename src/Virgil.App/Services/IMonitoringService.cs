namespace Virgil.App.Services;

public record Metrics(double Cpu, double Ram, double CpuTemp, double DiskReadMBs, double DiskWriteMBs, double NetUpMbps, double NetDownMbps);

public interface IMonitoringService
{
    Metrics Read();
}