using System;
using System.Threading;
using System.Threading.Tasks;

namespace Virgil.App.ViewModels
{
    public partial class DashboardViewModel
    {
        private async Task RunWithNarrationAsync(string actionId, Func<Task> action)
        {
            var token = CancellationToken.None;

            try
            {
                await _narration.OnActionStartedAsync(actionId, token);
                await action();
                await _narration.OnActionCompletedAsync(actionId, success: true, token);
            }
            catch (Exception ex)
            {
                await _narration.OnActionCompletedAsync(actionId, success: false, token);
                AppendChat($"Erreur pendant '{actionId}' : {ex.Message}");
            }
        }

        public void RunMaintenance() => _ = RunMaintenanceAsync();

        private Task RunMaintenanceAsync() =>
            RunWithNarrationAsync("maintenance_full", () => _systemActions.RunMaintenanceAsync());

        public void RunSmartCleanup() => _ = RunSmartCleanupAsync();

        private Task RunSmartCleanupAsync() =>
            RunWithNarrationAsync("smart_cleanup", () => _systemActions.RunSmartCleanupAsync());

        public void RunBrowsersCleanup() => _ = RunBrowsersCleanupAsync();

        private Task RunBrowsersCleanupAsync() =>
            RunWithNarrationAsync("browsers_clean", () => _systemActions.RunBrowsersCleanupAsync());

        public void RunUpdateAll() => _ = RunUpdateAllAsync();

        private Task RunUpdateAllAsync() =>
            RunWithNarrationAsync("updates_all", () => _systemActions.RunUpdateAllAsync());

        public void RunDefenderScan() => _ = RunDefenderScanAsync();

        private Task RunDefenderScanAsync() =>
            RunWithNarrationAsync("defender_scan", () => _systemActions.RunDefenderScanAsync());

        public void OpenConfiguration() => _ = OpenConfigurationAsync();

        private Task OpenConfigurationAsync() =>
            RunWithNarrationAsync("open_config", () => _systemActions.OpenConfigAsync());
    }
}
