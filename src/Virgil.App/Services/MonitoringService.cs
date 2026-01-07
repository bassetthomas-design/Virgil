using System;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

using LibreHardwareMonitor.Hardware;
using Virgil.App.Models;
using Virgil.App.Utils;

namespace Virgil.App.Services
{
    public class MonitoringService
    {
        public event EventHandler<MetricsEventArgs>? Updated;
        public event Action<double,double,double,double>? Metrics;

        private readonly Computer _pc;
        private readonly bool _isHardwareAvailable;
        private readonly object _timerGate = new();
        private Timer _timer = new(2000) { AutoReset = true };
        private int _isSampling;
        private DateTime _lastErrorLogUtc = DateTime.MinValue;
        private readonly TimeSpan _errorLogInterval = TimeSpan.FromSeconds(30);
        private readonly TimeSpan _staleThreshold = TimeSpan.FromSeconds(8);
        private MetricState _cpuUsage = new();
        private MetricState _gpuUsage = new();
        private MetricState _ramUsage = new();
        private MetricState _diskUsage = new();
        private MetricState _cpuTemp = new();
        private MetricState _gpuTemp = new();
        private MetricState _diskTemp = new();

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
            lock (_timerGate)
            {
                _timer.Stop();
                _timer.Dispose();
                _timer = new Timer(ms) { AutoReset = true };
                _timer.Elapsed += (_, __) => Sample();
                _timer.Start();
            }
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
            try
            {
                if (Interlocked.Exchange(ref _isSampling, 1) == 1)
                {
                    return;
                }

                SampleCore();
            }
            catch (Exception ex)
            {
                LogMonitoringException(ex, "Monitoring sample failed.");
            }
            finally
            {
                Interlocked.Exchange(ref _isSampling, 0);
            }
        }

