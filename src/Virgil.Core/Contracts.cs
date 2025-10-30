namespace Virgil.Core;
public enum Mood { Happy, Focused, Warn, Alert, Sleepy, Proud, Tired }
public record SystemStats(double Cpu,double Gpu,double Ram,double Disk,double CpuTemp,double GpuTemp,double DiskTemp);
public interface IMaintenanceService { Task<MaintenanceResult> CleanAsync(CleanLevel level, IProgress<string>? log=null, CancellationToken ct=default); }
public interface IUpdateService { Task<UpdateResult> UpdateAllAsync(IProgress<string>? log=null, CancellationToken ct=default); }
public interface IMonitoringService { event EventHandler<SystemStats>? Updated; void Start(); void Stop(); bool IsRunning { get; } }
public enum CleanLevel { Simple, Complete, Pro }
public record MaintenanceResult(bool Success,long BytesFreed,string Report);
public record UpdateResult(bool Success,string Report);
