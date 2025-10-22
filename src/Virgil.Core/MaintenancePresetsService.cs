using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Virgil.Core
{
    /// <summary>
    /// Regroupe des actions “prêtes à l’emploi” (presets) : nettoyage rapide,
    /// mises à jour logicielles, cycle Windows Update, etc.
    /// Cette implémentation n’a pas de dépendances UI et renvoie des logs texte.
    /// </summary>
    public sealed class MaintenancePresetsService
    {
        /// <summary>
        /// Nettoyage rapide : fichiers temporaires Windows + caches navigateurs.
        /// </summary>
        public async Task<string> QuickCleanAsync(bool forceBrowser = false)
        {
            var sb = new StringBuilder();

            try
            {
                // 1) Fichiers temporaires Windows (best-effort)
                var tempReport = CleanWindowsTemp();
                sb.AppendLine(tempReport);
            }
            catch (Exception ex)
            {
                sb.AppendLine($"[QuickClean] Erreur nettoyage TEMP: {ex.Message}");
            }

            try
            {
                // 2) Caches navigateurs (via BrowserCleaningService que nous avons dans le repo)
                var bcs = new BrowserCleaningService();
                if (!forceBrowser && bcs.IsAnyBrowserRunning())
                {
                    sb.AppendLine("[QuickClean] Un navigateur est ouvert — nettoyage des caches ignoré (Force=false).");
                }
                else
                {
                    var rep = bcs.AnalyzeAndClean(new BrowserCleaningOptions { Force = forceBrowser });
                    sb.AppendLine($"[QuickClean] Caches navigateurs détectés: ~{rep.BytesFound / (1024.0 * 1024):F1} MB");
                    sb.AppendLine($"[QuickClean] Caches navigateurs supprimés: ~{rep.BytesDeleted / (1024.0 * 1024):F1} MB");
                    if (rep.SkippedReasons.Count > 0)
                        sb.AppendLine($"[QuickClean] Skips: {string.Join(" | ", rep.SkippedReasons)}");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"[QuickClean] Erreur nettoyage navigateurs: {ex.Message}");
            }

            await Task.CompletedTask;
            return sb.ToString();
        }

        /// <summary>
        /// Mises à jour via winget pour toutes les applis/jeux/pilotes connus de winget.
        /// </summary>
        public async Task<string> UpdateAllAppsAsync()
        {
            try
            {
                var us = new UpdateService();
                var output = await us.UpgradeAllAsync().ConfigureAwait(false);
                return string.IsNullOrWhiteSpace(output)
                    ? "[AppsUpdate] Aucune mise à jour winget détectée ou sortie vide."
                    : output;
            }
            catch (Exception ex)
            {
                return $"[AppsUpdate] Erreur: {ex.Message}";
            }
        }

        /// <summary>
        /// Cycle Windows Update (Scan → Download → Install). Optionnellement Restart.
        /// </summary>
        public async Task<string> WindowsUpdateFullAsync(bool andRestart = false)
        {
            try
            {
                var wus = new WindowsUpdateService();
                var agg = await wus.UpdateWindowsAsync(andRestart).ConfigureAwait(false);
                // Convertir l’agrégat en texte lisible
                return agg.Join();
            }
            catch (Exception ex)
            {
                return $"[WindowsUpdate] Erreur: {ex.Message}";
            }
        }

        /// <summary>
        /// “Preset complet” : QuickClean + MAJ apps + Windows Update.
        /// </summary>
        public async Task<string> FullMaintenanceAsync(bool forceBrowser = false, bool windowsRestart = false)
        {
            var sb = new StringBuilder();

            sb.AppendLine("=== Maintenance complète ===");
            sb.AppendLine("-- Nettoyage rapide --");
            sb.AppendLine(await QuickCleanAsync(forceBrowser).ConfigureAwait(false));

            sb.AppendLine("-- Mises à jour applications (winget) --");
            sb.AppendLine(await UpdateAllAppsAsync().ConfigureAwait(false));

            sb.AppendLine("-- Windows Update (scan→download→install) --");
            sb.AppendLine(await WindowsUpdateFullAsync(windowsRestart).ConfigureAwait(false));

            sb.AppendLine("=== Fin maintenance ===");
            return sb.ToString();
        }

        // ----------------------------------------------------------
        // Helpers internes
        // ----------------------------------------------------------

        /// <summary>
        /// Nettoie les dossiers temporaires Windows en best-effort et retourne un résumé.
        /// </summary>
        private static string CleanWindowsTemp()
        {
            var targets = new[]
            {
                Environment.ExpandEnvironmentVariables("%TEMP%"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"),
            };

            long found = 0;
            int deleted = 0;

            foreach (var t in targets)
            {
                if (string.IsNullOrWhiteSpace(t) || !Directory.Exists(t)) continue;

                try
                {
                    foreach (var f in Directory.EnumerateFiles(t, "*", SearchOption.AllDirectories))
                    {
                        try { found += new FileInfo(f).Length; } catch { }
                    }

                    foreach (var f in Directory.EnumerateFiles(t, "*", SearchOption.AllDirectories))
                    { try { File.Delete(f); deleted++; } catch { } }

                    foreach (var d in Directory.EnumerateDirectories(t, "*", SearchOption.AllDirectories))
                    { try { Directory.Delete(d, true); } catch { } }
                }
                catch { /* ignore dossier inaccessible */ }
            }

            var sb = new StringBuilder();
            sb.AppendLine($"[Temp] Fichiers temporaires détectés: ~{found / (1024.0 * 1024):F1} MB");
            sb.AppendLine($"[Temp] Fichiers supprimés: {deleted}");
            return sb.ToString();
        }
    }
}
