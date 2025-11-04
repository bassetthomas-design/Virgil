using System.Threading;
using System.Threading.Tasks;

namespace Virgil.Core.Services
{
    /// <summary>
    /// Mises à jour : winget (apps/jeux), Windows Update, Defender, pilotes, Store.
    /// </summary>
    public interface IUpdateService
    {
        /// <summary>winget upgrade --all --include-unknown</summary>
        Task<ExecResult> UpdateApplicationsAsync(CancellationToken ct = default);

        /// <summary>Windows Update (scan/download/install).</summary>
        Task<ExecResult> UpdateWindowsAsync(bool includeDriversViaWU = true, CancellationToken ct = default);

        /// <summary>Microsoft Defender : MAJ signatures + scan rapide (optionnel).</summary>
        Task<ExecResult> UpdateDefenderAsync(bool quickScanAfterUpdate = true, CancellationToken ct = default);

        /// <summary>Pilotes : détection marque + MAJ (outil dédié/pnputil) + backup.</summary>
        Task<ExecResult> UpdateDriversAsync(bool backupBefore = true, CancellationToken ct = default);

        /// <summary>Microsoft Store : mise à jour des apps UWP.</summary>
        Task<ExecResult> UpdateStoreAppsAsync(CancellationToken ct = default);

        /// <summary>Tout mettre à jour (orchestration globale).</summary>
        Task<ExecResult> UpdateAllAsync(CancellationToken ct = default);
    }
}
