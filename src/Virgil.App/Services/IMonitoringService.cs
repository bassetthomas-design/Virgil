namespace Virgil.App.Services;

public record Metrics(double Cpu, double Ram, double CpuTemp);

public interface IMonitoringService
{
    Metrics Read();
}