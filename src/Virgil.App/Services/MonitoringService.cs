using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
        private readonly object _loopGate = new();
        private CancellationTokenSource? _loopCts;
        private Task? _loopTask;
        private DateTime _lastErrorLogUtc = DateTime.MinValue;
        private readonly TimeSpan _errorLogInterval = TimeSpan.FromSeconds(30);
        private readonly TimeSpan _cacheDuration = TimeSpan.FromSeconds(30);
        private readonly TimeSpan _minInterval = TimeSpan.FromSeconds(5);
        private readonly TimeSpan _maxInterval = TimeSpan.FromSeconds(10);
        private TimeSpan _pollInterval = TimeSpan.FromSeconds(7);
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
        }

        public void SetInterval(int ms)
        {
            var requested = TimeSpan.FromMilliseconds(ms);
            if (requested < _minInterval)
            {
                requested = _minInterval;
            }
            else if (requested > _maxInterval)
            {
                requested = _maxInterval;
            }

            _pollInterval = requested;
        }

        public void Start()
        {
            lock (_loopGate)
            {
                if (_loopTask is { IsCompleted: false })
                {
                    return;
                }

                _loopCts?.Cancel();
                _loopCts?.Dispose();
                _loopCts = new CancellationTokenSource();
                _loopTask = Task.Run(() => MonitorLoopAsync(_loopCts.Token));
            }
        }

        public void Stop()
        {
            lock (_loopGate)
            {
                _loopCts?.Cancel();
                _loopCts?.Dispose();
                _loopCts = null;
                _loopTask = null;
            }
        }

        /// <summary>
        /// Effectue immédiatement un nouveau prélèvement des métriques.
        /// </summary>
        public Task RescanAsync()
        {
            return Task.Run(Sample);
        }

        private async Task MonitorLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var started = DateTime.UtcNow;
                Sample();
                var elapsed = DateTime.UtcNow - started;
                var delay = _pollInterval - elapsed;
                if (delay < TimeSpan.Zero)
                {
                    delay = TimeSpan.Zero;
                }

                try
                {
                    await Task.Delay(delay, token).ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    return;
                }
            }
        }

        private void Sample()
        {
            try
            {
                SampleCore();
            }
            catch (Exception ex)
            {
                LogMonitoringException(ex, "Monitoring sample failed.");
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

            var now = DateTime.UtcNow;
            UpdateMetric(_cpuUsage, cpuUsage, now);
            UpdateMetric(_gpuUsage, gpuUsage, now);
            UpdateMetric(_ramUsage, ramUsage, now);
            UpdateMetric(_diskUsage, diskUsage, now);
            UpdateMetric(_cpuTemp, cpuTemp, now);
            UpdateMetric(_gpuTemp, gpuTemp, now);
            UpdateMetric(_diskTemp, diskTemp, now);

            var cpuUsageSample = GetSample(_cpuUsage, now);
            var gpuUsageSample = GetSample(_gpuUsage, now);
            var ramUsageSample = GetSample(_ramUsage, now);
            var diskUsageSample = GetSample(_diskUsage, now);
            var cpuTempSample = GetSample(_cpuTemp, now);
            var gpuTempSample = GetSample(_gpuTemp, now);
            var diskTempSample = GetSample(_diskTemp, now);
            var snapshot = new MetricsSnapshot(
                cpuUsageSample,
                gpuUsageSample,
                ramUsageSample,
                cpuTempSample,
                diskUsageSample,
                gpuTempSample,
                diskTempSample);

            Metrics?.Invoke(snapshot.CpuUsage.Value, snapshot.GpuUsage.Value, snapshot.RamUsage.Value, snapshot.CpuTemp.Value);
            Updated?.Invoke(this, new MetricsEventArgs(
                snapshot.CpuUsage.Value,
                snapshot.GpuUsage.Value,
                snapshot.RamUsage.Value,
                snapshot.CpuTemp.Value,
                snapshot.DiskUsage.Value,
                snapshot.GpuTemp.Value,
                snapshot.DiskTemp.Value,
                snapshot.CpuUsage.IsStale,
                snapshot.GpuUsage.IsStale,
                snapshot.RamUsage.IsStale,
                snapshot.CpuTemp.IsStale,
                snapshot.DiskUsage.IsStale,
                snapshot.GpuTemp.IsStale,
                snapshot.DiskTemp.IsStale)
            {
                SampledAtUtc = now,
                DataAge = snapshot.DataAge,
                CpuUsageLastUpdatedUtc = snapshot.CpuUsage.LastUpdatedUtc,
                CpuUsageDataAge = snapshot.CpuUsage.DataAge,
                GpuUsageLastUpdatedUtc = snapshot.GpuUsage.LastUpdatedUtc,
                GpuUsageDataAge = snapshot.GpuUsage.DataAge,
                RamUsageLastUpdatedUtc = snapshot.RamUsage.LastUpdatedUtc,
                RamUsageDataAge = snapshot.RamUsage.DataAge,
                DiskUsageLastUpdatedUtc = snapshot.DiskUsage.LastUpdatedUtc,
                DiskUsageDataAge = snapshot.DiskUsage.DataAge,
                CpuTempLastUpdatedUtc = snapshot.CpuTemp.LastUpdatedUtc,
                CpuTempDataAge = snapshot.CpuTemp.DataAge,
                GpuTempLastUpdatedUtc = snapshot.GpuTemp.LastUpdatedUtc,
                GpuTempDataAge = snapshot.GpuTemp.DataAge,
                DiskTempLastUpdatedUtc = snapshot.DiskTemp.LastUpdatedUtc,
                DiskTempDataAge = snapshot.DiskTemp.DataAge
            });
        }

        private void UpdateMetric(MetricState state, double? value, DateTime now)
        {
            if (value.HasValue && !double.IsNaN(value.Value))
            {
                state.Update(value.Value, now);
            }
            else
            {
                state.MarkSampled(now);
            }
        }

        private MetricSample GetSample(MetricState state, DateTime now)
        {
            if (state.HasRecentValue(now, _cacheDuration))
            {
                var age = now - state.LastUpdatedUtc!.Value;
                var stale = state.LastUpdatedUtc < state.LastSampleUtc;
                return new MetricSample(state.LastGoodValue ?? double.NaN, stale, state.LastUpdatedUtc, age);
            }

            return new MetricSample(double.NaN, true, null, null);
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
            public DateTime? LastUpdatedUtc { get; private set; }
            public DateTime? LastSampleUtc { get; private set; }
            public bool HasValue => LastGoodValue.HasValue;

            public void Update(double value, DateTime now)
            {
                LastGoodValue = value;
                LastUpdatedUtc = now;
                LastSampleUtc = now;
            }

            public void MarkSampled(DateTime now)
            {
                LastSampleUtc = now;
            }

            public bool HasRecentValue(DateTime now, TimeSpan maxAge)
                => LastUpdatedUtc.HasValue && (now - LastUpdatedUtc.Value) <= maxAge;
        }

        private readonly record struct MetricSample(
            double Value,
            bool IsStale,
            DateTime? LastUpdatedUtc,
            TimeSpan? DataAge);

        private readonly record struct MetricsSnapshot(
            MetricSample CpuUsage,
            MetricSample GpuUsage,
            MetricSample RamUsage,
            MetricSample CpuTemp,
            MetricSample DiskUsage,
            MetricSample GpuTemp,
            MetricSample DiskTemp)
        {
            public TimeSpan? DataAge
            {
                get
                {
                    var ages = new[]
                        {
                            CpuUsage.DataAge,
                            GpuUsage.DataAge,
                            RamUsage.DataAge,
                            DiskUsage.DataAge,
                            CpuTemp.DataAge,
                            GpuTemp.DataAge,
                            DiskTemp.DataAge
                        }
                        .Where(age => age.HasValue)
                        .Select(age => age!.Value)
                        .ToArray();

                    return ages.Length == 0 ? null : ages.Max();
                }
            }
        }
    }
}
