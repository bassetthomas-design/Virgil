using System.Text;
using System.Threading.Tasks;
using Virgil.Core.Services;

namespace Virgil.Core
{
    /// <summary>
    /// Scénarios de maintenance "one-click".
    /// </summary>
    public sealed class MaintenancePresetsService
    {
        public async Task<string> QuickCleanAsync(bool forceBrowser)
        {
            var sb = new StringBuilder();

            // 1) Temp standards
            var temp = new CleaningService();
            var size = temp.GetTempFilesSize();
            temp.CleanTempFiles();
            sb.AppendLine($"[Temp] cleaned ~{size / (1024.0 * 1024):F1} MB");

            // 2) Caches navigateurs
            var browsers = new BrowserCleaningService();
            var rep = browsers.AnalyzeAndClean(new BrowserCleaningOptions { Force = forceBrowser });
            sb.AppendLine($"[Browsers] {rep}");

            return sb.ToString();
        }

        public async Task<string> FullMaintenanceAsync(bool forceBrowser, bool windowsRestart)
        {
            var sb = new StringBuilder();

            // Nettoyage rapide
            sb.AppendLine(await QuickCleanAsync(forceBrowser));

            // Nettoyage étendu
            var ext = new ExtendedCleaningService();
            var exRep = ext.AnalyzeAndClean();
            sb.AppendLine($"[Extended] found ~{exRep.BytesFound / (1024.0 * 1024):F1} MB, deleted ~{exRep.BytesDeleted / (1024.0 * 1024):F1} MB");

            // MAJ apps/jeux
            var app = new ApplicationUpdateService();
            var appOut = await app.UpgradeAllAsync(includeUnknown: true, silent: true);
            if (!string.IsNullOrWhiteSpace(appOut))
            {
                sb.AppendLine("[Winget apps]");
                sb.AppendLine(appOut.Trim());
            }

            // Windows Update
            var wu = new WindowsUpdateService();
            var agg = await wu.UpdateWindowsAsync(restartAfter: windowsRestart);
            sb.AppendLine("[Windows Update]");
            sb.AppendLine(agg.ToString());

            return sb.ToString();
        }
    }
}
