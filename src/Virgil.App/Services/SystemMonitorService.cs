#nullable enable

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using LibreHardwareMonitor.Hardware;

namespace Virgil.App.Services
{
    /// <summary>
    /// Snapshot de métriques système (minimum viable pour #146).
    /// Valeurs en pourcentage (0..100) + températures (°C).
    /// </summary>
    public sealed class SystemMonitorSnapshot
    {
        public float CpuUsage { get; set; }
        public float RamUsage { get; set; }
        public float GpuUsage { get; set; }
        public float DiskUsage { get; set; }
        public float CpuTemperature { get; set; }
        public float GpuTemperature { get; set; }
        public float DiskTemperature { get; set; }
    }

    public interface ISystemMonitorService
    {
        event EventHandler<SystemMonitorSnapshot>? SnapshotUpdated;

        /// <summary>Dernière valeur connue (jamais null, mais peut être à 0 si indisponible).</summary>
        SystemMonitorSnapshot Latest { get; }

        Task StartAsync(CancellationToken cancellationToken);
        Task StopAsync(CancellationToken cancellationToken);
    }

    /// <summary>
    /// Implémentation simple, stable, Windows-first, pour remonter CPU/RAM réels.
    /// (#146) – On ajoutera GPU/Disques/Températures plus tard.
    /// </summary>
    public sealed class SystemMonitorService : ISystemMonitorService, IDisposable
    {
        private System.Threading.Timer? _timer;
        private volatile bool _isRunning;
        private readonly object _gate = new();

        // Perf counters (Windows). Si indisponibles, restent null et on publie 0.
        private PerformanceCounter? _cpuCounter;
        private PerformanceCounter? _ramCounter;
        private readonly Computer? _computer;

        public event EventHandler<SystemMonitorSnapshot>? SnapshotUpdated;

        public SystemMonitorSnapshot Latest { get; } = new SystemMonitorSnapshot();

        public SystemMonitorService()
        {
            // Initialise les compteurs de performance.
            // Si ça échoue (compteurs absents / runtime non supporté), on reste en mode dégradé.
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _ramCounter = new PerformanceCounter("Memory", "% Committed Bytes In Use");
                _computer = new Computer
                {
                    IsCpuEnabled = true,
                    IsGpuEnabled = true,
                    IsMemoryEnabled = true,
                    IsStorageEnabled = true,
                    IsMotherboardEnabled = true
                };
                _computer.Open();
            }
            catch
            {
                _cpuCounter = null;
                _ramCounter = null;
                _computer = null;
            }
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (_isRunning) return Task.CompletedTask;

            lock (_gate)
            {
                if (_isRunning) return Task.CompletedTask;

                _isRunning = true;

                // Prime les counters: le premier NextValue() est souvent 0.
                try { _cpuCounter?.NextValue(); } catch { }
                try { _ramCounter?.NextValue(); } catch { }

                _timer = new System.Threading.Timer(OnTick, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            if (!_isRunning) return Task.CompletedTask;

            lock (_gate)
            {
                _isRunning = false;
                _timer?.Dispose();
                _timer = null;
            }

            return Task.CompletedTask;
        }

        private void OnTick(object? state)
        {
            if (!_isRunning) return;

            float cpu = 0f;
            float ram = 0f;
            float gpu = 0f;
            float disk = 0f;
            float cpuTemp = 0f;
            float gpuTemp = 0f;
            float diskTemp = 0f;

            try { cpu = _cpuCounter?.NextValue() ?? 0f; } catch { cpu = 0f; }
            try { ram = _ramCounter?.NextValue() ?? 0f; } catch { ram = 0f; }

            if (_computer != null)
            {
                try
                {
                    foreach (var hw in _computer.Hardware)
                    {
                        hw.Update();
                        switch (hw.HardwareType)
                        {
                            case HardwareType.Cpu:
                                foreach (var s in hw.Sensors)
                                {
                                    if (s.SensorType == SensorType.Load && s.Name.Equals("CPU Total", StringComparison.OrdinalIgnoreCase))
                                    {
                                        cpu = s.Value ?? cpu;
                                    }
                                    else if (s.SensorType == SensorType.Temperature && s.Name.Contains("Package", StringComparison.OrdinalIgnoreCase))
                                    {
                                        cpuTemp = s.Value ?? cpuTemp;
                                    }
                                }

                                break;

                            case HardwareType.GpuAmd:
                            case HardwareType.GpuNvidia:
                            case HardwareType.GpuIntel:
                                foreach (var s in hw.Sensors)
                                {
                                    if (s.SensorType == SensorType.Load && (s.Name.Contains("Core", StringComparison.OrdinalIgnoreCase) || s.Name.Equals("GPU Core", StringComparison.OrdinalIgnoreCase)))
                                    {
                                        gpu = s.Value ?? gpu;
                                    }
                                    else if (s.SensorType == SensorType.Temperature)
                                    {
                                        gpuTemp = s.Value ?? gpuTemp;
                                    }
                                }

                                break;

                            case HardwareType.Memory:
                                foreach (var s in hw.Sensors)
                                {
                                    if (s.SensorType == SensorType.Load)
                                    {
                                        ram = s.Value ?? ram;
                                    }
                                }

                                break;

                            case HardwareType.Storage:
                                foreach (var s in hw.Sensors)
                                {
                                    if (s.SensorType == SensorType.Load && s.Name.Contains("Usage", StringComparison.OrdinalIgnoreCase))
                                    {
                                        disk = Math.Max(disk, s.Value ?? disk);
                                    }
                                    else if (s.SensorType == SensorType.Temperature)
                                    {
                                        diskTemp = Math.Max(diskTemp, s.Value ?? diskTemp);
                                    }
                                }

                                break;
                        }
                    }
                }
                catch
                {
                    // On reste en mode dégradé si LibreHardwareMonitor échoue.
                }
            }

            // Clamp basique
            if (cpu < 0) cpu = 0;
            if (cpu > 100) cpu = 100;
            if (ram < 0) ram = 0;
            if (ram > 100) ram = 100;
            if (gpu < 0) gpu = 0;
            if (gpu > 100) gpu = 100;
            if (disk < 0) disk = 0;
            if (disk > 100) disk = 100;
            if (cpuTemp < 0) cpuTemp = 0;
            if (gpuTemp < 0) gpuTemp = 0;
            if (diskTemp < 0) diskTemp = 0;

            Latest.CpuUsage = cpu;
            Latest.RamUsage = ram;
            Latest.GpuUsage = gpu;
            Latest.DiskUsage = disk;
            Latest.CpuTemperature = cpuTemp;
            Latest.GpuTemperature = gpuTemp;
            Latest.DiskTemperature = diskTemp;

            try
            {
                SnapshotUpdated?.Invoke(this, new SystemMonitorSnapshot
                {
                    CpuUsage = cpu,
                    RamUsage = ram,
                    GpuUsage = gpu,
                    DiskUsage = disk,
                    CpuTemperature = cpuTemp,
                    GpuTemperature = gpuTemp,
                    DiskTemperature = diskTemp
                });
            }
            catch
            {
                // On évite qu’un subscriber cassé stoppe le monitoring.
            }
        }

        public void Dispose()
        {
            try { _timer?.Dispose(); } catch { }
            _timer = null;

            try { _cpuCounter?.Dispose(); } catch { }
            try { _ramCounter?.Dispose(); } catch { }
            try { _computer?.Close(); } catch { }

            _cpuCounter = null;
            _ramCounter = null;
            _isRunning = false;
        }
    }
}
