using System;
using System.Linq;
using System.Timers;
using LibreHardwareMonitor.Hardware;

namespace Virgil.App.Services;

public sealed class MonitoringService : IMonitoringService, IDisposable
{
    private readonly Computer _pc;
    private readonly Timer _timer;
    private bool _running;

    public event EventHandler<IMonitoringService.Snapshot>? Updated;
    public bool IsRunning => _running;

    public MonitoringService()
    {
        _pc = new Computer(){ IsCpuEnabled = true, IsGpuEnabled = true, IsMemoryEnabled = true, IsStorageEnabled = true };
        _pc.Open();
        _timer = new Timer(1000);
        _timer.Elapsed += Tick;
    }

    public void Start(){ _running = true; _timer.Start(); }
    public void Stop(){ _running = false; _timer.Stop(); }

    private void Tick(object? s, ElapsedEventArgs e)
    {
        try
        {
            foreach (var h in _pc.Hardware) h.Update();

            double cpuUsage = SensorValue(HardwareType.Cpu, SensorType.Load, "CPU Total");
            double cpuTemp  = SensorValue(HardwareType.Cpu, SensorType.Temperature);
            double gpuUsage = SensorValue(HardwareType.GpuNvidia, SensorType.Load, "GPU Core")
                            + SensorValue(HardwareType.GpuAmd, SensorType.Load, "GPU Core");
            double gpuTemp  = SensorValue(HardwareType.GpuNvidia, SensorType.Temperature)
                            + SensorValue(HardwareType.GpuAmd, SensorType.Temperature);
            double ramUsage = SensorValue(HardwareType.Memory, SensorType.Load);
            double diskUsage= SensorValue(HardwareType.Storage, SensorType.Load);
            double diskTemp = SensorValue(HardwareType.Storage, SensorType.Temperature);

            var snap = new IMonitoringService.Snapshot(
                Clamp(cpuUsage), Clamp(cpuTemp),
                Clamp(gpuUsage), Clamp(gpuTemp),
                Clamp(ramUsage),
                Clamp(diskUsage), Clamp(diskTemp)
            );
            Updated?.Invoke(this, snap);
        }
        catch { }
    }

    private static double Clamp(double v) => double.IsNaN(v) ? 0 : Math.Max(0, Math.Min(100, v));

    private double SensorValue(HardwareType type, SensorType st, string? name = null)
    {
        double best = double.NaN;
        foreach (var h in EnumerateHardware(type))
        {
            foreach (var s in h.Sensors)
            {
                if (s.SensorType != st) continue;
                if (name != null && !string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)) continue;
                if (s.Value.HasValue) { best = double.IsNaN(best) ? s.Value.Value : Math.Max(best, s.Value.Value); }
            }
        }
        return best;
    }

    private System.Collections.Generic.IEnumerable<IHardware> EnumerateHardware(HardwareType t)
    {
        foreach (var h in _pc.Hardware)
        {
            if (h.HardwareType == t) yield return h;
            foreach (var sub in h.SubHardware) if (sub.HardwareType == t) yield return sub;
        }
    }

    public void Dispose(){ try{ _timer.Dispose(); _pc.Close(); } catch{} }
}
