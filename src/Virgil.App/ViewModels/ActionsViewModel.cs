using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Virgil.App.Services;
using Virgil.Services.Narration;

namespace Virgil.App.ViewModels
{
    /// <summary>
    /// ViewModel pour le panneau d'actions rapides.
    /// Reçoit un identifiant d'action (Tag / CommandParameter) et délègue au backend.
    /// </summary>
    public class ActionsViewModel : BaseViewModel
    {
        private readonly SystemActionsService _systemActions;
        private readonly VirgilNarrationService _virgil;
        private readonly INetworkActionsService _network;
        private readonly IPerformanceActionsService _performance;
        private readonly ISpecialActionsService _special;

        /// <summary>
        /// Commande appelée par les boutons d'ActionsPanel.xaml, avec l'identifiant d'action en paramètre.
        /// </summary>
        public ICommand InvokeActionCommand { get; }

        public ActionsViewModel(
            SystemActionsService systemActions,
            VirgilNarrationService virgil,
            INetworkActionsService network,
            IPerformanceActionsService performance,
            ISpecialActionsService special)
        {
            _systemActions = systemActions ?? throw new ArgumentNullException(nameof(systemActions));
            _virgil = virgil ?? throw new ArgumentNullException(nameof(virgil));
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _performance = performance ?? throw new ArgumentNullException(nameof(performance));
            _special = special ?? throw new ArgumentNullException(nameof(special));

            InvokeActionCommand = new RelayCommand(param =>
            {
                var actionId = param as string;
                if (!string.IsNullOrWhiteSpace(actionId))
                {
                    InvokeAction(actionId);
                }
            });
        }

        /// <summary>
        /// Point d'entrée synchrone pour déclencher une action rapide.
        /// </summary>
        public void InvokeAction(string actionId)
        {
            if (string.IsNullOrWhiteSpace(actionId))
                return;

            _ = InvokeActionInternalAsync(actionId);
        }

        private async Task InvokeActionInternalAsync(string actionId)
        {
            var success = false;

            try
            {
                await _virgil.OnActionStartedAsync(actionId, CancellationToken.None);
                await ExecuteActionCoreAsync(actionId);
                success = true;
            }
            catch
            {
                success = false;
            }
            finally
            {
                await _virgil.OnActionCompletedAsync(actionId, success, CancellationToken.None);
            }
        }

        /// <summary>
        /// Mapping des identifiants d'action (Tags UI) vers les appels backend.
        /// </summary>
        private Task ExecuteActionCoreAsync(string actionId)
        {
            return actionId switch
            {
                // Maintenance et nettoyage principal
                "maintenance_full"    => _systemActions.RunMaintenanceAsync(),
                "smart_cleanup"       => _systemActions.RunSmartCleanupAsync(),
                "quick_clean"         => _systemActions.RunSmartCleanupAsync(),

                // Navigateurs
                "browsers_clean"      => _systemActions.RunCleanBrowsersAsync(),
                "browser_soft_clean"  => _systemActions.RunCleanBrowsersAsync(),
                "browser_deep_clean"  => _systemActions.RunCleanBrowsersAsync(),

                // Mises à jour
                "updates_all"         => _systemActions.RunUpdateAllAsync(),
                "apps_update_all"     => _systemActions.RunUpdateAllAsync(),
                "windows_update"      => _systemActions.RunUpdateAllAsync(),
                "gpu_driver_check"    => _systemActions.RunUpdateAllAsync(),

                // Sécurité
                "defender_scan"       => _systemActions.RunDefenderAsync(),

                // Configuration
                "open_config"         => _systemActions.OpenConfigAsync(),

                // Réseau
                "network_diag"         => _network.RunDiagnosticsAsync(),
                "network_soft_reset"   => _network.SoftResetAsync(),
                "network_hard_reset"   => _network.HardResetAsync(),
                "network_latency_test" => _network.RunLatencyTestAsync(),

                // Performance
                "perf_mode_on"        => _performance.EnablePerfModeAsync(),
                "perf_mode_off"       => _performance.DisablePerfModeAsync(),
                "startup_analyze"     => _performance.AnalyzeStartupAsync(),
                "gaming_kill_session"  => _performance.KillGamingSessionProcessesAsync(),

                // Spéciaux
                "rambo_repair"        => _special.RamboRepairAsync(),
                "chat_thanos"         => _special.PurgeChatHistoryAsync(),
                "app_reload_settings" => _special.ReloadSettingsAsync(),
                "monitoring_rescan"   => _special.RescanMonitoringAsync(),

                // Par défaut : non implémenté
                _                         => Task.CompletedTask
            };
        }
    }
}
