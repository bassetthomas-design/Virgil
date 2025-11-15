using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Virgil.Services.Abstractions;

namespace Virgil.Services;

/// <summary>
/// Implémentation de base du service de nettoyage.
/// Pour l’instant :
///  - Nettoyage rapide : corbeille utilisateur + dossier TEMP utilisateur.
///  - Les autres méthodes restent des stubs à spécialiser plus tard.
/// </summary>
public sealed class CleanupService : ICleanupService
{
    public async Task RunSimpleAsync(CancellationToken ct = default)
    {
        // Nettoyage rapide = opérations basiques et relativement sûres.
        // On travaille en best-effort : si quelque chose échoue, on continue.

        try
        {
            // 1) Corbeille utilisateur via Shell (simple approche : utiliser PowerShell).
            //    Ce code suppose que PowerShell est disponible sur la machine.
            //    Si l’appel échoue, on ignore l’erreur.
            await RunProcessSafeAsync("powershell.exe",
                "-NoLogo -NoProfile -Command "Clear-RecycleBin -Force -ErrorAction SilentlyContinue"", ct);
        }
        catch
        {
            // Ignorer : on ne veut pas faire échouer tout le nettoyage pour ça.
        }

        try
        {
            // 2) Dossier temporaire utilisateur (Path.GetTempPath).
            var tempPath = Path.GetTempPath();
            if (!string.IsNullOrWhiteSpace(tempPath) && Directory.Exists(tempPath))
            {
                foreach (var file in Directory.EnumerateFiles(tempPath, "*", SearchOption.AllDirectories))
                {
                    if (ct.IsCancellationRequested) return;
                    try
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                        File.Delete(file);
                    }
                    catch
                    {
                        // Best-effort : certains fichiers peuvent être verrouillés.
                    }
                }
            }
        }
        catch
        {
            // Encore une fois : best-effort.
        }
    }

    public Task RunAdvancedAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task RunBrowserLightAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task RunBrowserDeepAsync(CancellationToken ct = default) => Task.CompletedTask;

    private static async Task RunProcessSafeAsync(string fileName, string arguments, CancellationToken ct)
    {
        try
        {
            using var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            process.Start();

            // On ne lit pas forcément la sortie, mais on s'assure que le process se termine.
            await Task.Run(() => process.WaitForExit(), ct);
        }
        catch
        {
            // Ignorer toute erreur de process dans ce contexte.
        }
    }
}
