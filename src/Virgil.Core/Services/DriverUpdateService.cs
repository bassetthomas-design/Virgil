using System.Threading.Tasks;

namespace Virgil.Core.Services
{
    /// <summary>
    /// Service "best-effort" pour les mises à jour de pilotes.
    /// Aujourd'hui il délègue à winget via ApplicationUpdateService.
    /// (NVIDIA/AMD/Intel/Realtek etc. passent souvent par des packages winget.)
    /// </summary>
    public sealed class DriverUpdateService
    {
        /// <summary>
        /// Lance les mises à jour de tout ce que winget peut voir (incluant pilotes
        /// publiés comme packages). Renvoie la sortie console agrégée.
        /// </summary>
        public async Task<string> UpgradeDriversAsync()
        {
            var app = new ApplicationUpdateService();
            // On laisse winget décider ; includeUnknown pour couvrir plus de cas.
            return await app.UpgradeAllAsync(includeUnknown: true, silent: true)
                            .ConfigureAwait(false);
        }
    }
}
