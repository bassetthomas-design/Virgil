using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

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
        // Optional counter for disk usage. Not all systems expose this counter, so it may be null.
        private readonly PerformanceCounter? _diskCounter;

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
            // Attempt to create a performance counter for disk usage. On systems where this counter
            // is unavailable, assignment will fail and we will fall back to DriveInfo.
            try
            {
                _diskCounter = new PerformanceCounter("LogicalDisk", "% Disk Time", "_Total");
            }
            catch
            {
                _diskCounter = null;
            }

            // Create the timer with a 1‑second interval. Fully qualified type name to disambiguate.
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
                // Update disk usage using the counter if available; otherwise fall back to DriveInfo.
                if (_diskCounter != null)
                {
                    try
                    {
                        LatestMetrics.DiskUsage = _diskCounter.NextValue();
                    }
                    catch
                    {
                        LatestMetrics.DiskUsage = 0f;
                    }
                }
                else
                {
                    try
                    {
                        var sysDrive = System.IO.DriveInfo.GetDrives()
                            .FirstOrDefault(d => d.IsReady && d.Name == System.IO.Path.GetPathRoot(Environment.SystemDirectory));
                        if (sysDrive != null)
                        {
                            var used = sysDrive.TotalSize - sysDrive.TotalFreeSpace;
                            LatestMetrics.DiskUsage = (float)(used * 100.0 / sysDrive.TotalSize);
                        }
                    }
                    catch
                    {
                        LatestMetrics.DiskUsage = 0f;
                    }
                }
                // GPU usage and temperature are not yet implemented; set to zero as stubs.
                LatestMetrics.GpuUsage = 0f;
                LatestMetrics.Temperature = 0f;

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

        /// <summary>
        /// Gets or sets the disk usage percentage (approximation).
        /// </summary>
        public float DiskUsage { get; set; }

        /// <summary>
        /// Gets or sets the GPU usage percentage (stub implementation).
        /// </summary>
        public float GpuUsage { get; set; }

        /// <summary>
        /// Gets or sets the temperature in degrees Celsius (stub implementation).
        /// </summary>
        public float Temperature { get; set; }
    }
}