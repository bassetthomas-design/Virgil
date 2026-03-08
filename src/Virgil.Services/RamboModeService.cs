using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Virgil.Services.Startup;

namespace Virgil.Services;

public sealed class InactiveFolderCandidate
{
    public string Path { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public DateTime LastUseUtc { get; init; }
    public bool IsSelected { get; set; }
}

public sealed class DuplicateFileItem
{
    public string Path { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public string Hash { get; init; } = string.Empty;
    public bool IsSelected { get; set; }
}

public sealed class GhostFileCandidate
{
    public string Path { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public DateTime LastUseUtc { get; init; }
    public string Extension { get; init; } = string.Empty;
    public bool IsSelected { get; set; }
}

public sealed class DuplicateGroup
{
    public List<DuplicateFileItem> Files { get; init; } = new();
    public long SizeBytes { get; init; }
    public int Count { get; init; }
}

public sealed class RamboResult
{
    public bool Succeeded { get; init; }
    public long TempFilesFreedBytes { get; init; }
    public long BrowserCacheFreedBytes { get; init; }
    public long SystemCacheFreedBytes { get; init; }
    public long DuplicateFilesPotentialBytes { get; init; }
    public long InactiveFoldersPotentialBytes { get; init; }
    public long StandbyMemoryFreedBytes { get; init; }
    public int FilesDeleted { get; init; }
    public int FoldersDeleted { get; init; }
    public int EmptyFoldersDeleted { get; init; }
    public int HeavyProcessesClosed { get; init; }
    public int IgnoredItems { get; init; }
    public int FailedSteps { get; init; }
    public bool AutoContinueUsed { get; init; }
    public List<string> DiskInsights { get; init; } = new();
    public List<string> StartupInsights { get; init; } = new();
    public List<string> RamInsights { get; init; } = new();
    public List<InactiveFolderCandidate> InactiveFolders { get; init; } = new();
    public List<DuplicateGroup> DuplicateGroups { get; init; } = new();
    public List<GhostFileCandidate> GhostFiles { get; init; } = new();
    public string Summary { get; init; } = "Mode RAMBO terminé.";
    public string? FailureReason { get; init; }
    public List<string> ErrorLogs { get; init; } = new();

    public static RamboResult Failed(string reason) => new()
    {
        Succeeded = false,
        FailureReason = reason,
        Summary = "Mode RAMBO interrompu."
    };
}

public sealed class RamboModeService
{
    private static readonly string[] BrowserSafeSubFolders = ["Cache", "GPUCache", "Code Cache", "ShaderCache", "Crashpad", "Temp"];
    private static readonly string[] GlobalProtectedZones =
    [
        @"C:\Program Files",
        @"C:\Program Files (x86)",
        @"C:\Windows\System32",
        @"C:\Windows\WinSxS"
    ];

    private readonly BrowserCleanupService _browserCleanup;
    private readonly IStartupAnalyzer _startupAnalyzer;
    private readonly PerformanceService.IStandbyMemoryReleaser _standbyReleaser;
    private readonly ISystemCommandRunner _commandRunner;
    private readonly IConfirmationPrompt? _confirmationPrompt;
    private readonly DiskAutopsyService _diskAutopsy;
    private readonly QuarantineService _quarantine;

    public RamboModeService(
        BrowserCleanupService? browserCleanup = null,
        IStartupAnalyzer? startupAnalyzer = null,
        PerformanceService.IStandbyMemoryReleaser? standbyReleaser = null,
        ISystemCommandRunner? commandRunner = null,
        IConfirmationPrompt? confirmationPrompt = null,
        DiskAutopsyService? diskAutopsy = null,
        QuarantineService? quarantine = null)
    {
        _browserCleanup = browserCleanup ?? new BrowserCleanupService();
        _startupAnalyzer = startupAnalyzer ?? new StartupAnalyzer();
        _standbyReleaser = standbyReleaser ?? new NullStandbyReleaser();
        _commandRunner = commandRunner ?? new SystemCommandRunner();
        _confirmationPrompt = confirmationPrompt;
        _diskAutopsy = diskAutopsy ?? new DiskAutopsyService();
        _quarantine = quarantine ?? new QuarantineService();
    }

