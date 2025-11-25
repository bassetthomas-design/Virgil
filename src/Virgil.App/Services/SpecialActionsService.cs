using System.Threading.Tasks;

namespace Virgil.App.Services
{
    /// <summary>
    /// Actions spéciales / utilitaires à haut niveau de dégâts potentiels.
    /// Les implémentations sont volontairement neutres pour l'instant.
    /// </summary>
    public class SpecialActionsService : ISpecialActionsService
    {
        public Task RamboRepairAsync()
        {
            // TODO: réparer certaines parties de l'environnement (Explorer, caches, etc.)
            return Task.CompletedTask;
        }

        public Task PurgeChatHistoryAsync()
        {
            // TODO: purger l'historique de chat persistant si applicable
            return Task.CompletedTask;
        }

        public Task ReloadSettingsAsync()
        {
            // TODO: recharger la configuration de l'application depuis le stockage
            return Task.CompletedTask;
        }

        public Task RescanMonitoringAsync()
        {
            // TODO: déclencher un rescan complet via le service de monitoring
            return Task.CompletedTask;
        }
    }
}
