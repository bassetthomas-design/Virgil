using System.Threading.Tasks;

namespace Virgil.Core.Services
{
    public static class BrowserCleaningServiceExtensions
    {
        /// <summary>
        /// Extension minimale pour compat `MainWindow` :
        /// renvoie un petit rapport textuel (à remplacer par la vraie analyse/clean plus tard).
        /// </summary>
        public static Task<string> AnalyzeAndCleanAsync(this BrowserCleaningService _)
        {
            // TODO: brancher la vraie détection profils + purge des caches par navigateur.
            return Task.FromResult("[Browsers] Analyse et nettoyage : aucun cache critique détecté (stub).");
        }
    }
}
