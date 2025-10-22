#nullable enable
using System;
using System.Text;
using System.Threading.Tasks;

namespace Virgil.Core.Services
{
    /// <summary>
    /// Exécute des scénarios d’entretien “rapide” et “complet” en enchaînant les services.
    /// Tout est best-effort (aucune exception levée vers l’appelant).
    /// </summary>
    public sealed class MaintenancePresetsService
    {
        private readonly CleaningService _tempCleaner = new();
        private readonly BrowserCleaningService _browserCleaner = new();
        private readonly ExtendedCleaningService _extendedCleaner = new();
        private readonly ApplicationUpdateService _appUpdater = new();
        private readonly DriverUpdateService _driverUpdater = new();
        private readonly WindowsUpdateService _wu = new();

        /// <summary>
        /// Entretien rapide : TEMP + caches navigateurs.
        /// </summary>
        public async Task<string> RunQuickAsync()
        {
            var sb = new StringBuilder();

            try
            {
                // TEMP
                var tempReport = await Task.Run(() =>
                {
                    var stats = _tempCleaner.CleanTempWithStats();
                    return $"TEMP: ~{stats.BytesFound / (1024.0 * 1024):F1} MB → ~{stats.BytesDeleted / (1024.0 * 1024):F1} MB supprimés, {stats.FilesDeleted} fichiers.";
                }).ConfigureAwait(false);
                sb.AppendLine(tempReport);
            }
            catch (Exception ex) { sb.AppendLine($"[TEMP] Erreur: {ex.Message}"); }

            try
            {
                // Navigateurs
                var browserReport = await Task.Run(() =>
                {
                    var rep = _browserCleaner.AnalyzeAndClean(new BrowserCleaningOptions { Force = false });
                    return $"Browsers: ~{rep.BytesFound / (1024.0 * 1024):F1} MB → ~{rep.BytesDeleted / (1024.0 * 1024):F1} MB.";
                }).ConfigureAwait(false);
                sb.AppendLine(browserReport);
            }
            catch (Exception ex) { sb.AppendLine($"[Browsers] Erreur: {ex.Message}"); }

            return sb.ToString();
        }

        /// <summary>
        /// Entretien complet : TEMP + navigateurs + nettoyage étendu + MAJ apps/jeux + Windows Update (+ pilotes best-effort).
        /// </summary>
        public async Task<string> RunFullAsync()
        {
            var sb = new StringBuilder();

            // TEMP
            try
            {
                var tempReport = await Task.Run(() =>
                {
                    var stats = _tempCleaner.CleanTempWithStats();
                    return $"TEMP: ~{stats.BytesFound / (1024.0 * 1024):F1} MB → ~{stats.BytesDeleted / (1024.0 * 1024):F1} MB supprimés, {stats.FilesDeleted} fichiers.";
                }).ConfigureAwait(false);
                sb.AppendLine(tempReport);
            }
            catch (Exception ex) { sb.AppendLine($"[TEMP] Erreur: {ex.Message}"); }

            // Navigateurs
            try
            {
                var browserReport = await Task.Run(() =>
                {
                    var rep = _browserCleaner.AnalyzeAndClean(new BrowserCleaningOptions { Force = false });
                    return $"Browsers: ~{rep.BytesFound / (1024.0 * 1024):F1} MB → ~{rep.BytesDeleted / (1024.0 * 1024):F1} MB.";
                }).ConfigureAwait(false);
                sb.AppendLine(browserReport);
            }
            catch (Exception ex) { sb.AppendLine($"[Browsers] Erreur: {ex.Message}"); }

            // Nettoyage étendu
            try
            {
                var extReport = await Task.Run(() =>
                {
                    var rep = _extendedCleaner.AnalyzeAndClean();
                    return $"Étendu: ~{rep.BytesFound / (1024.0 * 1024):F1} MB → ~{rep.BytesDeleted / (1024.0 * 1024):F1} MB.";
                }).ConfigureAwait(false);
                sb.AppendLine(extReport);
            }
            catch (Exception ex) { sb.AppendLine($"[Extended] Erreur: {ex.Message}"); }

            // MAJ apps / jeux (winget)
            try
            {
                var wingetOut = await _appUpdater.UpgradeAllAsync(includeUnknown: true, silent: true).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(wingetOut))
                    sb.AppendLine("[Winget] Terminé (voir détails ci-dessous) :").AppendLine(wingetOut.Trim());
                else
                    sb.AppendLine("[Winget] Terminé.");
            }
            catch (Exception ex) { sb.AppendLine($"[Winget] Erreur: {ex.Message}"); }

            // Windows Update (scan + download + install)
            try
            {
                await _wu.StartScanAsync().ConfigureAwait(false);
                await _wu.StartDownloadAsync().ConfigureAwait(false);
                var installOut = await _wu.StartInstallAsync().ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(installOut))
                    sb.AppendLine("[WU] Install :").AppendLine(installOut.Trim());
                else
                    sb.AppendLine("[WU] Install demandé.");
            }
            catch (Exception ex) { sb.AppendLine($"[Windows Update] Erreur: {ex.Message}"); }

            // Pilotes (best-effort)
            try
            {
                var dOut = await _driverUpdater.UpgradeDriversAsync().ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(dOut))
                    sb.AppendLine("[Drivers] Terminé (voir détails) :").AppendLine(dOut.Trim());
                else
                    sb.AppendLine("[Drivers] Vérification terminée.");
            }
            catch (Exception ex) { sb.AppendLine($"[Drivers] Erreur: {ex.Message}"); }

            return sb.ToString();
        }
    }
}
