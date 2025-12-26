#nullable enable

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Virgil.App.Services
{
    /// <summary>
    /// Snapshot de métriques système (minimum viable pour #146).
    /// Valeurs en pourcentage (0..100).
    /// </summary>
    public sealed class SystemMonitorSnapshot
    {
        public float CpuUsage { get; set; }
        public float RamUsage { get; set; }
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
        private Timer? _timer;
        private volatile bool _isRunning;
        private readonly object _gate = new();

        // Perf counters (Windows). Si indisponibles, restent null et on publie 0.
        private PerformanceCounter? _cpuCounter;
        private PerformanceCounter? _ramCounter;

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
            }
            catch
            {
                _cpuCounter = null;
                _ramCounter = null;
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

                _timer = new Timer(OnTick, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
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

            try { cpu = _cpuCounter?.NextValue() ?? 0f; } catch { cpu = 0f; }
            try { ram = _ramCounter?.NextValue() ?? 0f; } catch { ram = 0f; }

            // Clamp basique
            if (cpu < 0) cpu = 0;
            if (cpu > 100) cpu = 100;
            if (ram < 0) ram = 0;
            if (ram > 100) ram = 100;

            Latest.CpuUsage = cpu;
            Latest.RamUsage = ram;

            try
            {
                SnapshotUpdated?.Invoke(this, new SystemMonitorSnapshot
                {
                    CpuUsage = cpu,
                    RamUsage = ram
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

            _cpuCounter = null;
            _ramCounter = null;
            _isRunning = false;
        }
    }
}
