*** Begin Patch
*** Update File: src/Virgil.App/Services/SystemMonitorService.cs
@@
-using System.Threading.Tasks;
+using System.Threading.Tasks;
+using System.Diagnostics;

@@
     public sealed class SystemMonitorService : ISystemMonitorService, IDisposable
     {
         private System.Threading.Timer? _timer;
         private bool _isRunning;
+
+        // Performance counters for CPU and memory usage
+        private readonly PerformanceCounter _cpuCounter;
+        private readonly PerformanceCounter _ramCounter;

@@
         public event EventHandler<SystemMonitorSnapshot>? SnapshotUpdated;

@@
         public Task StartAsync(CancellationToken cancellationToken)
         {
@@
             _isRunning = true;
@@
             _timer = new System.Threading.Timer(OnTick, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
+
+            // Prime the counters to avoid returning 0 on the first call
+            _cpuCounter?.NextValue();
+            _ramCounter?.NextValue();

             return Task.CompletedTask;
         }

@@
         private void OnTick(object? state)
         {
             if (!_isRunning)
                 return;

-            // TODO: replace with real system metrics (CPU, RAM, GPU, Disk, Temps)
-            var snapshot = new SystemMonitorSnapshot
-            {
-                CpuUsage = 0,
-                RamUsage = 0
-            };
+            // Collect real system metrics using performance counters
+            float cpuUsage = 0;
+            float ramUsage = 0;
+
+            try
+            {
+                cpuUsage = _cpuCounter?.NextValue() ?? 0;
+            }
+            catch
+            {
+                // ignore errors and fall back to 0
+                cpuUsage = 0;
+            }
+
+            try
+            {
+                ramUsage = _ramCounter?.NextValue() ?? 0;
+            }
+            catch
+            {
+                ramUsage = 0;
+            }
+
+            var snapshot = new SystemMonitorSnapshot
+            {
+                CpuUsage = cpuUsage,
+                RamUsage = ramUsage
+                // TODO: capture GPU usage, Disk activity and temperatures using appropriate APIs
+            };

*** End Patch
