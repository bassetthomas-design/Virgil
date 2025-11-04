using System.Threading;
using System.Threading.Tasks;

namespace Virgil.Core.Services
{
    /// <summary>
    /// Santé & réparation : SFC/DISM, SMART, services essentiels, etc.
    /// </summary>
    public interface IHealthService
    {
        /// <summary>Exécute SFC /scannow et retourne le rapport.</summary>
        Task<ExecResult> RunSfcAsync(CancellationToken ct = default);

        /// <summary>Exécute DISM /Online /Cleanup-Image /RestoreHealth.</summary>
        Task<ExecResult> RunDismRestoreHealthAsync(CancellationToken ct = default);

        /// <summary>SMART/Disque : collecte des infos rapides (résumé texte).</summary>
        Task<ExecResult> CheckSmartAsync(CancellationToken ct = default);

        /// <summary>Vérification rapide globale : SFC (verification), DISM (checkhealth), services clés.</summary>
        Task<ExecResult> QuickHealthCheckAsync(CancellationToken ct = default);
    }
}
