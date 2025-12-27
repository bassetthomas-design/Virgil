using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Virgil.Services.Abstractions;

namespace Virgil.Services;

/// <summary>
/// Service de nettoyage de base.
/// Pour l’instant, seule l’action de nettoyage rapide (RunSimpleAsync)
/// effectue un nettoyage best-effort du dossier TEMP utilisateur.
/// Les autres méthodes sont des stubs à spécialiser plus tard.
/// </summary>
public sealed class CleanupService : ICleanupService
{
    public async Task<ActionExecutionResult> RunSimpleAsync(CancellationToken ct = default)
    {
        try
        {
            var tempPath = Path.GetTempPath();

            if (!string.IsNullOrWhiteSpace(tempPath) && Directory.Exists(tempPath))
            {
                foreach (var file in Directory.EnumerateFiles(tempPath, "*", SearchOption.AllDirectories))
                {
                    if (ct.IsCancellationRequested)
                        return ActionExecutionResult.Failure("Nettoyage annulé");

                    try
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                        File.Delete(file);
                    }
                    catch
                    {
                        // Best-effort : certains fichiers peuvent être verrouillés ou protégés.
                    }
                }
            }
        }
        catch
        {
            // On ignore les erreurs globales : objectif = ne jamais faire échouer l’action.
        }

        return ActionExecutionResult.Ok("Nettoyage rapide effectué (best effort)");
    }

    public Task<ActionExecutionResult> RunAdvancedAsync(CancellationToken ct = default)
        => Task.FromResult(ActionExecutionResult.NotAvailable("Nettoyage disque avancé non implémenté"));

    public Task<ActionExecutionResult> RunBrowserLightAsync(CancellationToken ct = default)
        => Task.FromResult(ActionExecutionResult.NotAvailable("Nettoyage navigateur léger non implémenté"));

    public Task<ActionExecutionResult> RunBrowserDeepAsync(CancellationToken ct = default)
        => Task.FromResult(ActionExecutionResult.NotAvailable("Nettoyage navigateur profond non implémenté"));
}
