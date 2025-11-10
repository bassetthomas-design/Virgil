using System;
using Virgil.App.Models;

namespace Virgil.App.Services
{
    public class MonitoringService
    {
        // New event expected by MonitoringViewModel
        public event EventHandler<MetricsEventArgs>? Updated;

        // Legacy/simple event used elsewhere in MVP scaffolding
        public event Action<double,double,double,double>? Metrics; // cpu,gpu,ram,temp (placeholders)

        private readonly Timer _timer = new(2000) { AutoReset = true };

        public MonitoringService()
        {
            _timer.Elapsed += (_, __) => Sample();
        }

        public void Start() => _timer.Start();
        public void Stop() => _timer.Stop();

        private void Sample()
        {
            // TODO: brancher LibreHardwareMonitor ici et remonter les vraies valeurs
            double cpu = 0, gpu = 0, ram = 0, temp = 0;

            // Emettre les deux formes d'événements pour compat
            Metrics?.Invoke(cpu, gpu, ram, temp);
            Updated?.Invoke(this, new MetricsEventArgs(cpu, gpu, ram, temp));
        }
    }
}
