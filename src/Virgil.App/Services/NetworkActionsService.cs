using System.Threading.Tasks;

namespace Virgil.App.Services
{
    /// <summary>
    /// Implémentation de base pour les actions réseau rapides.
    /// Les méthodes sont volontairement stubs pour l'instant afin de garder le build stable.
    /// </summary>
    public class NetworkActionsService : INetworkActionsService
    {
        public Task RunDiagnosticsAsync()
        {
            // TODO: implémentation réelle (scripts de diagnostic réseau, logs, etc.)
            return Task.CompletedTask;
        }

        public Task SoftResetAsync()
        {
            // TODO: reset léger (flush DNS, cache, etc.)
            return Task.CompletedTask;
        }

        public Task HardResetAsync()
        {
            // TODO: reset complet réseau (stack, adaptateurs, etc.)
            return Task.CompletedTask;
        }

        public Task RunLatencyTestAsync()
        {
            // TODO: test de latence (ping, mesure, rapport)
            return Task.CompletedTask;
        }
    }
}
