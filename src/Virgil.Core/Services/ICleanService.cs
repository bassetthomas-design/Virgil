using System.Threading;
using System.Threading.Tasks;

namespace Virgil.Core.Services
{
    /// <summary>
    /// Nettoyages : TEMP, navigateurs, caches système, WinSxS, etc.
    /// Interface simple pour être appelée depuis la VM/Agent.
    /// </summary>
    public interface ICleanService
    {
        /// <summary>Nettoyage des dossiers temporaires (user + Windows) et résidus simples.</summary>
        Task<ExecResult> CleanTempAsync(bool includePrefetch = true, CancellationToken ct = default);

        /// <summary>Nettoyage navigateurs (Chrome/Edge/Firefox/Brave/Opera/OperaGX/Vivaldi).</summary>
        /// <param name="clearCookies">True pour supprimer cookies (par défaut: false, on préserve les sessions).</param>
        Task<ExecResult> CleanBrowsersAsync(bool clearCookies = false, CancellationToken ct = default);

        /// <summary>Nettoyage composant Windows (DISM StartComponentCleanup).</summary>
        Task<ExecResult> CleanWinSxSAsync(CancellationToken ct = default);

        /// <summary>Reset caches Store/Icones/UWP (wsreset, icon cache).</summary>
        Task<ExecResult> CleanStoreAndShellCachesAsync(CancellationToken ct = default);

        /// <summary>Nettoyage “intelligent” : choisit simple/complet/pro selon l’état.</summary>
        /// <param name="depthHint">"auto" (par défaut), "simple", "complet", "pro".</param>
        Task<ExecResult> CleanAllAsync(string depthHint = "auto", CancellationToken ct = default);
    }
}
