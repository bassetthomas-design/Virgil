using System;
using System.Threading;
using System.Threading.Tasks;

namespace Virgil.App.Services
{
    public class QuickActionsService : IQuickActionsService
    {
        private readonly SystemActionsService _systemActions;

        public QuickActionsService(SystemActionsService systemActions)
        {
            _systemActions = systemActions ?? throw new ArgumentNullException(nameof(systemActions));
        }

        public Task ExecuteAsync(string actionId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(actionId))
                return Task.CompletedTask;

            switch (actionId)
            {
                case "maintenance_full":
                    return _systemActions.RunMaintenanceAsync();

                case "smart_cleanup":
                case "quick_clean":
                    return _systemActions.RunSmartCleanupAsync();

                case "defender_scan":
                    return _systemActions.RunDefenderAsync();

                // TODO: À brancher quand leurs services existeront réellement.
                case "browsers_clean":
                case "browser_soft_clean":
                case "browser_deep_clean":
                case "network_diag":
                case "network_soft_reset":
                case "network_hard_reset":
                case "network_latency_test":
                case "perf_mode_on":
                case "perf_mode_off":
                case "startup_analyze":
                case "gaming_kill_session":
                case "apps_update_all":
                case "windows_update":
                case "gpu_driver_check":
                case "rambo_repair":
                case "chat_thanos":
                case "app_reload_settings":
                case "monitoring_rescan":
                    return Task.CompletedTask;

                default:
                    return Task.CompletedTask;
            }
        }
    }
}
