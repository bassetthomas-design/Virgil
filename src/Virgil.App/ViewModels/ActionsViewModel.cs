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

        /// <summary>
        /// Commande appelée par les boutons d'ActionsPanel.xaml, avec l'identifiant d'action en paramètre.
        /// </summary>
        public ICommand InvokeActionCommand { get; }

        public ActionsViewModel(SystemActionsService systemActions, VirgilNarrationService virgil)
        {
            _systemActions = systemActions ?? throw new ArgumentNullException(nameof(systemActions));
            _virgil = virgil ?? throw new ArgumentNullException(nameof(virgil));

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
                // Les erreurs sont remontées via la narration / logs internes.
                success = false;
            }
            finally
            {
                await _virgil.OnActionCompletedAsync(actionId, success, CancellationToken.None);
            }
        }

        /// <summary>
        /// Mapping des identifiants d'action (Tags UI) vers les appels backend.
        /// Pour l'instant, on route uniquement vers les actions système principales.
        /// Les autres identifiants tombent en no-op (Task.CompletedTask).
        /// </summary>
        private Task ExecuteActionCoreAsync(string actionId)
        {
            return actionId switch
            {
                // Maintenance et nettoyage principal
                "maintenance_full"    => _systemActions.RunMaintenanceAsync(),
                "smart_cleanup"       => _systemActions.RunSmartCleanupAsync(),
                "quick_clean"         => _systemActions.RunSmartCleanupAsync(),

                // Navigateurs : tant que le backend ne distingue pas soft/deep, on route sur le même
                "browsers_clean"      => _systemActions.RunCleanBrowsersAsync(),
                "browser_soft_clean"  => _systemActions.RunCleanBrowsersAsync(),
                "browser_deep_clean"  => _systemActions.RunCleanBrowsersAsync(),

                // Mises à jour : on utilise la mise à jour globale en attendant des actions plus fines
                "updates_all"         => _systemActions.RunUpdateAllAsync(),
                "apps_update_all"     => _systemActions.RunUpdateAllAsync(),
                "windows_update"      => _systemActions.RunUpdateAllAsync(),
                "gpu_driver_check"    => _systemActions.RunUpdateAllAsync(),

                // Sécurité
                "defender_scan"       => _systemActions.RunDefenderAsync(),

                // Configuration
                "open_config"         => _systemActions.OpenConfigAsync(),

                // Par défaut : pas encore implémenté côté backend, on ne fait rien
                _                         => Task.CompletedTask
            };
        }
    }
}
