using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Virgil.Core.Services
{
    /// <summary>
    /// Provides a high-level "smart" cleaning routine that determines the level
    /// of maintenance to perform based on current disk space and memory
    /// utilisation. This class orchestrates simple, full and deep cleaning
    /// without requiring the caller to pick a mode explicitly. It reports
    /// progress messages through an optional <see cref="IProgress{T}"/>.
    ///
    /// This implementation uses best ‑effort heuristics to gauge system
    /// utilisation. It relies on <see cref="DriveInfo"/> to measure free disk
    /// space and runtime GC memory information to approximate memory usage.
    /// If those APIs fail, it falls back to conservative defaults. The
    /// thresholds can be adjusted if needed.
    /// </summary>
    public sealed class SmartCleaningService
    {
        private readonly CleaningService _cleaning = new();
        private readonly ExtendedCleaningService _extended = new();
        private readonly BrowserCleaningService _browser = new();
        private readonly MaintenanceService _maintenance = new();

        /// <summary>
        /// Analyse the system state and perform an appropriate cleaning mode.
        /// </summary>
        /// <param name="progress">Progress reporter used to communicate user ‑facing messages.</param>
        /// <param name="ct">Cancellation token to abort the operation.</param>
        /// <returns>A human readable summary of the performed actions.</returns>
        public async Task<string> RunSmartCleanAsync(IProgress<string>? progress = null, CancellationToken ct = default)
        {
            // Compute the overall free disk ratio across all drives
            double freeRatio = 1.0;
            try
            {
                long total = 0, free = 0;
                foreach (var d in DriveInfo.GetDrives())
                {
                    try
                    {
                        if (d.IsReady)
                        {
                            total += d.TotalSize;
                            free += d.AvailableFreeSpace;
                        }
                    }
                    catch
                    {
                        // ignore drives we cannot query
                    }
                }
                if (total > 0)
                    freeRatio = (double)free / total;
            }
            catch { /* ignore errors */ }

            // Compute approximate memory utilisation
            double memUsage = 0.0;
            try
            {
                // GC memory info provides a cross-platform view of memory pressure.
                var info = GC.GetGCMemoryInfo();
                var totalMem = info.TotalAvailableMemoryBytes;
                var usedMem = Math.Max(info.HeapSizeBytes, info.TotalCommittedBytes);

                if (totalMem > 0 && usedMem >= 0)
                {
                    memUsage = Math.Min(1.0, usedMem / (double)totalMem);
                }
                else
                {
                    // Fallback to the process working set if total memory is unavailable.
                    memUsage = ComputeWorkingSetRatio();
                }
            }
            catch
            {
                // Fallback: use GC memory as a very rough proxy
                try
                {
                    long total = GC.GetTotalMemory(forceFullCollection: false);
                    memUsage = Math.Min(1.0, total / (double)(1024L * 1024L * 1024L));
                }
                catch
                {
                    memUsage = ComputeWorkingSetRatio();
                }
            }

            // Decide the cleaning level based on heuristics
            string level;
            if (freeRatio > 0.30 && memUsage < 0.60)
            {
                level = "simple";
            }
            else if (freeRatio > 0.10 && memUsage < 0.80)
            {
                level = "full";
            }
            else
            {
                level = "deep";
            }

            progress?.Report($"Mode de nettoyage automatique choisi: {level}");
            progress?.Report($"Espace disque libre: {freeRatio * 100:F1}%, utilisation mémoire: {memUsage * 100:F1}%");

            // Execute the selected cleaning routine
            switch (level)
            {
                case "simple":
                    // Clean temporary files and browser caches
                    ct.ThrowIfCancellationRequested();
                    string temp = await _cleaning.CleanTempAsync().ConfigureAwait(false);
                    progress?.Report(temp);
                    ct.ThrowIfCancellationRequested();
                    var brSimple = _browser.AnalyzeAndClean(new BrowserCleaningOptions());
                    progress?.Report(brSimple.ToString());
                    return $"[Simple] Nettoyage terminé\n{temp}\n{brSimple}";

                case "full":
                    // Use the full maintenance pipeline (temp + extended + browser + updates)
                    ct.ThrowIfCancellationRequested();
                    var maintenanceReport = await _maintenance.RunFullMaintenanceAsync(progress, ct).ConfigureAwait(false);
                    return $"[Complet] Nettoyage terminé\n{maintenanceReport}";

                default:
                    // Deep cleaning: include extended cleaning as well
                    ct.ThrowIfCancellationRequested();
                    string t = await _cleaning.CleanTempAsync().ConfigureAwait(false);
                    progress?.Report(t);
                    ct.ThrowIfCancellationRequested();
                    string ext = await _extended.CleanAsync().ConfigureAwait(false);
                    progress?.Report(ext);
                    ct.ThrowIfCancellationRequested();
                    var brDeep = _browser.AnalyzeAndClean(new BrowserCleaningOptions());
                    progress?.Report(brDeep.ToString());
                    return $"[Approfondi] Nettoyage terminé\n{t}\n{ext}\n{brDeep}";
            }
        }

        private static double ComputeWorkingSetRatio()
        {
            try
            {
                // Environment.WorkingSet returns the current process physical memory usage in bytes.
                // We approximate total physical memory with TotalAvailableMemoryBytes when possible.
                var info = GC.GetGCMemoryInfo();
                var totalMem = info.TotalAvailableMemoryBytes;
                if (totalMem > 0)
                {
                    return Math.Min(1.0, Environment.WorkingSet / (double)totalMem);
                }
            }
            catch
            {
                // ignored
            }

            return 0.0;
        }
    }
}
