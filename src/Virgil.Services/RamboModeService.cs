using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using Virgil.Services.Startup;

namespace Virgil.Services;

public sealed record RamboResult(
    bool Succeeded,
    double TempFilesFreedBytes,
    double BrowserCacheFreedBytes,
    int EmptyFoldersRemoved,
    int OrphanFoldersRemoved,
    double StandbyMemoryFreedBytes,
    int HeavyProcessesClosed,
    List<string> DiskInsights,
    List<string> StartupInsights,
    List<string> RamInsights,
    string Summary,
    string? FailureReason)
{
    public static RamboResult Failed(string reason)
        => new(
            false,
            0,
            0,
            0,
            0,
            0,
            0,
            new List<string>(),
            new List<string>(),
            new List<string>(),
            "Mode RAMBO interrompu.",
            reason);
}

public sealed class RamboModeService
{
    private static readonly string[] ProtectedVendors =
    {
        "microsoft", "windows", "nvidia", "amd", "intel", "realtek", "defender", "driver"
    };

    private readonly BrowserCleanupService _browserCleanup;
    private readonly IStartupAnalyzer _startupAnalyzer;
    private readonly PerformanceService.IStandbyMemoryReleaser _standbyReleaser;
    private readonly ISystemCommandRunner _commandRunner;

    public RamboModeService(
        BrowserCleanupService? browserCleanup = null,
        IStartupAnalyzer? startupAnalyzer = null,
        PerformanceService.IStandbyMemoryReleaser? standbyReleaser = null,
        ISystemCommandRunner? commandRunner = null)
    {
        _browserCleanup = browserCleanup ?? new BrowserCleanupService();
        _startupAnalyzer = startupAnalyzer ?? new StartupAnalyzer();
        _standbyReleaser = standbyReleaser ?? new NullStandbyReleaser();
        _commandRunner = commandRunner ?? new SystemCommandRunner();
    }

    public async Task<RamboResult> RunAsync(CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows())
        {
            return RamboResult.Failed("Mode disponible uniquement sous Windows.");
        }

        var diagnostics = new List<string>();
        var diskInsights = new List<string>();
        var startupInsights = new List<string>();
        var ramInsights = new List<string>();

        var tempFreed = 0d;
        var browserFreed = 0d;
        var emptyRemoved = 0;
        var orphanRemoved = 0;
        var standbyFreed = 0d;
        var heavyClosed = 0;

