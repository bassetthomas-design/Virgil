using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Virgil.Services.Abstractions;
using Virgil.Services.Startup;

namespace Virgil.Services;

/// <summary>
/// Implémentation du service Performance (action 7 : libérer la RAM en mode "soft").
/// </summary>
public sealed class PerformanceService : IPerformanceService
{
    private readonly IProcessProvider _processProvider;
    private readonly IMemoryReader _memoryReader;
    private readonly IStandbyMemoryReleaser _standbyReleaser;
    private readonly IProcessWhitelistProvider _whitelistProvider;
    private readonly IAppMemoryTrimmer _appMemoryTrimmer;

    public PerformanceService(
        IProcessProvider? processProvider = null,
        IMemoryReader? memoryReader = null,
        IStandbyMemoryReleaser? standbyMemoryReleaser = null,
        IProcessWhitelistProvider? whitelistProvider = null,
        IAppMemoryTrimmer? appMemoryTrimmer = null)
    {
        _processProvider = processProvider ?? new WindowsProcessProvider();
        _memoryReader = memoryReader ?? new WindowsMemoryReader();
        _standbyReleaser = standbyMemoryReleaser ?? new NoAdminStandbyReleaser();
        _whitelistProvider = whitelistProvider ?? new ProcessMapWhitelistProvider();
        _appMemoryTrimmer = appMemoryTrimmer ?? new AppMemoryTrimmer();
    }

    public Task<ActionExecutionResult> EnableGamingModeAsync(CancellationToken ct = default)
        => Task.FromResult(ActionExecutionResult.NotAvailable("Mode performance non disponible"));

    public Task<ActionExecutionResult> RestoreNormalModeAsync(CancellationToken ct = default)
        => Task.FromResult(ActionExecutionResult.NotAvailable("Retour au mode normal non disponible"));

    public Task<ActionExecutionResult> AnalyzeStartupAsync(CancellationToken ct = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult(ActionExecutionResult.NotAvailable("Optimisation du démarrage uniquement disponible sur Windows."));
        }