    public async Task<RamboResult> RunAsync(CancellationToken ct, Func<string, CancellationToken, Task>? narrateAsync = null)
    {
        if (!OperatingSystem.IsWindows())
        {
            return RamboResult.Failed("Mode disponible uniquement sous Windows.");
        }

        var diskInsights = new List<string>();
        var startupInsights = new List<string>();
        var ramInsights = new List<string>();
        var errorLogs = new List<string>();
        var inactiveFolders = new List<InactiveFolderCandidate>();
        var duplicateGroups = new List<DuplicateGroup>();
        var ghostFiles = new List<GhostFileCandidate>();

        long tempFreed = 0;
        long browserFreed = 0;
        long systemFreed = 0;
        long duplicatePotential = 0;
        long inactivePotential = 0;
        long standbyFreed = 0;
        int filesDeleted = 0;
        int foldersDeleted = 0;
        int emptyFoldersDeleted = 0;
        int heavyClosed = 0;
        int ignored = 0;
        int failedSteps = 0;
        var autoContinue = false;

        try
        {
            await NarrateAsync("Mode RAMBO activé.\nBon… c’est pas ma guerre, mais quelqu’un doit nettoyer ce système.", ct).ConfigureAwait(false);

            await ExecuteStepAsync("DiskInsights", "autopsie disque", () =>
            {
                var report = _diskAutopsy.Analyze();
                foreach (var entry in report.Entries.OrderByDescending(x => x.SizeBytes).Take(8))
                {
                    diskInsights.Add($"{entry.PathLabel} → {FormatBytes(entry.SizeBytes)}");
                }

                if (!string.IsNullOrWhiteSpace(report.Summary))
                {
                    diskInsights.Add(report.Summary);
                }

                return Task.CompletedTask;
            }, OnErrorAsync, ct).ConfigureAwait(false);

            await ExecuteStepAsync("StartupInsights", "insights démarrage", () =>
            {
                CollectStartupInsights(startupInsights);
                return Task.CompletedTask;
            }, OnErrorAsync, ct).ConfigureAwait(false);

            await ExecuteStepAsync("RamInsights", "insights RAM", () =>
            {
                CollectRamInsights(ramInsights);
                return Task.CompletedTask;
            }, OnErrorAsync, ct).ConfigureAwait(false);

            await NarrateAsync("Je cherche les dossiers oubliés.", ct).ConfigureAwait(false);
            await ExecuteStepAsync("InactiveFolders", "dossiers inactifs", () =>
            {
                var scan = ScanInactiveFolders();
                inactiveFolders.AddRange(scan);
                inactivePotential = scan.Sum(x => x.SizeBytes);
                return Task.CompletedTask;
            }, OnErrorAsync, ct).ConfigureAwait(false);

            await NarrateAsync("Je recherche des fichiers dupliqués.", ct).ConfigureAwait(false);
            await ExecuteStepAsync("DuplicateFiles", "fichiers dupliqués", () =>
            {
                var groups = ScanDuplicateFiles();
                duplicateGroups.AddRange(groups);
                duplicatePotential = groups.Sum(g => g.SizeBytes * Math.Max(0, g.Count - 1));
                return Task.CompletedTask;
            }, OnErrorAsync, ct).ConfigureAwait(false);

            await ExecuteStepAsync("GhostFiles", "fichiers fantômes", () =>
            {
                ghostFiles.AddRange(ScanGhostFiles());
                return Task.CompletedTask;
            }, OnErrorAsync, ct).ConfigureAwait(false);

            diskInsights.Add($"Duplicates → {FormatBytes(duplicatePotential)}");
            diskInsights.Add($"Inactive files → {FormatBytes(inactivePotential)}");
            diskInsights.Add($"Ghost files détectés → {ghostFiles.Count}");

            await ExecuteStepAsync("AnalyzeOrphans", "résidus logiciels", () =>
            {
                AnalyzeOrphanResidues(diskInsights);
                return Task.CompletedTask;
            }, OnErrorAsync, ct).ConfigureAwait(false);

            await NarrateAsync("Je nettoie les fichiers temporaires.", ct).ConfigureAwait(false);
            await ExecuteStepAsync("CleanupTemp", "temporaires", () =>
            {
                var metrics = CleanupFilesInRoots(GetTempCleanupRoots(), onlyOlderThan: DateTime.UtcNow.AddHours(-24), ct);
                tempFreed += metrics.FreedBytes;
                filesDeleted += metrics.FilesDeleted;
                foldersDeleted += metrics.FoldersDeleted;
                ignored += metrics.Ignored;
                return Task.CompletedTask;
            }, OnErrorAsync, ct).ConfigureAwait(false);

            await NarrateAsync("Je fouille les caches du système.", ct).ConfigureAwait(false);
            await ExecuteStepAsync("CleanupSystemCache", "caches système", () =>
            {
                var metrics = CleanupFilesInRoots(GetSystemCacheRoots(), null, ct);
                systemFreed += metrics.FreedBytes;
                filesDeleted += metrics.FilesDeleted;
                foldersDeleted += metrics.FoldersDeleted;
                ignored += metrics.Ignored;
                return Task.CompletedTask;
            }, OnErrorAsync, ct).ConfigureAwait(false);

            await ExecuteStepAsync("WindowsUpdateCache", "cache windows update", async () =>
            {
                await RunWindowsUpdateCacheCleanupAsync(ct).ConfigureAwait(false);
                var metrics = CleanupFilesInRoots(GetWindowsUpdateRoots(), null, ct);
                systemFreed += metrics.FreedBytes;
                filesDeleted += metrics.FilesDeleted;
                foldersDeleted += metrics.FoldersDeleted;
            }, OnErrorAsync, ct).ConfigureAwait(false);

            await NarrateAsync("Caches navigateurs détectés.\nJe m’en occupe.", ct).ConfigureAwait(false);
            await ExecuteStepAsync("BrowserCleanup", "cache navigateur", async () =>
            {
                var result = await _browserCleanup.CleanAsync(new BrowserCleanupOptions { Cache = true }, null, ct).ConfigureAwait(false);
                browserFreed += result.FreedBytes;
                filesDeleted += result.FilesDeleted;
                ignored += result.LockedItems;
            }, OnErrorAsync, ct).ConfigureAwait(false);

            await ExecuteStepAsync("EmptyFolders", "dossiers vides", () =>
            {
                emptyFoldersDeleted = RemoveEmptyFoldersInSafeRoots();
                foldersDeleted += emptyFoldersDeleted;
                return Task.CompletedTask;
            }, OnErrorAsync, ct).ConfigureAwait(false);

            var runModerate = _confirmationPrompt is not null && await _confirmationPrompt
                .ConfirmAsync("RAMBO souhaite effectuer des optimisations système.", ct)
                .ConfigureAwait(false);

            if (runModerate)
            {
                await NarrateAsync("Je libère de la mémoire.", ct).ConfigureAwait(false);
                await ExecuteStepAsync("ModerateActions", "optimisations système", async () =>
                {
                    standbyFreed += OptimizeStandbyAndWorkingSets();
                    heavyClosed += CleanupHeavyBackgroundProcesses();
                    await _commandRunner.RunAsync("ipconfig", "/flushdns", TimeSpan.FromSeconds(20), ct).ConfigureAwait(false);
                    await _commandRunner.RunAsync("taskkill", "/f /im explorer.exe", TimeSpan.FromSeconds(20), ct).ConfigureAwait(false);
                    await _commandRunner.RunAsync("cmd.exe", "/c start explorer.exe", TimeSpan.FromSeconds(20), ct).ConfigureAwait(false);
                    _ = await _commandRunner.RunAsync("wsreset.exe", "", TimeSpan.FromSeconds(20), ct).ConfigureAwait(false);
                }, OnErrorAsync, ct).ConfigureAwait(false);
            }

            await RunAdvancedActionWithConfirmationAsync("cleanmgr /sagerun", "cleanmgr.exe", "/sagerun:1", "peut durer plusieurs minutes.", ct);
            await RunAdvancedActionWithConfirmationAsync("DISM StartComponentCleanup", "dism.exe", "/Online /Cleanup-Image /StartComponentCleanup", "nettoie le magasin de composants.", ct);
            await RunAdvancedActionWithConfirmationAsync("DISM ResetBase", "dism.exe", "/Online /Cleanup-Image /StartComponentCleanup /ResetBase", "ResetBase empêche la désinstallation de versions antérieures des composants.", ct);
            await RunAdvancedActionWithConfirmationAsync("SFC", "sfc.exe", "/scannow", "sfc peut prendre du temps.", ct);
            await RunAdvancedActionWithConfirmationAsync("Winsock reset", "netsh", "winsock reset", "le reset winsock modifie la pile réseau.", ct);

            var windowsOld = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "..", "Windows.old");
            if (Directory.Exists(Path.GetFullPath(windowsOld)))
            {
                await RunAdvancedActionWithConfirmationAsync("Suppression Windows.old", "cmd.exe", "/c rd /s /q C:\\Windows.old", "la suppression de Windows.old retire la capacité de rollback.", ct);
            }

            await NarrateAsync("Mission accomplie.\nLe système est plus propre.", ct).ConfigureAwait(false);

            return new RamboResult
            {
                Succeeded = true,
                TempFilesFreedBytes = tempFreed,
                BrowserCacheFreedBytes = browserFreed,
                SystemCacheFreedBytes = systemFreed,
                DuplicateFilesPotentialBytes = duplicatePotential,
                InactiveFoldersPotentialBytes = inactivePotential,
                StandbyMemoryFreedBytes = standbyFreed,
                FilesDeleted = filesDeleted,
                FoldersDeleted = foldersDeleted,
                EmptyFoldersDeleted = emptyFoldersDeleted,
                HeavyProcessesClosed = heavyClosed,
                IgnoredItems = ignored,
                FailedSteps = failedSteps,
                AutoContinueUsed = autoContinue,
                DiskInsights = diskInsights,
                StartupInsights = startupInsights,
                RamInsights = ramInsights,
                InactiveFolders = inactiveFolders,
                DuplicateGroups = duplicateGroups,
                GhostFiles = ghostFiles,
                Summary = BuildSummary(tempFreed, browserFreed, systemFreed, filesDeleted, foldersDeleted, emptyFoldersDeleted, heavyClosed, ignored, failedSteps),
                ErrorLogs = errorLogs
            };
        }
        catch (Exception ex)
        {
            return new RamboResult
            {
                Succeeded = false,
                TempFilesFreedBytes = tempFreed,
                BrowserCacheFreedBytes = browserFreed,
                SystemCacheFreedBytes = systemFreed,
                DuplicateFilesPotentialBytes = duplicatePotential,
                InactiveFoldersPotentialBytes = inactivePotential,
                StandbyMemoryFreedBytes = standbyFreed,
                FilesDeleted = filesDeleted,
                FoldersDeleted = foldersDeleted,
                EmptyFoldersDeleted = emptyFoldersDeleted,
                HeavyProcessesClosed = heavyClosed,
                IgnoredItems = ignored,
                FailedSteps = failedSteps,
                AutoContinueUsed = autoContinue,
                DiskInsights = diskInsights,
                StartupInsights = startupInsights,
                RamInsights = ramInsights,
                InactiveFolders = inactiveFolders,
                DuplicateGroups = duplicateGroups,
                GhostFiles = ghostFiles,
                Summary = "Mode RAMBO: échec partiel.",
                FailureReason = ex.Message,
                ErrorLogs = errorLogs
            };
        }

