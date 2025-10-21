using System.Text;
using System.Threading.Tasks;

namespace Virgil.Core
{
    /// <summary>
    /// Provides a high-level method to run a complete maintenance routine, performing
    /// cleaning of temporary files and browser caches, updating applications,
    /// drivers and Windows. The service returns a textual summary of the
    /// operations performed.
    /// </summary>
    public class MaintenancePresetsService
    {
        private readonly CleaningService _cleaningService;
        private readonly BrowserCleaningService _browserCleaningService;
        private readonly ApplicationUpdateService _appUpdateService;
        private readonly DriverUpdateService _driverUpdateService;
        private readonly WindowsUpdateService _windowsUpdateService;

        public MaintenancePresetsService(
            CleaningService cleaningService,
            BrowserCleaningService browserCleaningService,
            ApplicationUpdateService appUpdateService,
            DriverUpdateService driverUpdateService,
            WindowsUpdateService windowsUpdateService)
        {
            _cleaningService = cleaningService;
            _browserCleaningService = browserCleaningService;
            _appUpdateService = appUpdateService;
            _driverUpdateService = driverUpdateService;
            _windowsUpdateService = windowsUpdateService;
        }

        /// <summary>
        /// Executes all maintenance tasks sequentially and returns a summary string.
        /// </summary>
        public async Task<string> RunFullMaintenanceAsync()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Exécution du programme d’entretien complet…");

            // Clean temporary files
            var tempSize = _cleaningService.GetTempFilesSize();
            _cleaningService.CleanTempFiles();
            sb.AppendLine($"Fichiers temporaires nettoyés ({tempSize / (1024.0 * 1024.0):F1} MB).");

            // Clean browser caches
            sb.AppendLine(_browserCleaningService.CleanBrowserCaches());

            // Update applications
            sb.AppendLine("Mise à jour des applications en cours…");
            sb.AppendLine(await _appUpdateService.UpdateAllApplicationsAsync().ConfigureAwait(false));

            // Update drivers
            sb.AppendLine("Mise à jour des pilotes en cours…");
            sb.AppendLine(await _driverUpdateService.UpdateDriversAsync().ConfigureAwait(false));

            // Update Windows
            sb.AppendLine("Mise à jour de Windows en cours…");
            sb.AppendLine(await _windowsUpdateService.UpdateWindowsAsync().ConfigureAwait(false));

            sb.AppendLine("Programme d’entretien complet terminé.");
            return sb.ToString();
        }
    }
}