        try
        {
            var optimizer = new StartupOptimizer(AppContext.BaseDirectory);
            var plan = optimizer.BuildAndApply();

            if (plan.Total == 0)
            {
                return Task.FromResult(ActionExecutionResult.NotAvailable("Aucun élément de démarrage détecté."));
            }

            var disabledApplied = plan.Disabled;
            var disabledPlanned = plan.DisablePlanned;
            var optionalCount = plan.Optionals;
            var keptCount = plan.Critical;
            var summary = $"Optimisation démarrage (safe) : {plan.Total} éléments scannés – gardés {keptCount}, optionnels {optionalCount}, désactivés {disabledApplied}/{disabledPlanned}.";

            var detailsLines = new List<string>();
            var impact = disabledApplied > 0
                ? "Impact attendu : démarrage plus léger (sans toucher aux composants critiques)."
                : "Impact attendu : diagnostic uniquement, aucun composant critique touché.";
            detailsLines.Add(impact);

            foreach (var entry in plan.Entries.Where(e => e.Decision == StartupDecision.Disable))
            {
                var status = entry.Applied ? "désactivé" : "proposé";
                var note = string.IsNullOrWhiteSpace(entry.ApplyNote) ? entry.Reason : entry.ApplyNote;
                detailsLines.Add($"- {entry.Entry.Name} ({entry.Entry.Source}): {status} – {note}");
            }

            foreach (var entry in plan.Entries.Where(e => e.Decision == StartupDecision.Optional).Take(5))
            {
                detailsLines.Add($"- {entry.Entry.Name} marqué optionnel : {entry.Reason}");
            }

            return Task.FromResult(ActionExecutionResult.Ok(summary, string.Join(Environment.NewLine, detailsLines)));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ActionExecutionResult.Failure($"Optimisation démarrage impossible : {ex.Message}"));
        }
    }

    public Task<ActionExecutionResult> CloseGamingSessionAsync(CancellationToken ct = default)
        => Task.FromResult(ActionExecutionResult.NotAvailable("Fermeture session gaming non implémentée"));

    public async Task<ActionExecutionResult> SoftRamFlushAsync(CancellationToken ct = default)
    {
        if (!_memoryReader.IsSupportedPlatform)
        {
            return ActionExecutionResult.NotAvailable("Libération RAM uniquement supportée sur Windows");
        }

        var whitelist = _whitelistProvider.GetNormalizedWhitelist();
        var before = _memoryReader.GetSnapshot();
        var reclaimedBytes = 0L;
        var processed = 0;
        var skippedByWhitelist = 0;
        var foregroundPid = _processProvider.TryGetForegroundProcessId();
        var standbyInfo = "";

        try
        {
            foreach (var process in _processProvider.EnumerateProcesses())
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    if (process.HasExited)
                        continue;

                    if (IsSystemProcess(process))
                        continue;

                    if (!IsBackgroundProcess(process, foregroundPid))
                        continue;

                    if (!IsWhitelisted(process, whitelist))
                    {
                        skippedByWhitelist++;
                        continue;
                    }

                    var beforeWorkingSet = process.WorkingSet;
                    if (beforeWorkingSet == 0)
                        continue;

                    if (process.TryTrimWorkingSet(out var trimmedBytes))
                    {
                        reclaimedBytes += Math.Max(0, trimmedBytes);
                        processed++;
                    }
                }
                catch
                {
                    // Best effort: ignorer les processus protégés ou déjà terminés.
                }
                finally
                {
                    process.Dispose();
                }
            }

            _standbyReleaser.TryRelease(out standbyInfo);
            _appMemoryTrimmer.Trim();

            var after = _memoryReader.GetSnapshot();
            var freedMb = Math.Max(0, after.AvailablePhysicalMb - before.AvailablePhysicalMb);
            var reclaimedMb = reclaimedBytes / (1024.0 * 1024);

            var summary = $"RAM libérée estimée : {freedMb:F1} Mo — avant {before.AvailablePhysicalMb:F1} Mo / après {after.AvailablePhysicalMb:F1} Mo (effet temporaire).";
            var details = $"Processus arrière-plan traités (liste blanche) : {processed}, trimming estimé : {reclaimedMb:F1} Mo.";

            if (freedMb <= 0)
            {
                details += " Windows reprend vite sa part, résultat net: 0 Mo.";
            }

            if (!string.IsNullOrWhiteSpace(standbyInfo))
            {
                details += $"\n{standbyInfo}";
            }

            if (whitelist.Count == 0)
            {
                details += "\nAucune liste blanche trouvée : aucun processus tiers touché, juste un coup de frais interne.";
            }
            else if (skippedByWhitelist > 0)
            {
                details += $"\nProcessus ignorés car hors liste blanche : {skippedByWhitelist}.";
            }

            details += "\nWindows reprendra ce qu’il veut. Profite du moment.";

            return ActionExecutionResult.Ok(summary, details);
        }
        catch (OperationCanceledException)
        {
            return ActionExecutionResult.Failure("Libération RAM annulée");
        }
        catch (Exception ex)
        {
            return ActionExecutionResult.Failure($"Libération RAM impossible : {ex.Message}");
        }
    }

    private static bool IsBackgroundProcess(IProcessHandle process, int? foregroundPid)
    {
        try
        {
            if (process.HasExited)
                return false;

            if (foregroundPid.HasValue && process.Id == foregroundPid.Value)
                return false;

            if (process.HasMainWindow)
                return false;
        }
        catch
        {
            return false;
        }

        return true;
    }

    private static bool IsSystemProcess(IProcessHandle process)
    {
        try
        {
            if (process.SessionId == 0)
                return true;

            var name = process.ProcessName;
            if (string.IsNullOrWhiteSpace(name))
                return true;

            return string.Equals(name, "System", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "Idle", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "Registry", StringComparison.OrdinalIgnoreCase)
                   || name.StartsWith("svchost", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return true;
        }
    }

    private static bool IsWhitelisted(IProcessHandle process, IReadOnlySet<string> whitelist)
    {
        if (whitelist.Count == 0)
            return false;

        try
        {
            var name = ProcessNameHelper.Normalize(process.ProcessName);
            return whitelist.Contains(name);
        }
        catch
        {
            return false;
        }
    }

    [DllImport("psapi.dll", SetLastError = true)]
    internal static extern bool EmptyWorkingSet(IntPtr hProcess);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private class MEMORYSTATUSEX
    {
        public uint dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    public record MemorySnapshot(double TotalPhysicalMb, double AvailablePhysicalMb);

    public interface IProcessProvider
    {
        IEnumerable<IProcessHandle> EnumerateProcesses();
        int? TryGetForegroundProcessId();
    }

    public interface IProcessHandle : IDisposable
    {
        int Id { get; }
        string ProcessName { get; }
        int SessionId { get; }
        bool HasExited { get; }
        bool HasMainWindow { get; }
        long WorkingSet { get; }
        bool TryTrimWorkingSet(out long reclaimedBytes);
    }

    public interface IMemoryReader
    {
        bool IsSupportedPlatform { get; }
        MemorySnapshot GetSnapshot();
    }

    public interface IStandbyMemoryReleaser
    {
        bool TryRelease(out string message);
    }

    public interface IProcessWhitelistProvider
    {
        IReadOnlySet<string> GetNormalizedWhitelist();
    }

    public interface IAppMemoryTrimmer
    {
        void Trim();
    }

    private sealed class WindowsProcessProvider : IProcessProvider
    {
        public IEnumerable<IProcessHandle> EnumerateProcesses()
        {
            var foregroundPid = TryGetForegroundProcessId();
            foreach (var process in Process.GetProcesses())
            {
                IProcessHandle? adapter = null;
                try
                {
                    adapter = new WindowsProcessHandle(process, isForeground: foregroundPid.HasValue && process.Id == foregroundPid.Value);
                }
                catch
                {
                    process.Dispose();
                }

                if (adapter != null)
                {
                    yield return adapter;
                }
            }
        }

        public int? TryGetForegroundProcessId()
        {
            try
            {
                var hwnd = GetForegroundWindow();
                if (hwnd == IntPtr.Zero)
                    return null;

                _ = GetWindowThreadProcessId(hwnd, out var pid);
                return (int)pid;
            }
            catch
            {
                return null;
            }
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    }

    private sealed class WindowsProcessHandle : IProcessHandle
    {
        private readonly Process _process;
        private readonly bool _isForeground;

        public WindowsProcessHandle(Process process, bool isForeground)
        {
            _process = process;
            _isForeground = isForeground;
        }

        public int Id => SafeGet(() => _process.Id, -1);
        public string ProcessName => SafeGet(() => _process.ProcessName, string.Empty);
        public int SessionId => SafeGet(() => _process.SessionId, 0);
        public bool HasExited => SafeGet(() => _process.HasExited, true);
        public bool HasMainWindow => !_isForeground && SafeGet(() => _process.MainWindowHandle != IntPtr.Zero, false);
        public long WorkingSet => SafeGet(() => _process.WorkingSet64, 0L);

        public bool TryTrimWorkingSet(out long reclaimedBytes)
        {
            reclaimedBytes = 0;
            var before = WorkingSet;
            if (before <= 0)
            {
                return false;
            }

            try
            {
                if (EmptyWorkingSet(_process.Handle))
                {
                    _process.Refresh();
                    var after = WorkingSet;
                    reclaimedBytes = Math.Max(0, before - after);
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        public void Dispose()
        {
            try { _process.Dispose(); } catch { }
        }

        private static T SafeGet<T>(Func<T> getter, T fallback)
        {
            try { return getter(); }
            catch { return fallback; }
        }
    }

    private sealed class WindowsMemoryReader : IMemoryReader
    {
        public bool IsSupportedPlatform => OperatingSystem.IsWindows();

        public MemorySnapshot GetSnapshot()
        {
            var status = new MEMORYSTATUSEX();
            if (!GlobalMemoryStatusEx(status))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            return new MemorySnapshot(
                TotalPhysicalMb: status.ullTotalPhys / (1024.0 * 1024),
                AvailablePhysicalMb: status.ullAvailPhys / (1024.0 * 1024));
        }
    }

    private sealed class NoAdminStandbyReleaser : IStandbyMemoryReleaser
    {
        public bool TryRelease(out string message)
        {
            message = "Libération du cache standby non disponible sans droits admin.";
            return false;
        }
    }

    private sealed class ProcessMapWhitelistProvider : IProcessWhitelistProvider
    {
        public IReadOnlySet<string> GetNormalizedWhitelist()
        {
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var path = Path.Combine(baseDir, "assets", "activity", "process-map.json");
                if (!File.Exists(path))
                {
                    return new HashSet<string>();
                }

                var json = File.ReadAllText(path);
                var map = JsonSerializer.Deserialize<ProcessMap>(json);
                if (map is null)
                {
                    return new HashSet<string>();
                }

                var names = map.AllProcesses()
                    .Select(ProcessNameHelper.Normalize)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                return names;
            }
            catch
            {
                return new HashSet<string>();
            }
        }

        private sealed class ProcessMap
        {
            public string[] Games { get; set; } = Array.Empty<string>();
            public string[] Browsers { get; set; } = Array.Empty<string>();
            public string[] IDE { get; set; } = Array.Empty<string>();
            public string[] Office { get; set; } = Array.Empty<string>();
            public string[] Media { get; set; } = Array.Empty<string>();
            public string[] Terminal { get; set; } = Array.Empty<string>();

            public IEnumerable<string> AllProcesses()
            {
                foreach (var name in Games.Concat(Browsers).Concat(IDE).Concat(Office).Concat(Media).Concat(Terminal))
                {
                    yield return name;
                }
            }
        }
    }

    private sealed class AppMemoryTrimmer : IAppMemoryTrimmer
    {
        public void Trim()
        {
            try
            {
                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized, blocking: false, compacting: true);
            }
            catch
            {
                // Best-effort GC trim.
            }
        }
    }

    public static class ProcessNameHelper
    {
        public static string Normalize(string name)
        {
            var cleaned = name?.Trim() ?? string.Empty;
            if (cleaned.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                cleaned = cleaned[..^4];
            }

            return cleaned.ToLowerInvariant();
        }
    }
}