        async Task NarrateAsync(string message, CancellationToken token)
        {
            if (narrateAsync is null || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            await narrateAsync(message, token).ConfigureAwait(false);
        }

        async Task<bool> OnErrorAsync(string stepName, string resource, Exception ex, CancellationToken token)
        {
            failedSteps++;
            if (autoContinue)
            {
                ignored++;
                AppendErrorLog(stepName, ex, resource, RamboErrorDecision.Continue, errorLogs);
                return true;
            }

            var decision = _confirmationPrompt is null
                ? new RamboErrorDialogResult { Decision = RamboErrorDecision.Stop }
                : await _confirmationPrompt.AskRamboErrorDecisionAsync(BuildFriendlyErrorMessage(ex), token).ConfigureAwait(false);

            autoContinue = autoContinue || decision.AutoContinueSimilarErrors;
            AppendErrorLog(stepName, ex, resource, decision.Decision, errorLogs);

            if (decision.Decision == RamboErrorDecision.Continue)
            {
                ignored++;
                return true;
            }

            throw new InvalidOperationException("RAMBO interrompu par l'utilisateur.", ex);
        }
    }

    private async Task RunWindowsUpdateCacheCleanupAsync(CancellationToken ct)
    {
        _ = await _commandRunner.RunAsync("sc.exe", "stop wuauserv", TimeSpan.FromSeconds(20), ct).ConfigureAwait(false);
        _ = await _commandRunner.RunAsync("sc.exe", "stop bits", TimeSpan.FromSeconds(20), ct).ConfigureAwait(false);
        _ = await _commandRunner.RunAsync("sc.exe", "start bits", TimeSpan.FromSeconds(20), ct).ConfigureAwait(false);
        _ = await _commandRunner.RunAsync("sc.exe", "start wuauserv", TimeSpan.FromSeconds(20), ct).ConfigureAwait(false);
    }

