using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Virgil.Core.Services;

namespace Virgil.Core.Services
{
    /// <summary>
    /// Report returned by <see cref="MaintenanceService"/> containing aggregated statistics
    /// about the full maintenance run.
    /// </summary>
    public sealed class MaintenanceReport
    {
        public long BytesFound { get; set; }
        public long BytesDeleted { get; set; }
        public List<BrowserCleaningReport> BrowserReports { get; } = new();
        public List<string> UpdateLogs { get; } = new();
        public override string ToString()
        {
            var lines = new List<string>();
            lines.Add($"Nettoyage – trouvé: {BytesFound / (1024.0 * 1024):F1} MB, supprimé: {BytesDeleted / (1024.0 * 1024):F1} MB");
            foreach (var br in BrowserReports)
                lines.Add(br.ToString());
            lines.AddRange(UpdateLogs);
            return string.Join(Environment.NewLine, lines);
        }
    }

    /// <summary>
    /// Orchestrates a full maintenance by performing intelligent cleaning, browser cleaning and updates.
    /// </summary>
    public sealed class MaintenanceService
    {
        private readonly CleaningService _cleaning = new();
        private readonly ExtendedCleaningService _extended = new();
        private readonly BrowserCleaningService _browser = new();
        private readonly ApplicationUpdateService _apps = new();
        private readonly WindowsUpdateService _windows = new();
        private readonly DriverUpdateService _drivers = new();
        private readonly DefenderUpdateService _defender = new();

        public async Task<MaintenanceReport> RunFullMaintenanceAsync(IProgress<string>? progress = null, CancellationToken ct = default)
        {
            var report = new MaintenanceReport();

            // Step 1: clean temporary files (simple + extended)
            progress?.Report("Nettoyage des fichiers temporaires...");
            string tempResult = await _cleaning.CleanTempAsync().ConfigureAwait(false);
            progress?.Report(tempResult);
            string extResult = await _extended.CleanAsync().ConfigureAwait(false);
            progress?.Report(extResult);
            // Parsing results is left to caller; here we simply append to report.

            // Step 2: clean browser caches
            progress?.Report("Nettoyage des navigateurs...");
            var bReport = _browser.AnalyzeAndClean(new BrowserCleaningOptions());
            report.BytesFound += bReport.BytesFound;
            report.BytesDeleted += bReport.BytesDeleted;
            report.BrowserReports.Add(bReport);
            progress?.Report(bReport.ToString());

            // Step 3: application and system updates
            progress?.Report("Mise à jour des applications...");
            report.UpdateLogs.Add(await _apps.UpgradeAllAsync(includeUnknown: true, silent: true).ConfigureAwait(false));
            progress?.Report("Vérification et installation des mises à jour Windows...");
            report.UpdateLogs.Add(await _windows.StartScanAsync().ConfigureAwait(false));
            report.UpdateLogs.Add(await _windows.StartDownloadAsync().ConfigureAwait(false));
            report.UpdateLogs.Add(await _windows.StartInstallAsync().ConfigureAwait(false));
            progress?.Report("Mise à jour des pilotes...");
            report.UpdateLogs.Add(await _drivers.UpgradeDriversAsync().ConfigureAwait(false));
            progress?.Report("Mise à jour de Microsoft Defender...");
            report.UpdateLogs.Add(await _defender.UpdateSignaturesAsync().ConfigureAwait(false));
            report.UpdateLogs.Add(await _defender.QuickScanAsync().ConfigureAwait(false));

            return report;
        }
    }
}
  
