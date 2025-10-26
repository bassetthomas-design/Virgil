using System.Text;
using System.Threading.Tasks;
using Virgil.Core.Services;

namespace Virgil.Core.Services
{
    public sealed class MaintenancePresetsService
    {
        public async Task<string> FullAsync()
        {
            var log = new StringBuilder();

            var clean = new CleaningService();
            log.AppendLine(await clean.CleanTempAsync().ConfigureAwait(false));

            var extended = new ExtendedCleaningService();
            log.AppendLine(await extended.CleanAsync().ConfigureAwait(false));

            var apps = new ApplicationUpdateService();
            log.AppendLine(await apps.UpgradeAllAsync(includeUnknown: true, silent: true).ConfigureAwait(false));

            var wu = new WindowsUpdateService();
            log.AppendLine(await wu.StartScanAsync().ConfigureAwait(false));
            log.AppendLine(await wu.StartDownloadAsync().ConfigureAwait(false));
            log.AppendLine(await wu.StartInstallAsync().ConfigureAwait(false));

            var drivers = new DriverUpdateService();
            log.AppendLine(await drivers.UpgradeDriversAsync().ConfigureAwait(false));

            var defender = new DefenderUpdateService();
            log.AppendLine(await defender.UpdateSignaturesAsync().ConfigureAwait(false));
            log.AppendLine(await defender.QuickScanAsync().ConfigureAwait(false));

            return log.ToString();
        }
    }
}
