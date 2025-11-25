using System.Threading.Tasks;

namespace Virgil.App.Services
{
    /// <summary>
    /// Implémentation de base pour les actions liées aux performances.
    /// Les méthodes sont des stubs tant que la logique système détaillée n'est pas définie.
    /// </summary>
    public class PerformanceActionsService : IPerformanceActionsService
    {
        public Task EnablePerfModeAsync()
        {
            // TODO: appliquer un profil de performance (alimentation, services, etc.)
            return Task.CompletedTask;
        }

        public Task DisablePerfModeAsync()
        {
            // TODO: revenir à un profil normal / équilibré
            return Task.CompletedTask;
        }

        public Task AnalyzeStartupAsync()
        {
            // TODO: analyser les programmes au démarrage et produire un rapport
            return Task.CompletedTask;
        }

        public Task KillGamingSessionProcessesAsync()
        {
            // TODO: fermer les processus non essentiels pour le jeu
            return Task.CompletedTask;
        }
    }
}