    private async Task RunAdvancedActionWithConfirmationAsync(string label, string fileName, string args, string warning, CancellationToken ct)
    {
        if (_confirmationPrompt is null)
        {
            return;
        }

        var confirmed = await _confirmationPrompt
            .ConfirmAsync($"Action avancée: {label}. Attention: {warning} Continuer ?", ct)
            .ConfigureAwait(false);
        if (!confirmed)
        {
            return;
        }

        var result = await _commandRunner.RunAsync(fileName, args, TimeSpan.FromMinutes(30), ct).ConfigureAwait(false);
        if (!result.Success && result.PickMessage()?.Contains("denied", StringComparison.OrdinalIgnoreCase) == true)
        {
            var elevate = await _confirmationPrompt.ConfirmAsync($"L'opération {label} nécessite des droits administrateur ciblés. Lancer en mode administrateur ?", ct).ConfigureAwait(false);
            if (elevate)
            {
                var escapedArgs = args.Replace("\"", "\\\"");
                var psArgs = $"-NoProfile -Command \"Start-Process -FilePath '{fileName}' -ArgumentList '{escapedArgs}' -Verb runAs -Wait\"";
                _ = await _commandRunner.RunAsync("powershell.exe", psArgs, TimeSpan.FromMinutes(30), ct).ConfigureAwait(false);
            }
        }
    }

