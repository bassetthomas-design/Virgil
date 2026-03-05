using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using LibreHardwareMonitor.Hardware;
using Virgil.App.Models;
using Virgil.App.Utils;

namespace Virgil.App.Services
{
    public class MonitoringService
    {
        private static readonly TimeSpan DefaultMonitoringInterval = TimeSpan.FromMinutes(5);

        public event EventHandler<MetricsEventArgs>? Updated;
        public event Action<double, double, double, double>? Metrics;
        public event EventHandler<SystemMetricsSnapshot>? SnapshotUpdated;

        private readonly object _loopGate = new();
        private CancellationTokenSource? _loopCts;
        private Task? _loopTask;
        private readonly SemaphoreSlim _sampleGate = new(1, 1);
        private readonly TimeSpan _errorLogInterval = TimeSpan.FromSeconds(30);
        private DateTime _lastErrorLogUtc = DateTime.MinValue;

        private TimeSpan _monitoringInterval = DefaultMonitoringInterval;

        private readonly PerformanceCounter? _cpuCounter;
        private readonly PerformanceCounter? _diskActiveCounter;
        private readonly PerformanceCounter? _diskReadCounter;
        private readonly PerformanceCounter? _diskWriteCounter;
        private readonly Dictionary<string, GpuAdapterCounters> _gpuCountersByAdapter;

        private readonly Computer? _computer;
        private readonly bool _isHardwareAvailable;

        private readonly FixedWindowAverage _cpuSmoothing = new(3);
        private readonly FixedWindowAverage _diskSmoothing = new(3);
        private readonly FixedWindowAverage _gpuSmoothing = new(3);
        private readonly FixedWindowAverage _cpuTempSmoothing = new(3);
        private readonly FixedWindowAverage _gpuTempSmoothing = new(3);
        private double? _lastAcceptedCpuTempRaw;
        private double? _lastAcceptedGpuTempRaw;

        private bool _cpuWarmupDone;
        private string _lastError = "none";

        public DateTime? LastTelemetryUpdateUtc { get; private set; }
        public DateTime? NextTelemetryUpdateUtc { get; private set; }
        public double? DataAgeSeconds { get; private set; }
        public string LastDiagnostics { get; private set; } = string.Empty;

        public MonitoringService()
        {
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total", true);
                _cpuCounter.NextValue();
            }
            catch { }

            try
            {
                _diskActiveCounter = new PerformanceCounter("PhysicalDisk", "% Disk Time", "_Total", true);
                _diskReadCounter = new PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", "_Total", true);
                _diskWriteCounter = new PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", "_Total", true);
                _diskActiveCounter.NextValue();
                _diskReadCounter.NextValue();
                _diskWriteCounter.NextValue();
            }
            catch { }

            try
            {
                _gpuCountersByAdapter = BuildGpuCountersByAdapter();
                foreach (var adapter in _gpuCountersByAdapter.Values)
                {
                    foreach (var counter in adapter.ThreeD)
                    {
                        try { counter.NextValue(); } catch { }
                    }

                    foreach (var counter in adapter.Total)
                    {
                        try { counter.NextValue(); } catch { }
                    }
                }
            }
            catch
            {
                _gpuCountersByAdapter = new Dictionary<string, GpuAdapterCounters>(StringComparer.OrdinalIgnoreCase);
            }

            try
            {
                _computer = new Computer
                {
                    IsCpuEnabled = true,
                    IsGpuEnabled = true,
                    IsMemoryEnabled = false,
                    IsStorageEnabled = false,
                    IsMotherboardEnabled = false
                };
                _computer.Open();
                _isHardwareAvailable = true;
            }
            catch
            {
                _computer = null;
                _isHardwareAvailable = false;
            }
        }

        public void SetMonitoringIntervalMs(int intervalMs)
        {
            _ = intervalMs;
            _monitoringInterval = DefaultMonitoringInterval;
        }

