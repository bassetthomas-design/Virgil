namespace Virgil.App.Services
{
    public interface IMonitoringService
    {
        MonitoringSnapshot Snapshot { get; }

        event EventHandler? Updated;
    }

    public record MonitoringSnapshot
    {
        public double CpuUsage { get; init; }
        public double MemoryUsage { get; init; }
        public double GpuUsage { get; init; }
        public double DiskUsage { get; init; }
        public double NetworkUsage { get; init; }
    }
}
