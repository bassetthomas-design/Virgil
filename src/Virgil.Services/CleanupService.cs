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
    public async Task RunSimpleAsync(CancellationToken ct = default)
    {
        try
        {
            var tempPath = Path.GetTempPath();

            if (!string.IsNullOrWhiteSpace(tempPath) && Directory.Exists(tempPath))
            {
                foreach (var file in Directory.EnumerateFiles(tempPath, "*", SearchOption.AllDirectories))
                {
                    if (ct.IsCancellationRequested)
                        return;

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

        await Task.CompletedTask;
    }

    public Task RunAdvancedAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task RunBrowserLightAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task RunBrowserDeepAsync(CancellationToken ct = default) => Task.CompletedTask;
}