    private static void CollectDiskInsights(List<string> insights)
    {
        var user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var targets = new[] { "Downloads", "Videos", "Pictures", "Desktop", "Documents", "Steam" }
            .Select(name => Path.Combine(user, name))
            .Where(Directory.Exists)
            .Select(path => (Name: Path.GetFileName(path), Bytes: TryGetDirectorySize(path)))
            .Where(x => x.Bytes > 0)
            .OrderByDescending(x => x.Bytes)
            .Take(6);

        foreach (var item in targets)
        {
            insights.Add($"{item.Name} utilise {FormatBytes(item.Bytes)}");
        }
    }

    private void CollectStartupInsights(List<string> insights)
    {
        var report = _startupAnalyzer.Analyze();
        insights.Add($"Éléments de démarrage: {report.Items.Count}");
        foreach (var item in report.Items.Where(i => i.RecommendedForDisable).Take(3))
        {
            insights.Add($"Non essentiel: {item.Name}");
        }
    }

    private static void CollectRamInsights(List<string> insights)
    {
        foreach (var p in Process.GetProcesses().OrderByDescending(x => SafeWorkingSet(x)).Take(5))
        {
            try
            {
                if (IsProtectedProcess(p))
                {
                    continue;
                }

                insights.Add($"{p.ProcessName} utilise {FormatBytes(SafeWorkingSet(p))}");
            }
            finally
            {
                p.Dispose();
            }
        }
    }

    private static List<InactiveFolderCandidate> ScanInactiveFolders(long minSizeBytes = 500L * 1024 * 1024, int inactiveDays = 180)
    {
        var now = DateTime.UtcNow;
        return EnumerateUserScanRoots()
            .SelectMany(TryEnumerateDirectories)
            .Where(path => !IsProtectedPath(path))
            .Select(path => new InactiveFolderCandidate
            {
                Path = path,
                Name = System.IO.Path.GetFileName(path),
                SizeBytes = TryGetDirectorySize(path),
                LastUseUtc = SafeGetLastWriteUtc(path)
            })
            .Where(x => x.SizeBytes > minSizeBytes && (now - x.LastUseUtc).TotalDays > inactiveDays)
            .OrderByDescending(x => x.SizeBytes)
            .Take(100)
            .ToList();
    }

