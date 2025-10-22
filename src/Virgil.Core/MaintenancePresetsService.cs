using System.Text;
using System.Threading.Tasks;
using Virgil.Core.Services;

namespace Virgil.Core
{
    /// <summary>
    /// Scénarios de maintenance utilisables par l’UI (rapide / complète).
    /// </summary>
    public class MaintenancePresetsService
    {
        public async Task<string> QuickAsync()
        {
            var log = new StringBuilder();

            // TEMP simple (Get + Clean)
            var clean = new CleaningService();
            var size = clean.GetTempFilesSize();
            log.AppendLine($"[TEMP] Trouvé ~{size / (1024.0 * 1024):F1} MB");
            clean.CleanTempFiles();
            log.AppendLine("[TEMP] Nettoyage effectué.");

            // Navigateurs
            var browsers = new BrowserCleaningService();
            var rep = browsers.AnalyzeAndClean(new BrowserCleaningOptions { Force = false });
            log.AppendLine($"[BROWSERS] ~{rep.BytesFound / (1024.0 * 1024):F1} MB → ~{rep.BytesDeleted / (1024.0 * 1024):F1} MB");

            // Apps
            var apps = new ApplicationUpdateService();
            log.AppendLine(await apps.UpgradeAllAsync(includeUnknown: true, silent: true).ConfigureAwait(false));

            return log.ToString();
        }

        public async Task<string> FullAsync()
        {
            var log = new StringBuilder();

            // TEMP
            var clean = new CleaningService();
            var size = clean.GetTempFilesSize();
            log.AppendLine($"[TEMP] Trouvé ~{size / (1024.0 * 1024):F1} MB");
            clean.CleanTempFiles();
            log.AppendLine("[TEMP] Nettoyage effectué.");

            // Navigateurs
            var browsers = new BrowserCleaningService();
            var rep = browsers.AnalyzeAndClean(new BrowserCleaningOptions { Force = false });
            log.AppendLine($"[BROWSERS] ~{rep.BytesFound / (1024.0 * 1024):F1} MB → ~{rep.BytesDeleted / (1024.0 * 1024):F1} MB");

            // Étendu
            var ext = new ExtendedCleaningService();
            var exRep = ext.AnalyzeAndClean();
            log.AppendLine($"[EXTENDED] ~{exRep.BytesFound / (1024.0 * 1024):F1} MB → ~{exRep.BytesDeleted / (1024.0 * 1024):F1} MB");

            // Apps
            var apps = new ApplicationUpdateService();
            log.AppendLine(await apps.UpgradeAllAsync(includeUnknown: true, silent: true).ConfigureAwait(false));

            // Windows Update
            var wu = new WindowsUpdateService();
            log.AppendLine(await wu.StartScanAsync().ConfigureAwait(false));
            log.AppendLine(await wu.StartDownloadAsync().ConfigureAwait(false));
            log.AppendLine(await wu.StartInstallAsync().ConfigureAwait(false));

            return log.ToString();
        }
    }
}
