using System;
using System.Threading;
using System.Threading.Tasks;

namespace Virgil.App.Services
{
    public class SystemMonitorSnapshot
    {
        public float CpuUsage { get; set; }
        public float RamUsage { get; set; }
        // TODO: GPU, Disk, Temperatures
    }

    public interface ISystemMonitorService
    {
        event EventHandler<SystemMonitorSnapshot>? SnapshotUpdated;

        Task StartAsync(CancellationToken cancellationToken);
        void Stop();
    }

    public sealed class SystemMonitorService : ISystemMonitorService, IDisposable
    {
        private Timer? _timer;
        private bool _isRunning;

        public event EventHandler<SystemMonitorSnapshot>? SnapshotUpdated;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (_isRunning)
                return Task.CompletedTask;

            _isRunning = true;

            _timer = new Timer(OnTick, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));

            return Task.CompletedTask;
        }

        private void OnTick(object? state)
        {
            if (!_isRunning)
                return;

            // TODO: replace with real system metrics (CPU, RAM, GPU, Disk, Temps)
            var snapshot = new SystemMonitorSnapshot
            {
                CpuUsage = 0,
                RamUsage = 0
            };

            SnapshotUpdated?.Invoke(this, snapshot);
        }

        public void Stop()
        {
            _isRunning = false;
            _timer?.Dispose();
            _timer = null;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
