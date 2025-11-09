using System;

namespace Virgil.App.Services;

public interface IMonitoringService
{
    event EventHandler<Snapshot>? Updated;
    bool IsRunning { get; }
    void Start();
    void Stop();

    public record Snapshot(
        double CpuUsage, double CpuTemp,
        double GpuUsage, double GpuTemp,
        double RamUsage,
        double DiskUsage, double DiskTemp
    );
}