    private static List<DuplicateGroup> ScanDuplicateFiles()
    {
        var files = EnumerateUserScanRoots()
            .SelectMany(path => TryEnumerateFiles(path))
            .Where(path => !IsProtectedPath(path))
            .Select(path => new FileInfo(path))
            .Where(fi => fi.Exists && fi.Length > 0)
            .GroupBy(fi => fi.Length)
            .Where(g => g.Count() > 1)
            .Take(500);

        var groups = new List<DuplicateGroup>();
        foreach (var sizeGroup in files)
        {
            var hashGroups = sizeGroup.GroupBy(fi => ComputeHash(fi.FullName)).Where(g => !string.IsNullOrWhiteSpace(g.Key) && g.Count() > 1);
            foreach (var hashGroup in hashGroups)
            {
                var items = hashGroup.Select(fi => new DuplicateFileItem
                {
                    Path = fi.FullName,
                    Name = fi.Name,
                    SizeBytes = fi.Length,
                    Hash = hashGroup.Key ?? string.Empty
                }).ToList();

                groups.Add(new DuplicateGroup
                {
                    Files = items,
                    SizeBytes = items.First().SizeBytes,
                    Count = items.Count
                });
            }
        }

        return groups.OrderByDescending(x => x.SizeBytes * x.Count).Take(100).ToList();
    }


    private static List<GhostFileCandidate> ScanGhostFiles(long minBytes = 1024L * 1024 * 1024, int inactiveDays = 90)
    {
        var now = DateTime.UtcNow;
        var largeExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".iso", ".rar", ".zip", ".mkv", ".mp4" };

        return EnumerateUserScanRoots()
            .SelectMany(TryEnumerateFiles)
            .Where(path => !IsProtectedPath(path))
            .Select(path => new FileInfo(path))
            .Where(fi => fi.Exists && fi.Length >= minBytes && largeExtensions.Contains(fi.Extension) && (now - fi.LastAccessTimeUtc).TotalDays > inactiveDays)
            .OrderByDescending(fi => fi.Length)
            .Take(150)
            .Select(fi => new GhostFileCandidate
            {
                Path = fi.FullName,
                Name = fi.Name,
                SizeBytes = fi.Length,
                LastUseUtc = fi.LastAccessTimeUtc,
                Extension = fi.Extension
            })
            .ToList();
    }

    private static string ComputeHash(string path)
    {
        try
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(path);
            var hash = sha.ComputeHash(stream);
            return Convert.ToHexString(hash);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static void AnalyzeOrphanResidues(List<string> insights)
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)
        };

        foreach (var root in roots.Where(Directory.Exists))
        {
            foreach (var dir in Directory.EnumerateDirectories(root).Take(10))
            {
                var ageDays = (DateTime.UtcNow - SafeGetLastWriteUtc(dir)).TotalDays;
                if (ageDays < 180)
                {
                    continue;
                }

                var size = TryGetDirectorySize(dir);
                if (size < 100L * 1024 * 1024)
                {
                    continue;
                }

                insights.Add($"Résidu suspect (analyse): {dir} ({FormatBytes(size)}, {ageDays:F0} jours)");
            }
        }
    }

    private static CleanupMetrics CleanupFilesInRoots(IEnumerable<string> roots, DateTime? onlyOlderThan, CancellationToken ct)
    {
        long freed = 0;
        int filesDeleted = 0;
        int foldersDeleted = 0;
        int ignored = 0;

        foreach (var root in roots.Distinct(StringComparer.OrdinalIgnoreCase).Where(Directory.Exists))
        {
            foreach (var file in TryEnumerateFiles(root))
            {
                ct.ThrowIfCancellationRequested();
                if (!IsSafeDeletionPath(file))
                {
                    ignored++;
                    continue;
                }

                try
                {
                    var fi = new FileInfo(file);
                    if (onlyOlderThan.HasValue && fi.LastWriteTimeUtc > onlyOlderThan.Value)
                    {
                        continue;
                    }

                    var len = fi.Length;
                    fi.IsReadOnly = false;
                    fi.Delete();
                    freed += len;
                    filesDeleted++;
                }
                catch
                {
                    ignored++;
                }
            }

            foreach (var dir in TryEnumerateDirectories(root).OrderByDescending(x => x.Length))
            {
                if (!IsSafeDeletionPath(dir))
                {
                    continue;
                }

                try
                {
                    if (!Directory.EnumerateFileSystemEntries(dir).Any())
                    {
                        Directory.Delete(dir, false);
                        foldersDeleted++;
                    }
                }
                catch
                {
                    ignored++;
                }
            }
        }

        return new CleanupMetrics(freed, filesDeleted, foldersDeleted, ignored);
    }

    private static int RemoveEmptyFoldersInSafeRoots()
    {
        return CleanupFilesInRoots(GetSafeCleanupRoots(), null, CancellationToken.None).FoldersDeleted;
    }

