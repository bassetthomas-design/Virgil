using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
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
        var targetPaths = BuildTargetPaths();
        if (targetPaths.Count == 0)
        {
            return ActionExecutionResult.NotAvailable("Aucun dossier temporaire détecté");
        }

        var totalFreed = 0L;
        var totalFiles = 0;
        var totalDirectories = 0;
        var details = new StringBuilder();

        try
        {
            foreach (var path in targetPaths)
            {
                var (freedBytes, filesDeleted, directoriesDeleted) = await CleanDirectoryAsync(path, ct).ConfigureAwait(false);
                totalFreed += freedBytes;
                totalFiles += filesDeleted;
                totalDirectories += directoriesDeleted;
                details.AppendLine($"[OK] {path} — {filesDeleted} fichiers, {directoriesDeleted} dossiers supprimés");
            }

            if (OperatingSystem.IsWindows())
            {
                var recycleDetails = EmptyRecycleBinSafely();
                details.AppendLine(recycleDetails);
            }
        }
        catch (OperationCanceledException)
        {
            return ActionExecutionResult.Failure("Nettoyage annulé");
        }
        catch
        {
            details.AppendLine("Certaines zones n'ont pas pu être nettoyées (ignoré : best effort)");
        }

        var freedMb = totalFreed / (1024.0 * 1024);
        var summary = $"Nettoyage rapide terminé – fichiers supprimés: {totalFiles}, dossiers: {totalDirectories}, espace libéré: {freedMb:F1} MB";
        return ActionExecutionResult.Ok(summary, details.ToString().TrimEnd());
    }

    public Task<ActionExecutionResult> RunAdvancedAsync(CancellationToken ct = default)
    {
        const string logMessage = "Executing ActionId=08 — Nettoyage disque avancé";
        Console.WriteLine(logMessage);
        System.Diagnostics.Trace.WriteLine(logMessage);

        if (OperatingSystem.IsWindows() && !IsRunningAsAdministrator())
        {
            const string adminMessage = "Nettoyage disque avancé indisponible : droits administrateur requis";
            return Task.FromResult(ActionExecutionResult.NotAvailable(adminMessage, "Exécution bloquée (admin requis)"));
        }

        var categories = BuildAdvancedCategories();
        var ignored = new List<string>();
        var cleanedSummaries = new List<string>();
        long totalFreed = 0;

        foreach (var category in categories)
        {
            ct.ThrowIfCancellationRequested();

            var categoryFreed = 0L;
            var categoryFiles = 0;
            var categoryDirectories = 0;

            var existingPaths = category.Paths.Where(Directory.Exists).ToList();
            if (existingPaths.Count == 0)
            {
                ignored.Add($"{category.Name} (aucun chemin trouvé)");
                continue;
            }

            foreach (var path in existingPaths)
            {
                var (freedBytes, filesDeleted, directoriesDeleted) = await CleanDirectoryAsync(path, ct).ConfigureAwait(false);
                categoryFreed += freedBytes;
                categoryFiles += filesDeleted;
                categoryDirectories += directoriesDeleted;
            }

            if (categoryFiles == 0 && categoryDirectories == 0)
            {
                ignored.Add($"{category.Name} (rien à supprimer)");
                continue;
            }

            totalFreed += categoryFreed;
            cleanedSummaries.Add($"{category.Name} — {categoryFiles} fichiers, {categoryDirectories} dossiers supprimés");
        }

        var freedMb = totalFreed / (1024.0 * 1024);
        if (cleanedSummaries.Count == 0)
        {
            var message = "Nettoyage disque avancé : Échec — aucune catégorie nettoyée";
            var details = ignored.Count == 0 ? null : string.Join("\n", ignored);
            return Task.FromResult(ActionExecutionResult.Failure(message, details));
        }

        var status = ignored.Count > 0 ? "Partiel" : "OK";
        var summary = $"Nettoyage disque avancé — Statut : {status}. Espace libéré : {freedMb:F1} MB.";

        var detailsBuilder = new StringBuilder();
        detailsBuilder.AppendLine("Catégories nettoyées :");
        foreach (var item in cleanedSummaries)
        {
            detailsBuilder.AppendLine($"- {item}");
        }

        if (ignored.Count > 0)
        {
            detailsBuilder.AppendLine("Éléments ignorés :");
            foreach (var skip in ignored)
            {
                detailsBuilder.AppendLine($"- {skip}");
            }
        }

        detailsBuilder.AppendLine("Suggestion : lancer un re-scan système pour valider l'état du disque.");

        return Task.FromResult(ActionExecutionResult.Ok(summary, detailsBuilder.ToString().TrimEnd()));
    }

    public Task<ActionExecutionResult> RunBrowserLightAsync(CancellationToken ct = default)
        => Task.FromResult(ActionExecutionResult.NotAvailable("Nettoyage navigateur léger non implémenté"));

    public Task<ActionExecutionResult> RunBrowserDeepAsync(CancellationToken ct = default)
        => Task.FromResult(ActionExecutionResult.NotAvailable("Nettoyage navigateur profond non implémenté"));

    private static List<string> BuildTargetPaths()
    {
        var paths = new List<string?>
        {
            Path.GetTempPath(),
            Environment.GetEnvironmentVariable("TEMP"),
            Environment.GetEnvironmentVariable("TMP"),
            SafeCombine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp"),
            SafeCombine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"),
            SafeCombine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrashDumps"),
            SafeCombine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "WER", "ReportArchive"),
            SafeCombine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Microsoft", "Windows", "WER", "ReportArchive"),
        };

        return paths
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => Path.GetFullPath(p!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(Directory.Exists)
            .ToList();
    }

    private static string SafeCombine(params string?[] parts)
    {
        if (parts.Any(p => string.IsNullOrWhiteSpace(p)))
            return string.Empty;

        return Path.Combine(parts!.Select(p => p!).ToArray());
    }

    private static async Task<(long FreedBytes, int FilesDeleted, int DirectoriesDeleted)> CleanDirectoryAsync(string root, CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            long freedBytes = 0;
            int filesDeleted = 0;
            int directoriesDeleted = 0;

            try
            {
                foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        var info = new FileInfo(file);
                        freedBytes += info.Exists ? info.Length : 0;
                        File.SetAttributes(file, FileAttributes.Normal);
                        File.Delete(file);
                        filesDeleted++;
                    }
                    catch
                    {
                        // Ignoré : certains fichiers peuvent être verrouillés.
                    }
                }

                foreach (var dir in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories).OrderByDescending(p => p.Length))
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        if (!Directory.EnumerateFileSystemEntries(dir).Any())
                        {
                            Directory.Delete(dir);
                            directoriesDeleted++;
                        }
                    }
                    catch
                    {
                        // Ignoré : best effort.
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Best effort : ne pas interrompre le nettoyage global.
            }

            return (freedBytes, filesDeleted, directoriesDeleted);
        }, ct).ConfigureAwait(false);
    }

    private static string EmptyRecycleBinSafely()
    {
        if (!OperatingSystem.IsWindows())
            return "Corbeille : ignorée (plateforme non Windows)";

        try
        {
            const RecycleFlags flags = RecycleFlags.SHERB_NOCONFIRMATION | RecycleFlags.SHERB_NOPROGRESSUI | RecycleFlags.SHERB_NOSOUND;
            int result = SHEmptyRecycleBin(IntPtr.Zero, null, flags);
            return result == 0 ? "Corbeille vidée." : "Corbeille : impossible de vider (ignoré).";
        }
        catch
        {
            return "Corbeille : impossible de vider (ignoré).";
        }
    }

    [Flags]
    private enum RecycleFlags : int
    {
        SHERB_NOCONFIRMATION = 0x00000001,
        SHERB_NOPROGRESSUI = 0x00000002,
        SHERB_NOSOUND = 0x00000004
    }

    [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, RecycleFlags dwFlags);

    private sealed record AdvancedCategory(string Name, IReadOnlyList<string> Paths);

    private static List<AdvancedCategory> BuildAdvancedCategories()
    {
        var windowsFolder = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        return new List<AdvancedCategory>
        {
            new("Cache Windows Update", new List<string>
            {
                SafeCombine(windowsFolder, "SoftwareDistribution", "Download"),
                SafeCombine(windowsFolder, "SoftwareDistribution", "DataStore", "Logs"),
            }.Where(p => !string.IsNullOrWhiteSpace(p)).ToList()),
            new("Logs système", new List<string>
            {
                SafeCombine(windowsFolder, "Logs"),
                SafeCombine(windowsFolder, "System32", "LogFiles"),
                SafeCombine(programData, "Microsoft", "Windows", "WER", "ReportArchive"),
            }.Where(p => !string.IsNullOrWhiteSpace(p)).ToList()),
            new("Fichiers temporaires profonds", new List<string>
            {
                SafeCombine(windowsFolder, "Temp"),
                SafeCombine(programData, "Microsoft", "Windows", "Caches"),
                Path.GetTempPath(),
            }.Where(p => !string.IsNullOrWhiteSpace(p)).ToList())
        };
    }

    private static bool IsRunningAsAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }
}
