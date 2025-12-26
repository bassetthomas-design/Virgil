using System;
using System.Threading.Tasks;
using System.Timers;

using LibreHardwareMonitor.Hardware;
using Virgil.App.Models;

namespace Virgil.App.Services
{
    public class MonitoringService
    {
        public event EventHandler<MetricsEventArgs>? Updated;
        public event Action<double,double,double,double>? Metrics;

        private readonly Computer _pc;
        private readonly bool _isHardwareAvailable;
        private Timer _timer = new(2000) { AutoReset = true };

        public MonitoringService()
        {
            try
            {
                _pc = new Computer
                {
                    IsCpuEnabled = true,
                    IsGpuEnabled = true,
                    IsMemoryEnabled = true,
                    IsStorageEnabled = true,
                    IsMotherboardEnabled = true
                };
                _pc.Open();
                _isHardwareAvailable = true;
            }
            catch
            {
                // Sur certaines configurations (VM, droits insuffisants…),
                // l'initialisation de LibreHardwareMonitor peut échouer et
                // planter l'application au démarrage. On garde un stub pour
                // éviter le crash et on désactive simplement la collecte.
                _pc = new Computer();
                _isHardwareAvailable = false;
            }
            _timer.Elapsed += (_, __) => Sample();
        }

        public void SetInterval(int ms)
        {
            _timer.Stop();
            _timer = new Timer(ms) { AutoReset = true };
            _timer.Elapsed += (_, __) => Sample();
            _timer.Start();
        }

        public void Start() => _timer.Start();
        public void Stop() => _timer.Stop();

        /// <summary>
        /// Effectue immédiatement un nouveau prélèvement des métriques.
        /// </summary>
        public Task RescanAsync()
        {
            return Task.Run(Sample);
        }

        private void Sample()
        {
            double cpuUsage = 0, gpuUsage = 0, ramUsage = 0;
            double cpuTemp = 0, gpuTemp = 0, diskUsage = 0, diskTemp = 0;
            if (!_isHardwareAvailable)
            {
                // Matériel non accessible : on publie des valeurs neutres
                // afin de ne pas faire planter le binding côté UI.
                Metrics?.Invoke(cpuUsage, gpuUsage, ramUsage, cpuTemp);
                Updated?.Invoke(this, new MetricsEventArgs(cpuUsage, gpuUsage, ramUsage, cpuTemp, diskUsage, gpuTemp, diskTemp));
                return;
            }

            try
            {
                foreach (var hw in _pc.Hardware)
                {
                    hw.Update();
                    switch (hw.HardwareType)
                    {
                        case HardwareType.Cpu:
                            foreach (var s in hw.Sensors)
                            {
                                if (s.SensorType == SensorType.Temperature && s.Name.Contains("CPU Package", StringComparison.OrdinalIgnoreCase)) cpuTemp = s.Value ?? cpuTemp;
                                if (s.SensorType == SensorType.Load && s.Name.Equals("CPU Total", StringComparison.OrdinalIgnoreCase)) cpuUsage = s.Value ?? cpuUsage;
                            }
                            break;
                        case HardwareType.GpuAmd:
                        case HardwareType.GpuNvidia:
                        case HardwareType.GpuIntel:
                            foreach (var s in hw.Sensors)
                            {
                                if (s.SensorType == SensorType.Temperature) gpuTemp = s.Value ?? gpuTemp;
                                if (s.SensorType == SensorType.Load && (s.Name.Contains("Core") || s.Name.Equals("GPU Core", StringComparison.OrdinalIgnoreCase))) gpuUsage = s.Value ?? gpuUsage;
                            }
                            break;
                        case HardwareType.Memory:
                            foreach (var s in hw.Sensors)
                            {
                                if (s.SensorType == SensorType.Load) ramUsage = s.Value ?? ramUsage;
                            }
                            break;
                        case HardwareType.Storage:
                            foreach (var s in hw.Sensors)
                            {
                                if (s.SensorType == SensorType.Load && s.Name.Contains("Usage", StringComparison.OrdinalIgnoreCase)) diskUsage = s.Value ?? diskUsage;
                                if (s.SensorType == SensorType.Temperature) diskTemp = Math.Max(diskTemp, s.Value ?? diskTemp);
                            }
                            break;
                    }
                }
            }
            catch { }

            Metrics?.Invoke(cpuUsage, gpuUsage, ramUsage, cpuTemp);
            Updated?.Invoke(this, new MetricsEventArgs(cpuUsage, gpuUsage, ramUsage, cpuTemp, diskUsage, gpuTemp, diskTemp));
        }
    }
}