    private long OptimizeStandbyAndWorkingSets()
    {
        _standbyReleaser.TryRelease(out _);
        var before = GC.GetTotalMemory(false);

        foreach (var p in Process.GetProcesses())
        {
            try
            {
                if (p.HasExited || IsProtectedProcess(p))
                {
                    continue;
                }

                PerformanceService.EmptyWorkingSet(p.Handle);
            }
            catch
            {
                // best effort
            }
            finally
            {
                p.Dispose();
            }
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var after = GC.GetTotalMemory(true);
        return Math.Max(0, before - after);
    }

    private static int CleanupHeavyBackgroundProcesses()
    {
        var closed = 0;
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                if (p.HasExited || IsProtectedProcess(p) || p.WorkingSet64 <= 700L * 1024 * 1024)
                {
                    continue;
                }

                if (!p.CloseMainWindow())
                {
                    p.Kill(false);
                }

                closed++;
            }
            catch
            {
                // skip
            }
            finally
            {
                p.Dispose();
            }
        }

        return closed;
    }

    private static bool IsSafeDeletionPath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (IsProtectedPath(fullPath))
        {
            return false;
        }

        return GetSafeCleanupRoots().Any(root => IsWithin(fullPath, root));
    }

    private static bool IsProtectedPath(string path)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        if (GlobalProtectedZones.Any(zone => IsWithin(path, zone)))
        {
            return true;
        }

        return string.Equals(path, appData, StringComparison.OrdinalIgnoreCase)
               || string.Equals(path, localAppData, StringComparison.OrdinalIgnoreCase)
               || string.Equals(path, programData, StringComparison.OrdinalIgnoreCase)
               || IsWithin(path, Path.Combine(localAppData, "Programs"));
    }

    private static bool IsWithin(string path, string root)
    {
        var fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> GetTempCleanupRoots()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        return new[] { Path.GetTempPath(), Path.Combine(local, "Temp"), Path.Combine(windows, "Temp") };
    }

    private static IEnumerable<string> GetWindowsUpdateRoots()
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        return new[]
        {
            Path.Combine(windows, "SoftwareDistribution", "Download"),
            Path.Combine(windows, "SoftwareDistribution", "DataStore"),
            Path.Combine(windows, "SoftwareDistribution", "DeliveryOptimization")
        };
    }

    private static IEnumerable<string> GetSystemCacheRoots()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        return new[]
        {
            Path.Combine(programData, "Microsoft", "Windows", "WER"),
            Path.Combine(windows, "Logs"),
            Path.Combine(windows, "Logs", "CBS"),
            Path.Combine(windows, "Panther"),
            Path.Combine(local, "Microsoft", "Windows", "Explorer"),
            Path.Combine(local, "D3DSCache"),
            Path.Combine(windows, "Minidump"),
            Path.Combine(programData, "Microsoft", "Diagnosis"),
            Path.Combine(programData, "Microsoft", "Windows Defender", "Scans", "History"),
            Path.Combine(programData, "Microsoft", "Windows", "DeliveryOptimization")
        };
    }

    private static IEnumerable<string> GetSafeCleanupRoots()
        => GetTempCleanupRoots().Concat(GetWindowsUpdateRoots()).Concat(GetSystemCacheRoots());

    private static IEnumerable<string> EnumerateUserScanRoots()
    {
        var user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var names = new[] { "Downloads", "Documents", "Videos", "Pictures", "Desktop" };
        return names.Select(x => Path.Combine(user, x)).Where(Directory.Exists);
    }

    private static IEnumerable<string> TryEnumerateFiles(string root)
    {
        try { return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories); }
        catch { return Array.Empty<string>(); }
    }

    private static IEnumerable<string> TryEnumerateDirectories(string root)
    {
        try { return Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly); }
        catch { return Array.Empty<string>(); }
    }

    private static DateTime SafeGetLastWriteUtc(string path)
    {
        try { return Directory.GetLastWriteTimeUtc(path); }
        catch { return DateTime.UtcNow; }
    }

    private static long SafeWorkingSet(Process p)
    {
        try { return p.WorkingSet64; }
        catch { return 0; }
    }

    private static bool IsProtectedProcess(Process p)
    {
        var name = p.ProcessName.ToLowerInvariant();
        return name is "system" or "idle" or "registry"
               || p.SessionId == 0
               || name.Contains("defender")
               || name.Contains("service");
    }

    private static long TryGetDirectorySize(string path)
    {
        try
        {
            return Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                .Select(file =>
                {
                    try { return new FileInfo(file).Length; }
                    catch { return 0L; }
                })
                .Sum();
        }
        catch
        {
            return 0;
        }
    }

    private static string BuildSummary(long temp, long browser, long systemCache, int filesDeleted, int foldersDeleted, int emptyFolders, int processesClosed, int ignored, int failed)
    {
        var lines = new List<string> { "Mode RAMBO terminé." };
        if (temp > 0) lines.Add($"• {FormatBytes(temp)} de fichiers temporaires supprimés");
        if (browser > 0) lines.Add($"• {FormatBytes(browser)} de caches navigateurs nettoyés");
        if (systemCache > 0) lines.Add($"• {FormatBytes(systemCache)} de caches système nettoyés");
        if (filesDeleted > 0) lines.Add($"• {filesDeleted} fichiers supprimés");
        if (foldersDeleted > 0) lines.Add($"• {foldersDeleted} dossiers supprimés");
        if (emptyFolders > 0) lines.Add($"• {emptyFolders} dossiers vides supprimés");
        if (processesClosed > 0) lines.Add($"• {processesClosed} processus fermés");
        if (ignored > 0) lines.Add($"• {ignored} éléments ignorés");
        if (failed > 0) lines.Add($"• {failed} étapes en échec");
        return string.Join(Environment.NewLine, lines);
    }

    public static string FormatBytes(long bytes)
    {
        const long kb = 1024;
        const long mb = 1024 * kb;
        const long gb = 1024 * mb;

        if (bytes >= gb) return string.Create(CultureInfo.InvariantCulture, $"{bytes / (double)gb:F1} GB");
        if (bytes >= mb) return string.Create(CultureInfo.InvariantCulture, $"{bytes / (double)mb:F0} MB");
        if (bytes >= kb) return string.Create(CultureInfo.InvariantCulture, $"{bytes / (double)kb:F0} KB");
        return string.Create(CultureInfo.InvariantCulture, $"{bytes} bytes");
    }

    private static async Task ExecuteStepAsync(string stepName, string resource, Func<Task> action, Func<string, string, Exception, CancellationToken, Task<bool>> errorHandler, CancellationToken ct)
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var shouldContinue = await errorHandler(stepName, resource, ex, ct).ConfigureAwait(false);
            if (!shouldContinue)
            {
                throw;
            }
        }
    }

    private static string BuildFriendlyErrorMessage(Exception ex)
    {
        return ex switch
        {
            UnauthorizedAccessException => "Accès refusé sur un élément protégé.",
            IOException => "Un fichier est en cours d'utilisation.",
            _ => "L'opération n'a pas pu se terminer correctement."
        };
    }

    private static void AppendErrorLog(string stepName, Exception ex, string resource, RamboErrorDecision decision, List<string> logs)
    {
        logs.Add($"StepName={stepName}; ExceptionType={ex.GetType().Name}; Resource={resource}; UserDecision={decision}");
    }

    public async Task<IReadOnlyList<string>> MoveInactiveFoldersToQuarantineAsync(IEnumerable<InactiveFolderCandidate> folders, CancellationToken ct = default)
    {
        var moved = new List<string>();
        foreach (var folder in folders.Where(x => x.IsSelected))
        {
            if (!Directory.Exists(folder.Path) || IsProtectedPath(folder.Path))
            {
                continue;
            }

            moved.Add(await _quarantine.MoveFolderAsync(folder.Path, ct).ConfigureAwait(false));
        }

        return moved;
    }

    public static IReadOnlyList<string> BuildSafeDuplicateDeletionPlan(IEnumerable<DuplicateGroup> groups)
    {
        var plan = new List<string>();
        foreach (var group in groups)
        {
            var selected = group.Files.Where(x => x.IsSelected).ToList();
            if (selected.Count >= group.Files.Count)
            {
                selected = selected.Take(group.Files.Count - 1).ToList();
            }

            plan.AddRange(selected.Select(x => x.Path));
        }

        return plan;
    }

    private sealed class NullStandbyReleaser : PerformanceService.IStandbyMemoryReleaser
    {
        public bool TryRelease(out string message)
        {
            message = string.Empty;
            return false;
        }
    }

    private readonly record struct CleanupMetrics(long FreedBytes, int FilesDeleted, int FoldersDeleted, int Ignored);
}
