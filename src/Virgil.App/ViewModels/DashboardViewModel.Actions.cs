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
                await _narration.OnActionCompletedAsync(actionId, true, token);
            }
            catch (Exception)
            {
                await _narration.OnActionCompletedAsync(actionId, false, token);
            }
        }

        public void RunMaintenance() => _ = RunMaintenanceAsync();

        private Task RunMaintenanceAsync()
        {
            return RunWithNarrationAsync("maintenance_full", () => _systemActions.RunMaintenanceAsync());
        }

        public void RunSmartCleanup() => _ = RunSmartCleanupAsync();

        private Task RunSmartCleanupAsync()
        {
            return RunWithNarrationAsync("smart_cleanup", () => _systemActions.RunSmartCleanupAsync());
        }

        public void RunBrowsersCleanup() => _ = RunBrowsersCleanupAsync();

        private Task RunBrowsersCleanupAsync()
        {
            // TODO: wire to real browsers cleanup when available in SystemActionsService or a dedicated service.
            return RunWithNarrationAsync("browsers_clean", () => Task.CompletedTask);
        }

        public void RunUpdateAll() => _ = RunUpdateAllAsync();

        private Task RunUpdateAllAsync()
        {
            // TODO: wire to real "update all" logic when the service API is defined.
            return RunWithNarrationAsync("updates_all", () => Task.CompletedTask);
        }

        public void RunDefenderScan() => _ = RunDefenderScanAsync();

        private Task RunDefenderScanAsync()
        {
            return RunWithNarrationAsync("defender_scan", () => _systemActions.RunDefenderAsync());
        }

        public void OpenConfiguration() => _ = OpenConfigurationAsync();

        private Task OpenConfigurationAsync()
        {
            // TODO: wire to real configuration opening logic (system or app settings).
            return RunWithNarrationAsync("open_config", () => Task.CompletedTask);
        }
    }
}
