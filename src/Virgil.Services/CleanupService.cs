using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    private readonly Func<CleanupPlan> _planFactory;
    private readonly Func<BrowserCleanPlan> _browserPlanFactory;
    private readonly Func<IReadOnlyCollection<AdvancedStep>> _advancedPlanFactory;
    private readonly Func<bool> _isWindows;
    private readonly Func<bool> _isAdministrator;
    private readonly Func<string, string, CancellationToken, Task<CommandResult>> _commandRunner;

    public CleanupService()
        : this(CleanupPlan.FromEnvironment, BrowserCleanPlan.FromEnvironment)
    {
    }

    public CleanupService(Func<CleanupPlan> planFactory)
        : this(planFactory, BrowserCleanPlan.FromEnvironment)
    {
    }

    public CleanupService(
        Func<CleanupPlan> planFactory,
        Func<BrowserCleanPlan> browserPlanFactory,
        Func<IReadOnlyCollection<AdvancedStep>>? advancedPlanFactory = null,
        Func<bool>? isWindows = null,
        Func<bool>? isAdministrator = null,
        Func<string, string, CancellationToken, Task<CommandResult>>? commandRunner = null)
    {
        _planFactory = planFactory ?? throw new ArgumentNullException(nameof(planFactory));
        _browserPlanFactory = browserPlanFactory ?? throw new ArgumentNullException(nameof(browserPlanFactory));
        _advancedPlanFactory = advancedPlanFactory ?? BuildAdvancedCategories;
        _isWindows = isWindows ?? OperatingSystem.IsWindows;
        _isAdministrator = isAdministrator ?? IsRunningAsAdministrator;
        _commandRunner = commandRunner ?? RunCommandAsync;
    }

    public async Task<ActionExecutionResult> RunSimpleAsync(CancellationToken ct = default)
    {
        var plan = _planFactory();
        if (plan.TempLocations.Count == 0 && plan.CacheLocations.Count == 0 && plan.LogLocations.Count == 0)
        {
            return ActionExecutionResult.NotAvailable("Aucune zone de nettoyage détectée");
        }

        var stats = new CleanupStats();

        try
        {
            foreach (var path in plan.TempLocations)
            {
                stats = stats.Add(await CleanDirectoryAsync(path, ct, plan.ShouldExclude).ConfigureAwait(false));
            }

            foreach (var path in plan.CacheLocations)
            {
                stats = stats.Add(await CleanDirectoryAsync(path, ct, plan.ShouldExclude).ConfigureAwait(false));
            }

            foreach (var path in plan.LogLocations)
            {
                stats = stats.Add(await CleanDirectoryAsync(
                    path,
                    ct,
                    plan.ShouldExclude,
                    fileFilter: fi => fi.Extension.Equals(".log", StringComparison.OrdinalIgnoreCase)
                        && fi.LastWriteTimeUtc < DateTime.UtcNow - plan.LogRetention
                ).ConfigureAwait(false));
            }

            if (plan.EmptyRecycleBin)
            {
                _ = EmptyRecycleBinSafely();
            }
        }
        catch (OperationCanceledException)
        {
            return ActionExecutionResult.Failure("Nettoyage annulé");
        }

        var freedText = FormatSize(stats.FreedBytes);
        var summary = $"Quantité libérée : {freedText} — Nombre de fichiers supprimés : {stats.FilesDeleted}. Ce n'était pas spectaculaire, mais ton disque respire mieux.";
        return ActionExecutionResult.Ok(summary);
    }

    public async Task<ActionExecutionResult> RunAdvancedAsync(CancellationToken ct = default)
    {
        const string logMessage = "Executing ActionId=08 — Nettoyage disque avancé";
        Console.WriteLine(logMessage);
        System.Diagnostics.Trace.WriteLine(logMessage);

        if (_isWindows() && !_isAdministrator())
        {
            const string adminMessage = "Nettoyage disque avancé indisponible : droits administrateur requis";
            const string details = "Action avancée, peut prendre du temps — mais sans admin, c’est niet (sécurité + accès système).";
            return ActionExecutionResult.NotAvailable(adminMessage, details);
        }

        var categories = _advancedPlanFactory();
        var ignored = new List<string>();
        var cleanedSummaries = new List<string>();
        var lockedSummaries = new List<string>();
        long totalFreed = 0;

        foreach (var category in categories)
        {
            ct.ThrowIfCancellationRequested();

            if (category.WindowsOnly && !_isWindows())
            {
                ignored.Add($"{category.Name} (ignoré : plateforme non Windows)");
                continue;
            }

            var categoryFreed = 0L;
            var categoryFiles = 0;
            var categoryDirectories = 0;
            var categoryLocked = 0;
            var categoryExecuted = false;

            if (category.CustomCleanup is not null)
            {
                var stats = await category.CustomCleanup(ct).ConfigureAwait(false);
                categoryExecuted = true;
                categoryFreed += stats.FreedBytes;
                categoryFiles += stats.FilesDeleted;
                categoryDirectories += stats.DirectoriesDeleted;
                categoryLocked += stats.LockedItems;

                if (category.CountExecutionAsCleanup && !stats.HasActivity)
                {
                    categoryFiles += 1; // Marqueur : l’étape a été exécutée même sans suppressions mesurables.
                }
            }
            else
            {
                var existingPaths = category.Paths.Where(Directory.Exists).ToList();
                if (existingPaths.Count == 0)
                {
                    ignored.Add($"{category.Name} (aucun chemin trouvé)");
                    continue;
                }

                foreach (var path in existingPaths)
                {
                    var result = await CleanDirectoryAsync(path, ct, fileFilter: category.FileFilter).ConfigureAwait(false);
                    categoryExecuted |= result.HasActivity;
                    categoryFreed += result.FreedBytes;
                    categoryFiles += result.FilesDeleted;
                    categoryDirectories += result.DirectoriesDeleted;
                    categoryLocked += result.LockedItems;
                }
            }

            if (!categoryExecuted && categoryFiles == 0 && categoryDirectories == 0)
            {
                if (categoryLocked > 0)
                {
                    lockedSummaries.Add($"{category.Name}: {categoryLocked} élément(s) verrouillé(s) ignoré(s)");
                    ignored.Add($"{category.Name} (fichiers verrouillés)");
                    continue;
                }

                ignored.Add($"{category.Name} (rien à supprimer)");
                continue;
            }

            if (categoryLocked > 0)
            {
                lockedSummaries.Add($"{category.Name}: {categoryLocked} élément(s) verrouillé(s) ignoré(s)");
            }

            totalFreed += categoryFreed;
            cleanedSummaries.Add($"{category.Name} — {categoryFiles} fichiers, {categoryDirectories} dossiers supprimés");
        }

        var freedText = FormatSize(totalFreed);
        if (cleanedSummaries.Count == 0)
        {
            var message = "Nettoyage disque avancé : Échec — aucune catégorie nettoyée";
            var details = ignored.Count == 0 ? null : string.Join("\n", ignored);
            return ActionExecutionResult.Failure(message, details);
        }

        var status = ignored.Count > 0 ? "Partiel" : "OK";
        var summary = $"Nettoyage disque avancé — Statut : {status}. Espace libéré : {freedText}. Action avancée, peut prendre du temps (et réclame l’admin).";

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

        if (lockedSummaries.Count > 0)
        {
            detailsBuilder.AppendLine("Éléments verrouillés (ignorés) :");
            foreach (var locked in lockedSummaries)
            {
                detailsBuilder.AppendLine($"- {locked}");
            }
        }

        detailsBuilder.AppendLine("Suggestion : lancer un re-scan du système (commande: monitoring_rescan).");

        return ActionExecutionResult.Ok(summary, detailsBuilder.ToString().TrimEnd());
    }

    public async Task<ActionExecutionResult> RunBrowserLightAsync(CancellationToken ct = default)
    {
        var plan = _browserPlanFactory();
        if (plan.Targets.Count == 0)
        {
            return ActionExecutionResult.NotAvailable("Aucun navigateur détecté (rien à nettoyer)");
        }

        var cleaned = new List<string>();
        var ignored = new List<string>();
        var totalStats = new CleanupStats();

        foreach (var target in plan.Targets)
        {
            ct.ThrowIfCancellationRequested();

            var hadLock = false;
            var existingPaths = target.Paths.Where(Directory.Exists).ToList();
            if (existingPaths.Count == 0)
            {
                continue;
            }

            foreach (var path in existingPaths)
            {
                var result = await CleanBrowserCacheAsync(path, ct).ConfigureAwait(false);
                hadLock |= result.HadLockedFiles;
                totalStats = totalStats.Add(result.Stats);
            }

            if (hadLock)
            {
                ignored.Add(target.BrowserName);
            }
            else
            {
                cleaned.Add(target.BrowserName);
            }
        }

        if (cleaned.Count == 0 && ignored.Count == 0)
        {
            return ActionExecutionResult.NotAvailable("Aucune donnée de navigation à nettoyer");
        }

        var freedText = FormatSize(totalStats.FreedBytes);
        var treated = cleaned.Count == 0 ? "aucun" : string.Join(", ", cleaned);
        var skipped = ignored.Count == 0 ? "aucun" : string.Join(", ", ignored);

        var summary = $"Navigateurs traités: {treated}. Navigateurs ignorés (ouverts/verrouillés): {skipped}. Quantité libérée: {freedText}. Fichiers supprimés: {totalStats.FilesDeleted}. Les miettes ont disparu. Les onglets n’ont rien remarqué.";
        return ActionExecutionResult.Ok(summary);
    }

    public Task<ActionExecutionResult> RunBrowserDeepAsync(CancellationToken ct = default)
        => Task.FromResult(ActionExecutionResult.NotAvailable("Nettoyage navigateur profond non implémenté"));

    private static async Task<CleanupStats> CleanDirectoryAsync(
        string root,
        CancellationToken ct,
        Func<string, bool>? shouldExclude = null,
        Func<FileInfo, bool>? fileFilter = null)
    {
        return await Task.Run(() =>
        {
            var stats = new CleanupStats();
            var locked = 0;

            try
            {
                if (!Directory.Exists(root))
                {
                    return stats;
                }

                foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                {
                    ct.ThrowIfCancellationRequested();
                    if (shouldExclude?.Invoke(file) == true)
                    {
                        continue;
                    }

                    try
                    {
                        var info = new FileInfo(file);
                        if (fileFilter is not null && !fileFilter(info))
                        {
                            continue;
                        }

                        stats = stats.Add(info.Exists ? info.Length : 0, 1, 0);
                        File.SetAttributes(file, FileAttributes.Normal);
                        File.Delete(file);
                    }
                    catch (IOException)
                    {
                        locked++;
                    }
                    catch
                    {
                        locked++;
                    }
                }

                foreach (var dir in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories).OrderByDescending(p => p.Length))
                {
                    ct.ThrowIfCancellationRequested();
                    if (shouldExclude?.Invoke(dir) == true)
                    {
                        continue;
                    }

                    try
                    {
                        if (!Directory.EnumerateFileSystemEntries(dir).Any())
                        {
                            Directory.Delete(dir);
                            stats = stats.Add(0, 0, 1);
                        }
                    }
                    catch (IOException)
                    {
                        locked++;
                    }
                    catch
                    {
                        locked++;
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

            return stats.Add(0, 0, 0, locked);
        }, ct).ConfigureAwait(false);
    }

    private static string SafeCombine(params string?[] parts)
    {
        if (parts.Any(p => string.IsNullOrWhiteSpace(p)))
            return string.Empty;

        return Path.Combine(parts!.Select(p => p!).ToArray());
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

    private static string FormatSize(long bytes)
    {
        var megaBytes = bytes / (1024d * 1024d);
        if (megaBytes >= 1024d)
        {
            return $"{megaBytes / 1024d:F2} Go";
        }

        return $"{megaBytes:F1} Mo";
    }

    public sealed record CleanupPlan(
        IReadOnlyCollection<string> TempLocations,
        IReadOnlyCollection<string> CacheLocations,
        IReadOnlyCollection<string> LogLocations,
        IReadOnlyCollection<string> ExcludedSegments,
        TimeSpan LogRetention,
        bool EmptyRecycleBin)
    {
        internal static CleanupPlan FromEnvironment()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var windowsFolder = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var commonAppData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

            var tempLocations = NormalizePaths(new[]
            {
                Path.GetTempPath(),
                Environment.GetEnvironmentVariable("TEMP"),
                Environment.GetEnvironmentVariable("TMP"),
                SafeCombine(localAppData, "Temp"),
                SafeCombine(windowsFolder, "Temp"),
            });

            var cacheLocations = NormalizePaths(new[]
            {
                SafeCombine(localAppData, "Microsoft", "Windows", "Caches"),
                SafeCombine(localAppData, "Microsoft", "Windows", "WER", "ReportArchive"),
                SafeCombine(commonAppData, "Microsoft", "Windows", "WER", "ReportArchive"),
                SafeCombine(localAppData, "CrashDumps"),
            });

            var logLocations = NormalizePaths(new[]
            {
                SafeCombine(localAppData, "Logs"),
                SafeCombine(appData, "Logs"),
            });

            var excludedSegments = new[]
            {
                "Chrome",
                "Edge",
                "Firefox",
                "Brave",
                "Opera",
                "Opera GX",
                "Vivaldi",
                "Safari"
            };

            return new CleanupPlan(tempLocations, cacheLocations, logLocations, excludedSegments, TimeSpan.FromDays(7), true);
        }

        internal bool ShouldExclude(string path)
        {
            var segments = path.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
            return segments.Any(segment => ExcludedSegments.Any(ex => segment.Contains(ex, StringComparison.OrdinalIgnoreCase)));
        }

        private static IReadOnlyCollection<string> NormalizePaths(IEnumerable<string?> paths)
            => paths
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => Path.GetFullPath(p!))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(Directory.Exists)
                .ToList();
    }

    public sealed record CleanupStats(long FreedBytes = 0, int FilesDeleted = 0, int DirectoriesDeleted = 0, int LockedItems = 0)
    {
        public CleanupStats Add(CleanupStats other)
            => new(FreedBytes + other.FreedBytes, FilesDeleted + other.FilesDeleted, DirectoriesDeleted + other.DirectoriesDeleted, LockedItems + other.LockedItems);

        public CleanupStats Add(long freedBytes, int filesDeleted, int directoriesDeleted, int lockedItems = 0)
            => new(FreedBytes + freedBytes, FilesDeleted + filesDeleted, DirectoriesDeleted + directoriesDeleted, LockedItems + lockedItems);

        public bool HasActivity => FreedBytes > 0 || FilesDeleted > 0 || DirectoriesDeleted > 0;
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

    public sealed record AdvancedStep(
        string Name,
        IReadOnlyList<string> Paths,
        Func<FileInfo, bool>? FileFilter = null,
        bool WindowsOnly = true,
        Func<CancellationToken, Task<CleanupStats>>? CustomCleanup = null,
        bool CountExecutionAsCleanup = false);

    private static async Task<BrowserCleanupResult> CleanBrowserCacheAsync(string root, CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            var stats = new CleanupStats();
            var hadLockedFiles = false;

            if (!Directory.Exists(root))
            {
                return new BrowserCleanupResult(stats, hadLockedFiles);
            }

            try
            {
                foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        var info = new FileInfo(file);
                        var size = info.Exists ? info.Length : 0;
                        File.SetAttributes(file, FileAttributes.Normal);
                        File.Delete(file);
                        stats = stats.Add(size, 1, 0);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch
                    {
                        hadLockedFiles = true;
                        stats = stats.Add(0, 0, 0, 1);
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
                                stats = stats.Add(0, 0, 1);
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch
                        {
                            hadLockedFiles = true;
                            stats = stats.Add(0, 0, 0, 1);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                throw;
            }
            catch
            {
                // Best effort: ignore unexpected IO issues.
            }

            return new BrowserCleanupResult(stats, hadLockedFiles);
        }, ct).ConfigureAwait(false);
    }

    public sealed record BrowserCleanPlan(IReadOnlyCollection<BrowserTarget> Targets)
    {
        internal static BrowserCleanPlan FromEnvironment()
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            var targets = new List<BrowserTarget>();

            void AddChromium(string name, params string?[] profileParts)
            {
                var profile = SafeCombine(profileParts);
                if (string.IsNullOrWhiteSpace(profile) || !Directory.Exists(profile))
                {
                    return;
                }

                var paths = new[]
                {
                    SafeCombine(profile, "Cache"),
                    SafeCombine(profile, "Code Cache"),
                    SafeCombine(profile, "GPUCache"),
                    SafeCombine(profile, "ShaderCache")
                }.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();

                if (paths.Any(Directory.Exists))
                {
                    targets.Add(new BrowserTarget(name, paths));
                }
            }

            AddChromium("Chrome", local, "Google", "Chrome", "User Data", "Default");
            AddChromium("Edge", local, "Microsoft", "Edge", "User Data", "Default");
            AddChromium("Brave", local, "BraveSoftware", "Brave-Browser", "User Data", "Default");
            AddChromium("Opera", local, "Opera Software", "Opera Stable");
            AddChromium("Opera GX", local, "Opera Software", "Opera GX Stable");
            AddChromium("Vivaldi", local, "Vivaldi", "User Data", "Default");

            var firefoxProfiles = SafeCombine(roaming, "Mozilla", "Firefox", "Profiles");
            if (!string.IsNullOrWhiteSpace(firefoxProfiles) && Directory.Exists(firefoxProfiles))
            {
                foreach (var profileDir in Directory.EnumerateDirectories(firefoxProfiles))
                {
                    var cache = SafeCombine(profileDir, "cache2");
                    var shader = SafeCombine(profileDir, "shader-cache");
                    var existing = new List<string>();
                    if (Directory.Exists(cache)) existing.Add(cache);
                    if (Directory.Exists(shader)) existing.Add(shader);
                    if (existing.Count > 0)
                    {
                        targets.Add(new BrowserTarget($"Firefox ({Path.GetFileName(profileDir)})", existing));
                    }
                }
            }

            return new BrowserCleanPlan(targets);
        }
    }

    public sealed record BrowserTarget(string BrowserName, IReadOnlyCollection<string> Paths);

    private sealed record BrowserCleanupResult(CleanupStats Stats, bool HadLockedFiles);

    private IReadOnlyCollection<AdvancedStep> BuildAdvancedCategories()
    {
        var windowsFolder = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var tempPath = Path.GetTempPath();

        var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);

        return new List<AdvancedStep>
        {
            new("Fichiers temporaires système profonds", new List<string>
            {
                SafeCombine(windowsFolder, "Temp"),
                SafeCombine(windowsFolder, "Prefetch"),
                SafeCombine(programData, "Microsoft", "Windows", "Caches"),
            }.Where(p => !string.IsNullOrWhiteSpace(p)).ToList()),
            new("Cache Windows Update", new List<string>
            {
                SafeCombine(windowsFolder, "SoftwareDistribution", "Download"),
                SafeCombine(windowsFolder, "SoftwareDistribution", "DataStore", "Logs"),
            }.Where(p => !string.IsNullOrWhiteSpace(p)).ToList()),
            new("Anciennes mises à jour / packages obsolètes (DISM)", Array.Empty<string>(), WindowsOnly: true, CustomCleanup: RunDismComponentCleanupAsync, CountExecutionAsCleanup: true),
            new("Fichiers de logs système anciens", new List<string>
            {
                SafeCombine(windowsFolder, "Logs"),
                SafeCombine(windowsFolder, "System32", "LogFiles"),
                SafeCombine(programData, "Microsoft", "Windows", "WER", "ReportArchive"),
                SafeCombine(programData, "Microsoft", "Windows", "WER", "ReportQueue"),
            }.Where(p => !string.IsNullOrWhiteSpace(p)).ToList(), FileFilter: fi => fi.Extension.Equals(".log", StringComparison.OrdinalIgnoreCase) && fi.LastWriteTimeUtc < sevenDaysAgo),
            new("Crash dumps et erreurs Windows", new List<string>
            {
                SafeCombine(windowsFolder, "Minidump"),
                SafeCombine(windowsFolder, "MEMORY.DMP"),
                SafeCombine(localAppData, "CrashDumps"),
            }.Where(p => !string.IsNullOrWhiteSpace(p)).ToList(), WindowsOnly: true),
            new("Dossiers temporaires utilisateurs profonds", new List<string>
            {
                tempPath,
                SafeCombine(localAppData, "Temp"),
                SafeCombine(appData, "Temp"),
            }.Where(p => !string.IsNullOrWhiteSpace(p)).ToList(), WindowsOnly: false)
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

    private async Task<CleanupStats> RunDismComponentCleanupAsync(CancellationToken ct)
    {
        if (!_isWindows())
        {
            return new CleanupStats();
        }

        try
        {
            var result = await _commandRunner("dism.exe", "/Online /Cleanup-Image /StartComponentCleanup /Quiet", ct).ConfigureAwait(false);
            if (!result.Success)
            {
                return new CleanupStats();
            }
        }
        catch
        {
            return new CleanupStats();
        }

        return new CleanupStats();
    }

    private static Task<CommandResult> RunCommandAsync(string fileName, string arguments, CancellationToken ct)
    {
        return Task.Run(async () =>
        {
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
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
                await process.WaitForExitAsync(ct).ConfigureAwait(false);

                var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
                output += await process.StandardError.ReadToEndAsync().ConfigureAwait(false);

                return new CommandResult(process.ExitCode == 0, output);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new CommandResult(false, ex.Message);
            }
        }, ct);
    }

    public sealed record CommandResult(bool Success, string? Output);
}
