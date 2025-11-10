using System;
using Virgil.App.Models;

namespace Virgil.App.Services
{
    public class MonitoringService
    {
        public event EventHandler<MetricsEventArgs>? Updated;
        public event Action<double,double,double,double>? Metrics; // legacy simple tuple (cpu,gpu,ram,cpuTemp)

        private readonly Timer _timer = new(2000) { AutoReset = true };

        public MonitoringService()
        {
            _timer.Elapsed += (_, __) => Sample();
        }

        public void Start() => _timer.Start();
        public void Stop() => _timer.Stop();

        private void Sample()
        {
            // TODO: remplacer par vraies valeurs LibreHardwareMonitor
            double cpuUsage = 0, gpuUsage = 0, ramUsage = 0, cpuTemp = 0;
            double diskUsage = 0, gpuTemp = 0, diskTemp = 0;

            Metrics?.Invoke(cpuUsage, gpuUsage, ramUsage, cpuTemp);
            Updated?.Invoke(this, new MetricsEventArgs(cpuUsage, gpuUsage, ramUsage, cpuTemp, diskUsage, gpuTemp, diskTemp));
        }
    }
}
