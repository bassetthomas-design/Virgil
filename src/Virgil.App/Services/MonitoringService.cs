using System;
using LibreHardwareMonitor.Hardware;
using Virgil.App.Models;

namespace Virgil.App.Services
{
    public class MonitoringService
    {
        public event EventHandler<MetricsEventArgs>? Updated;
        public event Action<double,double,double,double>? Metrics;

        private readonly Computer _pc;
        private readonly Timer _timer = new(2000) { AutoReset = true };

        public MonitoringService()
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
            _timer.Elapsed += (_, __) => Sample();
        }

        public void Start() => _timer.Start();
        public void Stop() => _timer.Stop();

        private void Sample()
        {
            double cpuUsage = 0, gpuUsage = 0, ramUsage = 0;
            double cpuTemp = 0, gpuTemp = 0, diskUsage = 0, diskTemp = 0;

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
                                if (s.SensorType == SensorType.Temperature && s.Name.Contains("CPU Package", StringComparison.OrdinalIgnoreCase))
                                    cpuTemp = s.Value ?? cpuTemp;
                                if (s.SensorType == SensorType.Load && s.Name.Equals("CPU Total", StringComparison.OrdinalIgnoreCase))
                                    cpuUsage = s.Value ?? cpuUsage;
                            }
                            break;
                        case HardwareType.GpuAmd:
                        case HardwareType.GpuNvidia:
                        case HardwareType.GpuIntel:
                            foreach (var s in hw.Sensors)
                            {
                                if (s.SensorType == SensorType.Temperature) gpuTemp = s.Value ?? gpuTemp;
                                if (s.SensorType == SensorType.Load && (s.Name.Contains("Core") || s.Name.Equals("GPU Core", StringComparison.OrdinalIgnoreCase)))
                                    gpuUsage = s.Value ?? gpuUsage;
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
                                if (s.SensorType == SensorType.Load && s.Name.Contains("Usage", StringComparison.OrdinalIgnoreCase))
                                    diskUsage = s.Value ?? diskUsage;
                                if (s.SensorType == SensorType.Temperature)
                                    diskTemp = Math.Max(diskTemp, s.Value ?? diskTemp);
                            }
                            break;
                    }
                }
            }
            catch { /* keep defaults on failure */ }

            Metrics?.Invoke(cpuUsage, gpuUsage, ramUsage, cpuTemp);
            Updated?.Invoke(this, new MetricsEventArgs(cpuUsage, gpuUsage, ramUsage, cpuTemp, diskUsage, gpuTemp, diskTemp));
        }
    }
}