        public void SetIntervalRange(int minMinutes, int maxMinutes)
        {
            _ = minMinutes;
            _ = maxMinutes;
            _monitoringInterval = DefaultMonitoringInterval;
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
                NextTelemetryUpdateUtc = null;
            }
        }

        public Task StartAsync() { Start(); return Task.CompletedTask; }
        public Task StopAsync() { Stop(); return Task.CompletedTask; }

        public Task RescanAsync() => RefreshNowAsync();

        public Task RefreshNowAsync()
        {
            Trace.WriteLine("Monitoring manual refresh requested.");
            return Task.Run(async () =>
            {
                var started = Stopwatch.StartNew();
                var sampled = await SampleAsync(CancellationToken.None).ConfigureAwait(false);
                started.Stop();
                LogTickDuration(started.Elapsed, sampled);
            });
        }

        private async Task MonitorLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var started = Stopwatch.StartNew();
                var sampled = await SampleAsync(token).ConfigureAwait(false);
                started.Stop();

                LogTickDuration(started.Elapsed, sampled);
                TimeSpan refreshInterval = TimeSpan.FromMinutes(5);
                _monitoringInterval = refreshInterval;
                var delay = refreshInterval - started.Elapsed;
                if (delay < TimeSpan.Zero)
                {
                    delay = TimeSpan.Zero;
                }

                var nextUpdate = DateTimeOffset.Now + refreshInterval;
                NextTelemetryUpdateUtc = nextUpdate.DateTime;
                Trace.WriteLine($"Monitoring next tick scheduled at {NextTelemetryUpdateUtc:O} (interval {_monitoringInterval.TotalSeconds:F1}s).");

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

        private async Task<bool> SampleAsync(CancellationToken token)
        {
            if (!await _sampleGate.WaitAsync(0, token).ConfigureAwait(false))
            {
                return false;
            }

            try
            {
                await SampleCoreAsync(token).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                LogMonitoringException(ex, "Monitoring sample failed.");
                return false;
            }
            finally
            {
                _sampleGate.Release();
            }
        }

        private async Task SampleCoreAsync(CancellationToken token)
        {
            var now = DateTimeOffset.UtcNow;
            if (!_cpuWarmupDone && _cpuCounter != null)
            {
                _cpuCounter.NextValue();
                await Task.Delay(TimeSpan.FromSeconds(1), token).ConfigureAwait(false);
                _cpuWarmupDone = true;
            }

            var ram = ReadRamMetrics();
            var cpuRaw = ReadCounter(_cpuCounter, 0, 100);
            var diskActiveRaw = ReadCounter(_diskActiveCounter, 0, 100);
            var diskReadBps = ReadCounter(_diskReadCounter, 0, null);
            var diskWriteBps = ReadCounter(_diskWriteCounter, 0, null);
            var gpuSample = ReadGpuSample();
            var temps = ReadTemperatures();
            var cpuTempRaw = ValidateTemperatureSample(temps.CpuTemp, ref _lastAcceptedCpuTempRaw);
            var gpuTempRaw = ValidateTemperatureSample(temps.GpuTemp, ref _lastAcceptedGpuTempRaw);

            var cpu = _cpuSmoothing.AddAndAverage(cpuRaw);
            var disk = _diskSmoothing.AddAndAverage(diskActiveRaw);
            var gpu = _gpuSmoothing.AddAndAverage(gpuSample.ActiveGpuPercent3D);
            var cpuTempSmoothed = _cpuTempSmoothing.AddAndAverage(cpuTempRaw);
            var gpuTempSmoothed = _gpuTempSmoothing.AddAndAverage(gpuTempRaw);

            var snapshot = new SystemMetricsSnapshot
            {
                Timestamp = now,
                CpuPercent = cpu,
                RamPercent = ram.Percent,
                RamUsedBytes = ram.UsedBytes,
                RamTotalBytes = ram.TotalBytes,
                DiskActivePercent = disk,
                DiskReadBps = diskReadBps,
                DiskWriteBps = diskWriteBps,
                GpuPercent = gpu,
                GpuAdapters = gpuSample.Adapters,
                ActiveGpuName = gpuSample.ActiveGpuName,
                ActiveGpuPercent = gpuSample.ActiveGpuPercent3D,
                ActiveGpuTempC = gpuTempSmoothed,
                CpuTempC = cpuTempSmoothed,
                GpuTempC = gpuTempSmoothed,
                TempProviderName = temps.ProviderName,
                CpuTempSensorName = temps.CpuSensorName,
                GpuTempSensorName = temps.GpuSensorName,
                CpuTempRawC = cpuTempRaw,
                GpuTempRawC = gpuTempRaw,
                CpuTempSmoothedC = cpuTempSmoothed,
                GpuTempSmoothedC = gpuTempSmoothed,
                SourceFlags = BuildSourceFlags(ram.IsAvailable, _cpuCounter != null, _diskActiveCounter != null, _gpuCountersByAdapter.Count > 0, temps.IsProviderAvailable),
                ProviderName = _gpuCountersByAdapter.Count > 0 ? "GPUEngineCounter" : "Unavailable",
                SampleAgeMs = (DateTimeOffset.UtcNow - now).TotalMilliseconds
            };

            LastTelemetryUpdateUtc = now.UtcDateTime;
            DataAgeSeconds = snapshot.SampleAgeMs / 1000d;

            BuildDiagnostics(snapshot, cpuRaw, cpu, diskActiveRaw, disk, gpuSample.ActiveGpuPercent3D, gpu);
            PublishLegacyEvents(snapshot);
            SnapshotUpdated?.Invoke(this, snapshot);
        }

        private void PublishLegacyEvents(SystemMetricsSnapshot snapshot)
        {
            var cpu = snapshot.CpuPercent ?? double.NaN;
            var ram = snapshot.RamPercent ?? double.NaN;
            var disk = snapshot.DiskActivePercent ?? double.NaN;
            var gpu = snapshot.GpuPercent ?? double.NaN;
            var cpuTemp = snapshot.CpuTempC ?? double.NaN;
            var gpuTemp = snapshot.GpuTempC ?? double.NaN;

            Metrics?.Invoke(OrZero(cpu), OrZero(gpu), OrZero(ram), OrZero(cpuTemp));
            Updated?.Invoke(this, new MetricsEventArgs(
                cpu,
                gpu,
                ram,
                cpuTemp,
                disk,
                gpuTemp,
                0,
                cpuUsageIsStale: !snapshot.CpuPercent.HasValue,
                gpuUsageIsStale: !snapshot.GpuPercent.HasValue,
                ramUsageIsStale: !snapshot.RamPercent.HasValue,
                cpuTempIsStale: !snapshot.CpuTempC.HasValue,
                diskUsageIsStale: !snapshot.DiskActivePercent.HasValue,
                gpuTempIsStale: !snapshot.GpuTempC.HasValue,
                diskTempIsStale: true)
            {
                SampledAtUtc = snapshot.Timestamp.UtcDateTime,
                DataAge = TimeSpan.FromMilliseconds(snapshot.SampleAgeMs),
                CpuUsageLastUpdatedUtc = snapshot.CpuPercent.HasValue ? snapshot.Timestamp.UtcDateTime : null,
                GpuUsageLastUpdatedUtc = snapshot.GpuPercent.HasValue ? snapshot.Timestamp.UtcDateTime : null,
                RamUsageLastUpdatedUtc = snapshot.RamPercent.HasValue ? snapshot.Timestamp.UtcDateTime : null,
                DiskUsageLastUpdatedUtc = snapshot.DiskActivePercent.HasValue ? snapshot.Timestamp.UtcDateTime : null,
                CpuTempLastUpdatedUtc = snapshot.CpuTempC.HasValue ? snapshot.Timestamp.UtcDateTime : null,
                GpuTempLastUpdatedUtc = snapshot.GpuTempC.HasValue ? snapshot.Timestamp.UtcDateTime : null,
                GpuAdapters = snapshot.GpuAdapters,
                ActiveGpuName = snapshot.ActiveGpuName,
                ActiveGpuPercent = snapshot.ActiveGpuPercent,
                ActiveGpuTemp = snapshot.ActiveGpuTempC
            });
        }

        private static double OrZero(double value)
            => double.IsNaN(value) || double.IsInfinity(value) ? 0d : value;

        private (bool IsAvailable, ulong? TotalBytes, ulong? UsedBytes, double? Percent) ReadRamMetrics()
        {
            var status = new MemoryStatusEx();
            try
            {
                if (!GlobalMemoryStatusEx(ref status) || status.ullTotalPhys == 0)
                {
                    return (false, null, null, null);
                }

                var total = status.ullTotalPhys;
                var available = Math.Min(status.ullAvailPhys, total);
                var used = total - available;
                var percent = ClampPercent((double)used / total * 100d);
                return (true, total, used, percent);
            }
            catch
            {
                return (false, null, null, null);
            }
        }

        private static double? ReadCounter(PerformanceCounter? counter, double min, double? max)
        {
            if (counter == null)
            {
                return null;
            }

            try
            {
                var value = counter.NextValue();
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    return null;
                }

                var asDouble = (double)value;
                if (asDouble < min)
                {
                    asDouble = min;
                }

                if (max.HasValue && asDouble > max.Value)
                {
                    asDouble = max.Value;
                }

                return asDouble;
            }
            catch
            {
                return null;
            }
        }

        private GpuSample ReadGpuSample()
        {
            if (_gpuCountersByAdapter.Count == 0)
            {
                return GpuSample.Empty;
            }

            try
            {
                var adapters = new List<GpuAdapterMetric>();
                foreach (var (adapterName, counters) in _gpuCountersByAdapter)
                {
                    var percent3D = SumCounters(counters.ThreeD);
                    var percentTotal = SumCounters(counters.Total);
                    if (!percent3D.HasValue && !percentTotal.HasValue)
                    {
                        continue;
                    }

                    adapters.Add(new GpuAdapterMetric(adapterName, percent3D, percentTotal));
                }

                if (adapters.Count == 0)
                {
                    return GpuSample.Empty;
                }

                var active = adapters
                    .OrderByDescending(a => a.Percent3D ?? double.MinValue)
                    .ThenByDescending(a => a.PercentTotal ?? double.MinValue)
                    .First();

                return new GpuSample(
                    adapters,
                    active.Name,
                    active.Percent3D ?? active.PercentTotal);
            }
            catch
            {
                return GpuSample.Empty;
            }
        }

        private static double? SumCounters(IEnumerable<PerformanceCounter> counters)
        {
            var sum = 0d;
            var found = false;
            foreach (var counter in counters)
            {
                try
                {
                    var value = counter.NextValue();
                    if (float.IsNaN(value) || float.IsInfinity(value))
                    {
                        continue;
                    }

                    sum += value;
                    found = true;
                }
                catch
                {
                    // Ignore single counter errors.
                }
            }

            return found ? ClampPercent(sum) : null;
        }

        private static Dictionary<string, GpuAdapterCounters> BuildGpuCountersByAdapter()
        {
            var gpuCategory = new PerformanceCounterCategory("GPU Engine");
            var names = gpuCategory.GetInstanceNames();
            var byAdapter = new Dictionary<string, GpuAdapterCounters>(StringComparer.OrdinalIgnoreCase);

            foreach (var instanceName in names)
            {
                var counters = gpuCategory.GetCounters(instanceName);
                var utilCounter = counters.FirstOrDefault(c => c.CounterName.Equals("Utilization Percentage", StringComparison.OrdinalIgnoreCase));
                if (utilCounter == null)
                {
                    continue;
                }

                var adapterName = ExtractAdapterName(instanceName);
                if (string.IsNullOrWhiteSpace(adapterName))
                {
                    continue;
                }

                if (!byAdapter.TryGetValue(adapterName, out var adapterCounters))
                {
                    adapterCounters = new GpuAdapterCounters(new List<PerformanceCounter>(), new List<PerformanceCounter>());
                    byAdapter[adapterName] = adapterCounters;
                }

                adapterCounters.Total.Add(utilCounter);

                if (instanceName.IndexOf("engtype_3D", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    adapterCounters.ThreeD.Add(utilCounter);
                }
            }

            return byAdapter;
        }

        private static string ExtractAdapterName(string instanceName)
        {
            const string pidToken = "pid_";
            var pidIndex = instanceName.IndexOf(pidToken, StringComparison.OrdinalIgnoreCase);
            if (pidIndex <= 0)
            {
                return instanceName;
            }

            var name = instanceName[..pidIndex].TrimEnd('_');
            return string.IsNullOrWhiteSpace(name) ? instanceName : name;
        }

        private TempReadResult ReadTemperatures()
        {
            if (!_isHardwareAvailable || _computer == null)
            {
                return new TempReadResult(false, "Unavailable", null, null, null, null);
            }

            double? cpu = null;
            double? gpu = null;
            string? cpuSensorName = null;
            string? gpuSensorName = null;

            foreach (var hw in _computer.Hardware)
            {
                try
                {
                    hw.Update();
                    switch (hw.HardwareType)
                    {
                        case HardwareType.Cpu:
                            foreach (var s in hw.Sensors)
                            {
                                if (s.SensorType == SensorType.Temperature && s.Name.Contains("Package", StringComparison.OrdinalIgnoreCase))
                                {
                                    var value = RoundTemperature(s.Value);
                                    if (value.HasValue)
                                    {
                                        cpu = value;
                                        cpuSensorName = s.Name;
                                    }
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
                                    var value = RoundTemperature(s.Value);
                                    if (value.HasValue)
                                    {
                                        gpu = value;
                                        gpuSensorName = s.Name;
                                        break;
                                    }
                                }
                            }
                            break;
                    }
                }
                catch
                {
                    // Best effort only.
                }
            }

            return new TempReadResult(true, "LibreHardwareMonitor", cpuSensorName, gpuSensorName, cpu, gpu);
        }

        private void BuildDiagnostics(SystemMetricsSnapshot snapshot, double? cpuRaw, double? cpuAvg, double? diskRaw, double? diskAvg, double? gpuRaw, double? gpuAvg)
        {
            var total = snapshot.RamTotalBytes?.ToString() ?? "unavailable";
            var used = snapshot.RamUsedBytes?.ToString() ?? "unavailable";
            var avail = snapshot.RamTotalBytes.HasValue && snapshot.RamUsedBytes.HasValue
                ? (snapshot.RamTotalBytes.Value - snapshot.RamUsedBytes.Value).ToString()
                : "unavailable";

            LastDiagnostics =
                $"MonitoringIntervalMs={_monitoringInterval.TotalMilliseconds:F0} " +
                $"LastSampleTimestamp={snapshot.Timestamp:O} " +
                $"SampleAgeMs={snapshot.SampleAgeMs:F0} " +
                $"ts={snapshot.Timestamp:O} ageMs={snapshot.SampleAgeMs:F0} " +
                $"ram(total={total},avail={avail},used={used},pct={FormatDiag(snapshot.RamPercent)}) " +
                $"cpu(raw={FormatDiag(cpuRaw)},avg={FormatDiag(cpuAvg)}) " +
                $"disk(raw={FormatDiag(diskRaw)},avg={FormatDiag(diskAvg)},readBps={FormatDiag(snapshot.DiskReadBps)},writeBps={FormatDiag(snapshot.DiskWriteBps)}) " +
                $"gpu(provider={snapshot.ProviderName},active={snapshot.ActiveGpuName ?? "unavailable"},raw={FormatDiag(gpuRaw)},avg={FormatDiag(gpuAvg)},adapters={FormatGpuAdapters(snapshot.GpuAdapters)}) " +
                $"temp(provider={snapshot.TempProviderName},cpuSensor={snapshot.CpuTempSensorName ?? "unavailable"},gpuSensor={snapshot.GpuTempSensorName ?? "unavailable"},cpuRaw={FormatDiag(snapshot.CpuTempRawC)},cpuSmoothed={FormatDiag(snapshot.CpuTempSmoothedC)},gpuRaw={FormatDiag(snapshot.GpuTempRawC)},gpuSmoothed={FormatDiag(snapshot.GpuTempSmoothedC)}) " +
                $"lastError={_lastError}";

            if (IsDiagnosticMode())
            {
                Trace.WriteLine($"Monitoring diagnostics: {LastDiagnostics}");
            }
        }

        private static string FormatDiag(double? value)
            => value.HasValue ? value.Value.ToString("0.0") : "unavailable";

        private static string FormatGpuAdapters(IReadOnlyList<GpuAdapterMetric> adapters)
        {
            if (adapters.Count == 0)
            {
                return "none";
            }

            return string.Join(",", adapters.Select(a => $"{a.Name}:3D={FormatDiag(a.Percent3D)} total={FormatDiag(a.PercentTotal)}"));
        }

        private static string BuildSourceFlags(bool ram, bool cpu, bool disk, bool gpu, bool temp)
            => $"RAM={(ram ? "Win32GlobalMemoryStatusEx" : "Unavailable")};CPU={(cpu ? "PerfCounter" : "Unavailable")};DISK={(disk ? "PerfCounter" : "Unavailable")};GPU={(gpu ? "GPUEngine" : "Unavailable")};TEMP={(temp ? "LibreHardwareMonitor" : "Unavailable")}";

        private static double? RoundTemperature(float? value)
            => value.HasValue ? RoundTemperature((double?)value.Value) : null;

        private static double? RoundTemperature(double? value)
        {
            if (!value.HasValue || double.IsNaN(value.Value) || double.IsInfinity(value.Value))
            {
                return null;
            }

            return Math.Round(value.Value, 1, MidpointRounding.AwayFromZero);
        }

        private static double? ValidateTemperatureSample(double? candidateRaw, ref double? lastAcceptedRaw)
        {
            if (!candidateRaw.HasValue)
            {
                return null;
            }

            var value = candidateRaw.Value;
            if (value < 10d || value > 110d)
            {
                return null;
            }

            if (lastAcceptedRaw.HasValue && Math.Abs(value - lastAcceptedRaw.Value) > 25d)
            {
                return null;
            }

            lastAcceptedRaw = value;
            return value;
        }

        private static double ClampPercent(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return 0d;
            }

            return Math.Clamp(Math.Round(value, 1, MidpointRounding.AwayFromZero), 0d, 100d);
        }

        private static bool IsDiagnosticMode()
        {
#if DEBUG
            return true;
#else
            return string.Equals(Environment.GetEnvironmentVariable("VIRGIL_DIAGNOSTIC"), "1", StringComparison.OrdinalIgnoreCase);
#endif
        }


        private sealed record TempReadResult(
            bool IsProviderAvailable,
            string ProviderName,
            string? CpuSensorName,
            string? GpuSensorName,
            double? CpuTemp,
            double? GpuTemp);

        private sealed record GpuAdapterCounters(List<PerformanceCounter> ThreeD, List<PerformanceCounter> Total);

        private sealed record GpuSample(
            IReadOnlyList<GpuAdapterMetric> Adapters,
            string? ActiveGpuName,
            double? ActiveGpuPercent3D)
        {
            public static GpuSample Empty { get; } = new(Array.Empty<GpuAdapterMetric>(), null, null);
        }

        private void LogTickDuration(TimeSpan duration, bool sampled)
        {
            var status = sampled ? "sampled" : "skipped";
            Trace.WriteLine($"Monitoring tick {status} in {duration.TotalMilliseconds:F0} ms.");
            if (LastTelemetryUpdateUtc.HasValue)
            {
                Trace.WriteLine($"Monitoring last refresh at {LastTelemetryUpdateUtc:O}.");
            }
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

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MemoryStatusEx
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;

            public MemoryStatusEx()
            {
                dwLength = (uint)Marshal.SizeOf(typeof(MemoryStatusEx));
                dwMemoryLoad = 0;
                ullTotalPhys = 0;
                ullAvailPhys = 0;
                ullTotalPageFile = 0;
                ullAvailPageFile = 0;
                ullTotalVirtual = 0;
                ullAvailVirtual = 0;
                ullAvailExtendedVirtual = 0;
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);

        private sealed class FixedWindowAverage
        {
            private readonly int _size;
            private readonly Queue<double> _values = new();

            public FixedWindowAverage(int size)
            {
                _size = Math.Max(1, size);
            }

            public double? AddAndAverage(double? value)
            {
                if (!value.HasValue || double.IsNaN(value.Value) || double.IsInfinity(value.Value))
                {
                    return _values.Count == 0 ? null : _values.Average();
                }

                _values.Enqueue(value.Value);
                while (_values.Count > _size)
                {
                    _values.Dequeue();
                }

                return _values.Average();
            }
        }
    }
}
