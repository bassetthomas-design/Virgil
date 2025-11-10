using System;

namespace Virgil.App.Services
{
    public class MonitoringService
    {
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
            Metrics?.Invoke(0, 0, 0, 0);
        }
    }
}