        private void SampleCore()
        {
            double? cpuUsage = null;
            double? gpuUsage = null;
            double? ramUsage = null;
            double? cpuTemp = null;
            double? gpuTemp = null;
            double? diskUsage = null;
            double? diskTemp = null;

            if (_isHardwareAvailable)
            {
                foreach (var hw in _pc.Hardware)
                {
                    try
                    {
                        hw.Update();
                    }
                    catch (Exception ex)
                    {
                        LogMonitoringException(ex, $"Monitoring update failed for {hw.HardwareType}.");
                        continue;
                    }

                    try
                    {
                        switch (hw.HardwareType)
                        {
                            case HardwareType.Cpu:
                                foreach (var s in hw.Sensors)
                                {
                                    if (s.SensorType == SensorType.Temperature && s.Name.Contains("CPU Package", StringComparison.OrdinalIgnoreCase))
                                    {
                                        cpuTemp = s.Value ?? cpuTemp;
                                    }
                                    else if (s.SensorType == SensorType.Load && s.Name.Equals("CPU Total", StringComparison.OrdinalIgnoreCase))
                                    {
                                        cpuUsage = s.Value ?? cpuUsage;
                                    }
                                }

                                break;
                            case HardwareType.GpuAmd:
                            case HardwareType.GpuNvidia:
                            case HardwareType.GpuIntel:
                                foreach (var s in hw.Sensors)
                                {
                                    if (s.SensorType == SensorType.Temperature)
                                    {
                                        gpuTemp = s.Value ?? gpuTemp;
                                    }
                                    else if (s.SensorType == SensorType.Load && (s.Name.Contains("Core") || s.Name.Equals("GPU Core", StringComparison.OrdinalIgnoreCase)))
                                    {
                                        gpuUsage = s.Value ?? gpuUsage;
                                    }
                                }

                                break;
                            case HardwareType.Memory:
                                foreach (var s in hw.Sensors)
                                {
                                    if (s.SensorType == SensorType.Load)
                                    {
                                        ramUsage = s.Value ?? ramUsage;
                                    }
                                }

                                break;
                            case HardwareType.Storage:
                                foreach (var s in hw.Sensors)
                                {
                                    if (s.SensorType == SensorType.Load && s.Name.Contains("Usage", StringComparison.OrdinalIgnoreCase))
                                    {
                                        diskUsage = s.Value ?? diskUsage;
                                    }
                                    else if (s.SensorType == SensorType.Temperature)
                                    {
                                        diskTemp = Math.Max(diskTemp ?? 0, s.Value ?? 0);
                                    }
                                }

                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        LogMonitoringException(ex, $"Monitoring sensor read failed for {hw.HardwareType}.");
                    }
                }
            }

            UpdateMetric(_cpuUsage, cpuUsage);
            UpdateMetric(_gpuUsage, gpuUsage);
            UpdateMetric(_ramUsage, ramUsage);
            UpdateMetric(_diskUsage, diskUsage);
            UpdateMetric(_cpuTemp, cpuTemp);
            UpdateMetric(_gpuTemp, gpuTemp);
            UpdateMetric(_diskTemp, diskTemp);

            var now = DateTime.UtcNow;
            var snapshot = new MetricsSnapshot(
                GetValue(_cpuUsage, now, out var cpuUsageStale),
                GetValue(_gpuUsage, now, out var gpuUsageStale),
                GetValue(_ramUsage, now, out var ramUsageStale),
                GetValue(_cpuTemp, now, out var cpuTempStale),
                GetValue(_diskUsage, now, out var diskUsageStale),
                GetValue(_gpuTemp, now, out var gpuTempStale),
                GetValue(_diskTemp, now, out var diskTempStale),
                cpuUsageStale,
                gpuUsageStale,
                ramUsageStale,
                cpuTempStale,
                diskUsageStale,
                gpuTempStale,
                diskTempStale);

            Metrics?.Invoke(snapshot.CpuUsage, snapshot.GpuUsage, snapshot.RamUsage, snapshot.CpuTemp);
            Updated?.Invoke(this, new MetricsEventArgs(
                snapshot.CpuUsage,
                snapshot.GpuUsage,
                snapshot.RamUsage,
                snapshot.CpuTemp,
                snapshot.DiskUsage,
                snapshot.GpuTemp,
                snapshot.DiskTemp,
                snapshot.CpuUsageIsStale,
                snapshot.GpuUsageIsStale,
                snapshot.RamUsageIsStale,
                snapshot.CpuTempIsStale,
                snapshot.DiskUsageIsStale,
                snapshot.GpuTempIsStale,
                snapshot.DiskTempIsStale));
        }

        private void UpdateMetric(MetricState state, double? value)
        {
            if (value.HasValue && !double.IsNaN(value.Value))
            {
                state.Update(value.Value);
            }
        }

        private double GetValue(MetricState state, DateTime now, out bool isStale)
        {
            isStale = !state.HasValue || (now - state.LastUpdatedUtc) > _staleThreshold;
            return state.LastGoodValue ?? double.NaN;
        }

        private void LogMonitoringException(Exception ex, string message)
        {
            var now = DateTime.UtcNow;
            if (now - _lastErrorLogUtc < _errorLogInterval)
            {
                return;
            }

            _lastErrorLogUtc = now;
            StartupLog.Write(message, ex);
        }

        private sealed class MetricState
        {
            public double? LastGoodValue { get; private set; }
            public DateTime LastUpdatedUtc { get; private set; }
            public bool HasValue => LastGoodValue.HasValue;

            public void Update(double value)
            {
                LastGoodValue = value;
                LastUpdatedUtc = DateTime.UtcNow;
            }
        }

        private readonly record struct MetricsSnapshot(
            double CpuUsage,
            double GpuUsage,
            double RamUsage,
            double CpuTemp,
            double DiskUsage,
            double GpuTemp,
            double DiskTemp,
            bool CpuUsageIsStale,
            bool GpuUsageIsStale,
            bool RamUsageIsStale,
            bool CpuTempIsStale,
            bool DiskUsageIsStale,
            bool GpuTempIsStale,
            bool DiskTempIsStale);
    }
}