        try
        {
            ct.ThrowIfCancellationRequested();

            // Phase 1 - baseline + insights
            CollectBaseline(diagnostics);
            CollectDiskInsights(diskInsights);
            CollectStartupInsights(startupInsights);
            CollectRamInsights(ramInsights);

            // Phase 2 - temp cleanup
            tempFreed += await CleanupTempAsync(ct).ConfigureAwait(false);

            // Phase 3 - windows cache cleanup
            tempFreed += await CleanupWindowsCachesAsync(ct).ConfigureAwait(false);

            // Phase 4 - Windows update cache cleanup
            await CleanupWindowsUpdateCacheAsync(ct).ConfigureAwait(false);

            // Phase 5 - browser cleanup
            var browserResult = await _browserCleanup.CleanAsync(
                new BrowserCleanupOptions
                {
                    Cache = true,
                    Cookies = false,
                    History = false,
                    Downloads = false,
                    Sessions = false,
                    SiteData = false,
                    Autofill = false,
                },
                progress: null,
                ct).ConfigureAwait(false);
            browserFreed += browserResult.FreedBytes;

            // Phase 6
            emptyRemoved += RemoveEmptyFolders();

            // Phase 7
            orphanRemoved += RemoveOrphanedResidues();

            // Phase 8
            standbyFreed += OptimizeMemory();

            // Phase 9
            heavyClosed += CleanupHeavyBackgroundProcesses();

            // Phase 10
            await SystemRefreshAsync(ct).ConfigureAwait(false);

            var summary = BuildSummary(tempFreed, browserFreed, emptyRemoved, orphanRemoved, standbyFreed, heavyClosed);
            return new RamboResult(
                true,
                tempFreed,
                browserFreed,
                emptyRemoved,
                orphanRemoved,
                standbyFreed,
                heavyClosed,
                diskInsights,
                startupInsights,
                ramInsights,
                summary,
                null);
        }
        catch (Exception ex)
        {
            diagnostics.Add(ex.Message);
            return new RamboResult(
                false,
                tempFreed,
                browserFreed,
                emptyRemoved,
                orphanRemoved,
                standbyFreed,
                heavyClosed,
                diskInsights,
                startupInsights,
                ramInsights,
                "Mode RAMBO: échec partiel.",
                ex.Message);
        }
    }

    private static string BuildSummary(double tempFreed, double browserFreed, int emptyRemoved, int orphanRemoved, double standbyFreed, int heavyClosed)
    {
        var totalGo = (tempFreed + browserFreed + standbyFreed) / (1024d * 1024d * 1024d);
        var folders = emptyRemoved + orphanRemoved;
        return string.Create(CultureInfo.InvariantCulture, $"Mode RAMBO terminé. Résumé: {totalGo:F2} Go nettoyés, {heavyClosed} processus fermés, {folders} dossiers supprimés.");
    }

    private static void CollectBaseline(List<string> diagnostics)
    {
        var systemDrive = DriveInfo.GetDrives().FirstOrDefault(d => d.IsReady && d.Name.StartsWith("C", StringComparison.OrdinalIgnoreCase));
        if (systemDrive is not null)
        {
            diagnostics.Add($"DiskFree={systemDrive.AvailableFreeSpace}");
        }

        using var current = Process.GetCurrentProcess();
        diagnostics.Add($"ProcessCount={Process.GetProcesses().Length}");
        diagnostics.Add($"WorkingSet={current.WorkingSet64}");
    }

    private static void CollectDiskInsights(List<string> insights)
    {
        var user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var targets = new[] { "Downloads", "Videos", "Documents", "Desktop", "Pictures", "Steam" }
            .Select(name => Path.Combine(user, name))
            .Where(Directory.Exists)
            .Select(path => (Name: Path.GetFileName(path), Bytes: TryGetDirectorySize(path)))
            .Where(x => x.Bytes > 0)
            .OrderByDescending(x => x.Bytes)
            .Take(5);

        foreach (var item in targets)
        {
            var gb = item.Bytes / (1024d * 1024d * 1024d);
            insights.Add($"{item.Name} utilise {gb:F1} Go");
        }
    }

    private void CollectStartupInsights(List<string> insights)
    {
        var report = _startupAnalyzer.Analyze();
        insights.Add($"Éléments de démarrage détectés: {report.Items.Count}");
        foreach (var item in report.Items.Where(i => i.RecommendedForDisable).Take(3))
        {
            insights.Add($"À surveiller: {item.Name}");
        }
    }

    private static void CollectRamInsights(List<string> insights)
    {
        foreach (var p in Process.GetProcesses()
                     .Where(p => !string.IsNullOrWhiteSpace(p.ProcessName))
                     .OrderByDescending(p => SafeWorkingSet(p))
                     .Take(5))
        {
            var gb = SafeWorkingSet(p) / (1024d * 1024d * 1024d);
            insights.Add($"{p.ProcessName} utilise {gb:F1} Go");
            p.Dispose();
        }
    }

    private static long SafeWorkingSet(Process p)
    {
        try { return p.WorkingSet64; }
        catch { return 0; }
    }

    private static async Task<double> CleanupTempAsync(CancellationToken ct)
    {
        var freed = 0L;
        var now = DateTime.UtcNow;
        var roots = new List<string>
        {
            Path.GetTempPath(),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "Local", "Temp"),
        };

        foreach (var root in roots.Distinct(StringComparer.OrdinalIgnoreCase).Where(Directory.Exists))
        {
            await Task.Yield();
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var info = new FileInfo(file);
                    if (info.LastWriteTimeUtc > now.AddHours(-24))
                    {
                        continue;
                    }

                    var len = info.Length;
                    info.IsReadOnly = false;
                    info.Delete();
                    freed += len;
                }
                catch
                {
                    // locked/safe skip
                }
            }
        }

        return freed;
    }

    private static async Task<double> CleanupWindowsCachesAsync(CancellationToken ct)
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var targets = new[]
        {
            Path.Combine(local, "Microsoft", "Windows", "Explorer"),
            Path.Combine(local, "Microsoft", "Windows", "WER"),
            Path.Combine(windows, "ProgramData", "Microsoft", "Windows", "WER"),
        };

        long freed = 0;
        foreach (var target in targets.Where(Directory.Exists))
        {
            await Task.Yield();
            foreach (var file in Directory.EnumerateFiles(target, "*", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();
                var name = Path.GetFileName(file);
                var isCache = name.StartsWith("thumbcache", StringComparison.OrdinalIgnoreCase)
                              || name.StartsWith("iconcache", StringComparison.OrdinalIgnoreCase)
                              || target.Contains("WER", StringComparison.OrdinalIgnoreCase);
                if (!isCache)
                {
                    continue;
                }

                try
                {
                    var info = new FileInfo(file);
                    var len = info.Length;
                    info.IsReadOnly = false;
                    info.Delete();
                    freed += len;
                }
                catch
                {
                    // skip locked
                }
            }
        }

        return freed;
    }

    private async Task CleanupWindowsUpdateCacheAsync(CancellationToken ct)
    {
        _ = await _commandRunner.RunAsync("sc.exe", "stop wuauserv", TimeSpan.FromSeconds(15), ct).ConfigureAwait(false);

        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SoftwareDistribution", "Download");
        if (Directory.Exists(path))
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var info = new FileInfo(file);
                    info.IsReadOnly = false;
                    info.Delete();
                }
                catch
                {
                    // skip locked
                }
            }
        }

        await Task.Yield();

        _ = await _commandRunner.RunAsync("sc.exe", "start wuauserv", TimeSpan.FromSeconds(15), ct).ConfigureAwait(false);
    }

    private static int RemoveEmptyFolders()
    {
        var removed = 0;
        foreach (var root in EnumerateSafeRoots())
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var dir in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length))
            {
                try
                {
                    if (!Directory.EnumerateFileSystemEntries(dir).Any())
                    {
                        Directory.Delete(dir, false);
                        removed++;
                    }
                }
                catch
                {
                    // skip
                }
            }
        }

        return removed;
    }

    private static int RemoveOrphanedResidues()
    {
        var installed = GetInstalledSoftwareNames();
        var removed = 0;
        foreach (var root in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                     Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)
                 }.Where(Directory.Exists))
        {
            foreach (var dir in Directory.EnumerateDirectories(root))
            {
                try
                {
                    var name = Path.GetFileName(dir);
                    var norm = name.ToLowerInvariant();
                    if (ProtectedVendors.Any(v => norm.Contains(v, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    if (installed.Any(app => norm.Contains(app, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    var age = DateTime.UtcNow - Directory.GetLastWriteTimeUtc(dir);
                    if (age < TimeSpan.FromDays(30))
                    {
                        continue;
                    }

                    if (dir.Contains("Program Files", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    Directory.Delete(dir, recursive: true);
                    removed++;
                }
                catch
                {
                    // skip protected/locked
                }
            }
        }

        return removed;
    }

    private double OptimizeMemory()
    {
        var before = GC.GetTotalMemory(forceFullCollection: false);
        _standbyReleaser.TryRelease(out _);

        foreach (var p in Process.GetProcesses())
        {
            try
            {
                if (p.HasExited || IsProtectedProcess(p) || p.ProcessName.Equals("explorer", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                PerformanceService.EmptyWorkingSet(p.Handle);
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

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var after = GC.GetTotalMemory(forceFullCollection: true);
        return Math.Max(0, before - after);
    }

    private static int CleanupHeavyBackgroundProcesses()
    {
        var closed = 0;
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                if (p.HasExited || IsProtectedProcess(p))
                {
                    continue;
                }

                if (p.WorkingSet64 <= 700L * 1024 * 1024)
                {
                    continue;
                }

                var name = p.ProcessName.ToLowerInvariant();
                var closable = name.Contains("launcher")
                               || name.Contains("updater")
                               || name.Contains("helper")
                               || name.Contains("discord")
                               || name.Contains("teams");
                if (!closable)
                {
                    continue;
                }

                if (!p.CloseMainWindow())
                {
                    p.Kill(entireProcessTree: false);
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

    private async Task SystemRefreshAsync(CancellationToken ct)
    {
        _ = await _commandRunner.RunAsync("ipconfig", "/flushdns", TimeSpan.FromSeconds(15), ct).ConfigureAwait(false);
        _ = await _commandRunner.RunAsync("taskkill", "/f /im explorer.exe", TimeSpan.FromSeconds(15), ct).ConfigureAwait(false);
        _ = await _commandRunner.RunAsync("cmd.exe", "/c start explorer.exe", TimeSpan.FromSeconds(15), ct).ConfigureAwait(false);
    }

    private static IEnumerable<string> EnumerateSafeRoots()
    {
        yield return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        yield return Path.GetTempPath();
    }

    private static bool IsProtectedProcess(Process p)
    {
        var name = p.ProcessName.ToLowerInvariant();
        return name is "system" or "idle" or "registry" or "explorer"
               || name.Contains("defender")
               || name.Contains("nvidia")
               || name.Contains("amd")
               || name.Contains("intel")
               || name.Contains("realtek")
               || name.Contains("audio")
               || name.Contains("driver")
               || p.SessionId == 0;
    }

    private static HashSet<string> GetInstalledSoftwareNames()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        LoadUninstallRegistry(set, RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
        LoadUninstallRegistry(set, RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
        return set;
    }

    private static void LoadUninstallRegistry(HashSet<string> set, RegistryHive hive, string keyPath)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
            using var uninstall = baseKey.OpenSubKey(keyPath);
            if (uninstall is null)
            {
                return;
            }

            foreach (var sub in uninstall.GetSubKeyNames())
            {
                using var appKey = uninstall.OpenSubKey(sub);
                var displayName = appKey?.GetValue("DisplayName") as string;
                if (!string.IsNullOrWhiteSpace(displayName))
                {
                    set.Add(displayName.ToLowerInvariant());
                }
            }
        }
        catch
        {
            // best effort
        }
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
    private sealed class NullStandbyReleaser : PerformanceService.IStandbyMemoryReleaser
    {
        public bool TryRelease(out string message)
        {
            message = "";
            return false;
        }
    }

}
