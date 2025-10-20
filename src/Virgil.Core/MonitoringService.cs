using System;
using System.Diagnostics;

namespace Virgil.Core
{
    /// <summary>
    /// Provides real‑time system metrics such as CPU and memory usage.
    /// This service exposes an event that is raised whenever metrics
    /// are refreshed. Consumers can subscribe to <see cref="MetricsUpdated"/>
    /// and read the latest values from <see cref="LatestMetrics"/>.
    /// </summary>
    public class MonitoringService
    {
        // Use fully qualified System.Timers.Timer to avoid ambiguity with System.Threading.Timer.
        private readonly System.Timers.Timer _timer;
        private readonly PerformanceCounter _cpuCounter;
        private readonly PerformanceCounter _memCounter;

        /// <summary>
        /// Gets the most recently sampled metrics. This property is updated
        /// on the UI thread by the service at the configured interval.
        /// </summary>
        public SystemMetrics LatestMetrics { get; } = new SystemMetrics();

        /// <summary>
        /// Occurs when new metrics have been sampled.
        /// </summary>
        public event EventHandler? MetricsUpdated;

        public MonitoringService()
        {
            // Initialize performance counters for total CPU and memory usage.
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _memCounter = new PerformanceCounter("Memory", "% Committed Bytes In Use");

            // Create the timer with a 1‑second interval. Use the fully qualified type name to disambiguate.
            _timer = new System.Timers.Timer(1000);
            _timer.Elapsed += OnTimerElapsed;
        }

        /// <summary>
        /// Starts sampling system metrics at the configured interval.
        /// </summary>
        public void Start() => _timer.Start();

        /// <summary>
        /// Stops sampling system metrics.
        /// </summary>
        public void Stop() => _timer.Stop();

        private void OnTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                LatestMetrics.CpuUsage = _cpuCounter.NextValue();
                LatestMetrics.MemoryUsage = _memCounter.NextValue();
                MetricsUpdated?.Invoke(this, EventArgs.Empty);
            }
            catch
            {
                // Ignore transient performance counter errors.
            }
        }
    }

    /// <summary>
    /// Represents a snapshot of system resource utilisation.
    /// </summary>
    public class SystemMetrics
    {
        /// <summary>
        /// Gets or sets the CPU usage percentage across all cores.
        /// </summary>
        public float CpuUsage { get; set; }

        /// <summary>
        /// Gets or sets the memory usage percentage of committed bytes.
        /// </summary>
        public float MemoryUsage { get; set; }
    }
}