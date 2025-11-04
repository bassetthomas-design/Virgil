using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Virgil.Core.Services
{
    /// <summary>
    /// Service d'exécution process/CLI sécurisé (winget, dism, etc.).
    /// Centralise la capture des sorties, codes retour et timing.
    /// </summary>
    public interface IExecService
    {
        /// <summary>
        /// Exécute un binaire/commande avec arguments.
        /// </summary>
        /// <param name="fileName">Ex: "winget", "cmd.exe", "powershell.exe", "dism.exe".</param>
        /// <param name="arguments">Chaîne d’arguments (déjà correctement quotée).</param>
        /// <param name="runAsAdmin">True pour tenter une élévation (si l’hôte le permet).</param>
        /// <param name="workingDirectory">Dossier de travail (optionnel).</param>
        /// <param name="timeout">Timeout d’exécution (optionnel).</param>
        /// <param name="ct">Cancellation token.</param>
        Task<ExecResult> RunAsync(
            string fileName,
            string? arguments = null,
            bool runAsAdmin = false,
            string? workingDirectory = null,
            TimeSpan? timeout = null,
            CancellationToken ct = default
        );
    }
}
