using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using Virgil.Services.Abstractions;
using Virgil.Services.Network;
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
    private readonly IPlatformInfo _platformInfo;
    private readonly IPrivilegeChecker _privilegeChecker;
    private readonly ISystemCommandRunner _commandRunner;
    private readonly IPerformanceStateStore _stateStore;

    private const string HighPerformancePlan = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    private static readonly TimeSpan DefaultCommandTimeout = TimeSpan.FromSeconds(10);

    public PerformanceService(
        IProcessProvider? processProvider = null,
        IMemoryReader? memoryReader = null,
        IStandbyMemoryReleaser? standbyMemoryReleaser = null,
        IProcessWhitelistProvider? whitelistProvider = null,
        IAppMemoryTrimmer? appMemoryTrimmer = null,
        IPlatformInfo? platformInfo = null,
        IPrivilegeChecker? privilegeChecker = null,
        ISystemCommandRunner? commandRunner = null,
        IPerformanceStateStore? stateStore = null)
    {
        _processProvider = processProvider ?? new WindowsProcessProvider();
        _memoryReader = memoryReader ?? new WindowsMemoryReader();
        _standbyReleaser = standbyMemoryReleaser ?? new NoAdminStandbyReleaser();
        _whitelistProvider = whitelistProvider ?? new ProcessMapWhitelistProvider();
        _appMemoryTrimmer = appMemoryTrimmer ?? new AppMemoryTrimmer();
        _platformInfo = platformInfo ?? new RuntimePlatformInfo();
        _privilegeChecker = privilegeChecker ?? new WindowsPrivilegeChecker();
        _commandRunner = commandRunner ?? new SystemCommandRunner();
        _stateStore = stateStore ?? new FilePerformanceStateStore();
    }

    public async Task<ActionExecutionResult> EnableGamingModeAsync(CancellationToken ct = default)
    {
        if (!_platformInfo.IsWindows())
        {
            return ActionExecutionResult.NotAvailable("Mode performance disponible uniquement sur Windows");
        }

        var state = _stateStore.Load();
        var steps = new List<StepResult>();

        var powerPlan = await ApplyHighPerformancePlanAsync(state, ct).ConfigureAwait(false);
        state = powerPlan.State;
        steps.Add(powerPlan.Step);

        var cpuSettings = await ReduceCpuSavingsAsync(state, ct).ConfigureAwait(false);
        state = cpuSettings.State;
        steps.Add(cpuSettings.Step);

        var priority = await BoostForegroundPriorityAsync(state, ct).ConfigureAwait(false);
        state = priority.State;
        steps.Add(priority.Step);
        steps.Add(DisableNonCriticalBackgroundTasks());

        state = state with
        {
            IsActive = true,
            ActivatedAt = DateTimeOffset.UtcNow,
            ActivePowerPlanGuid = HighPerformancePlan
        };
        _stateStore.Save(state);

        var (powerStatus, systemStatus) = Summarize(steps);
        var gpuStatus = "Ignoré (proposition seulement)";
        var summary = new StringBuilder();
        summary.Append($"Mode performance: ACTIVÉ. Alimentation: {powerStatus}. Système: {systemStatus}. GPU: {gpuStatus}.");
        summary.Append(" Désactiver le mode performance ?");

        var details = BuildDetails(steps, gpuStatus, includeGpu: true, performanceEnabled: true);
        var success = steps.Any(s => s.Status is StepOutcome.Ok or StepOutcome.Partial) && steps.All(s => s.Status != StepOutcome.Fail);
        return new ActionExecutionResult(success, summary.ToString(), details);
    }

    public async Task<ActionExecutionResult> RestoreNormalModeAsync(CancellationToken ct = default)
    {
        if (!_platformInfo.IsWindows())
        {
            return ActionExecutionResult.NotAvailable("Retour au mode normal uniquement disponible sur Windows");
        }

        var state = _stateStore.Load();
        var steps = new List<StepResult>();

        if (!HasSnapshot(state))
        {
            steps.Add(new StepResult("Alimentation", StepOutcome.Skipped, "Plan d'origine inconnu (aucun état enregistré)"));
            steps.Add(new StepResult("Système", StepOutcome.Skipped, "Réglages initiaux non enregistrés"));

            var (missingPowerStatus, missingSystemStatus) = Summarize(steps);
            var missingGpuStatus = "Ignoré (aucun changement enregistré)";
            var missingSnapshotSummary =
                $"Mode performance: DÉSACTIVÉ. Alimentation: {missingPowerStatus}. Système: {missingSystemStatus}. GPU: {missingGpuStatus}. Impossible de restaurer complètement: état initial non enregistré.";
            var missingDetails = BuildDetails(steps, missingGpuStatus, includeGpu: true, performanceEnabled: false);
            return new ActionExecutionResult(false, missingSnapshotSummary, missingDetails);
        }

        steps.Add(await RestorePowerPlanAsync(state, ct).ConfigureAwait(false));
        steps.Add(await RestoreCpuSavingsAsync(state, ct).ConfigureAwait(false));
        steps.Add(await RestorePrioritySeparationAsync(state, ct).ConfigureAwait(false));
        steps.Add(RestoreBackgroundTasks(state));

        _stateStore.Clear();

        var (powerStatus, systemStatus) = Summarize(steps);
        var gpuStatus = "Ignoré (aucun réglage GPU enregistré)";
        var summary = $"Mode performance: DÉSACTIVÉ. Alimentation: {powerStatus}. Système: {systemStatus}. GPU: {gpuStatus}. Retour au mode \"je fais moins d'efforts\".";
        var details = BuildDetails(steps, gpuStatus, includeGpu: true, performanceEnabled: false);
        var success = steps.Any(s => s.Status is StepOutcome.Ok or StepOutcome.Partial) && steps.All(s => s.Status != StepOutcome.Fail);
        return new ActionExecutionResult(success, summary, details);
    }

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

    private enum StepOutcome
    {
        Ok,
        Partial,
        Skipped,
        Fail
    }

    private sealed record StepResult(string Component, StepOutcome Status, string Details);

    private async Task<(StepResult Step, PerformanceModeState State)> ApplyHighPerformancePlanAsync(PerformanceModeState state, CancellationToken ct)
    {
        var currentPlan = await GetActivePowerPlanAsync(ct).ConfigureAwait(false);
        if (currentPlan.Success && string.IsNullOrWhiteSpace(state.PreviousPowerPlanGuid))
        {
            state = state with { PreviousPowerPlanGuid = currentPlan.PlanGuid };
            _stateStore.Save(state);
        }

        var switchResult = await _commandRunner.RunAsync("powercfg", $"/S {HighPerformancePlan}", DefaultCommandTimeout, ct).ConfigureAwait(false);
        if (!switchResult.Success)
        {
            var reason = switchResult.PickMessage() ?? "powercfg indisponible";
            return (new StepResult("Alimentation", StepOutcome.Partial, $"Plan hautes performances non appliqué : {reason}"), state);
        }

        return (new StepResult("Alimentation", StepOutcome.Ok, "Plan \"Performances élevées\" appliqué"), state);
    }

    private async Task<(StepResult Step, PerformanceModeState State)> ReduceCpuSavingsAsync(PerformanceModeState state, CancellationToken ct)
    {
        var snapshot = await GetCpuThrottleMinAsync(ct).ConfigureAwait(false);
        if (snapshot.Success && state.PreviousCpuThrottleMinAc is null)
        {
            state = state with { PreviousCpuThrottleMinAc = snapshot.Value };
            _stateStore.Save(state);
        }

        var tweak = await _commandRunner.RunAsync("powercfg", "-setacvalueindex scheme_current sub_processor PROCTHROTTLEMIN 100", DefaultCommandTimeout, ct).ConfigureAwait(false);
        if (!tweak.Success)
        {
            var reason = tweak.PickMessage() ?? "réglage CPU ignoré";
            return (new StepResult("Alimentation", StepOutcome.Partial, $"Économies CPU non réduites : {reason}"), state);
        }

        return (new StepResult("Alimentation", StepOutcome.Ok, "Économies CPU réduites (AC)"), state);
    }

    private async Task<(StepResult Step, PerformanceModeState State)> BoostForegroundPriorityAsync(PerformanceModeState state, CancellationToken ct)
    {
        if (!_privilegeChecker.IsAdministrator())
        {
            return (new StepResult("Système", StepOutcome.Partial, "Priorité premier plan ignorée (droits admin requis)"), state);
        }

        try
        {
            const string keyPath = "SYSTEM\\CurrentControlSet\\Control\\PriorityControl";
            using var key = Registry.LocalMachine.OpenSubKey(keyPath, writable: true);
            if (key is null)
            {
                return (new StepResult("Système", StepOutcome.Partial, "Clé de priorité introuvable"), state);
            }

            var currentValue = key.GetValue("Win32PrioritySeparation");
            if (currentValue is int current && state.PreviousPrioritySeparation is null)
            {
                state = state with { PreviousPrioritySeparation = current }; // Persist snapshot.
                _stateStore.Save(state);
            }

            key.SetValue("Win32PrioritySeparation", 0x26, RegistryValueKind.DWord);
            return (new StepResult("Système", StepOutcome.Ok, "Priorité premier plan renforcée"), state);
        }
        catch (Exception ex)
        {
            return (new StepResult("Système", StepOutcome.Partial, $"Priorité premier plan non appliquée: {ex.Message}"), state);
        }
    }

    private StepResult DisableNonCriticalBackgroundTasks()
    {
        return new StepResult("Système", StepOutcome.Partial, "Tâches arrière-plan non critiques ignorées (aucune liste définie)");
    }

    private async Task<StepResult> RestorePowerPlanAsync(PerformanceModeState state, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(state.PreviousPowerPlanGuid))
        {
            return new StepResult("Alimentation", StepOutcome.Skipped, "Plan d'origine inconnu (aucun snapshot)");
        }

        var result = await _commandRunner.RunAsync("powercfg", $"/S {state.PreviousPowerPlanGuid}", DefaultCommandTimeout, ct).ConfigureAwait(false);
        if (!result.Success)
        {
            var reason = result.PickMessage() ?? "impossible de restaurer le plan";
            return new StepResult("Alimentation", StepOutcome.Partial, $"Plan d'origine non restauré : {reason}");
        }

        return new StepResult("Alimentation", StepOutcome.Ok, "Plan d'alimentation rétabli");
    }

    private async Task<StepResult> RestoreCpuSavingsAsync(PerformanceModeState state, CancellationToken ct)
    {
        if (state.PreviousCpuThrottleMinAc is null)
        {
            return new StepResult("Alimentation", StepOutcome.Skipped, "Réglage CPU ignoré (état initial manquant)");
        }

        var restore = await _commandRunner.RunAsync(
            "powercfg",
            $"-setacvalueindex scheme_current sub_processor PROCTHROTTLEMIN {state.PreviousCpuThrottleMinAc.Value}",
            DefaultCommandTimeout,
            ct).ConfigureAwait(false);

        if (!restore.Success)
        {
            var reason = restore.PickMessage() ?? "impossible de restaurer le réglage CPU";
            return new StepResult("Alimentation", StepOutcome.Partial, $"Réglage CPU non restauré : {reason}");
        }

        return new StepResult("Alimentation", StepOutcome.Ok, $"Réglage CPU restauré ({state.PreviousCpuThrottleMinAc.Value}%)");
    }

    private async Task<StepResult> RestorePrioritySeparationAsync(PerformanceModeState state, CancellationToken ct)
    {
        if (!_privilegeChecker.IsAdministrator())
        {
            return new StepResult("Système", StepOutcome.Partial, "Priorité système non rétablie (droits admin requis)");
        }

        if (state.PreviousPrioritySeparation is null)
        {
            return new StepResult("Système", StepOutcome.Skipped, "Priorité système déjà par défaut");
        }

        try
        {
            const string keyPath = "SYSTEM\\CurrentControlSet\\Control\\PriorityControl";
            using var key = Registry.LocalMachine.OpenSubKey(keyPath, writable: true);
            if (key is null)
            {
                return new StepResult("Système", StepOutcome.Partial, "Clé de priorité introuvable");
            }

            key.SetValue("Win32PrioritySeparation", state.PreviousPrioritySeparation.Value, RegistryValueKind.DWord);
            return new StepResult("Système", StepOutcome.Ok, "Priorité système restaurée");
        }
        catch (Exception ex)
        {
            return new StepResult("Système", StepOutcome.Partial, $"Priorité système inchangée: {ex.Message}");
        }
    }

    private StepResult RestoreBackgroundTasks(PerformanceModeState state)
    {
        if (state.DisabledTasks is null || state.DisabledTasks.Count == 0)
        {
            return new StepResult("Système", StepOutcome.Skipped, "Aucune tâche arrière-plan à réactiver");
        }

        return new StepResult("Système", StepOutcome.Partial, $"Réactivation non implémentée ({state.DisabledTasks.Count} tâches enregistrées)");
    }

    private async Task<(bool Success, int? Value)> GetCpuThrottleMinAsync(CancellationToken ct)
    {
        var query = await _commandRunner.RunAsync(
            "powercfg",
            "-q scheme_current sub_processor PROCTHROTTLEMIN",
            DefaultCommandTimeout,
            ct).ConfigureAwait(false);

        if (!query.Success || string.IsNullOrWhiteSpace(query.Output))
        {
            return (false, null);
        }

        var match = Regex.Match(query.Output, "Current AC Power Setting Index:\\s*0x([0-9a-fA-F]+)");
        if (!match.Success)
        {
            return (false, null);
        }

        try
        {
            var value = Convert.ToInt32(match.Groups[1].Value, 16);
            return (true, value);
        }
        catch
        {
            return (false, null);
        }
    }

    private async Task<(bool Success, string? PlanGuid)> GetActivePowerPlanAsync(CancellationToken ct)
    {
        var result = await _commandRunner.RunAsync("powercfg", "/GetActiveScheme", DefaultCommandTimeout, ct).ConfigureAwait(false);
        if (!result.Success || string.IsNullOrWhiteSpace(result.Output))
        {
            return (false, null);
        }

        var match = Regex.Match(result.Output, "([a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12})");
        return match.Success ? (true, match.Groups[1].Value) : (false, null);
    }

    private static (string powerStatus, string systemStatus) Summarize(List<StepResult> steps)
    {
        var power = steps.Where(s => s.Component == "Alimentation").ToList();
        var system = steps.Where(s => s.Component == "Système").ToList();

        return (FormatGroupStatus(power), FormatGroupStatus(system));
    }

    private static string FormatGroupStatus(List<StepResult> steps)
    {
        if (steps.Count == 0)
        {
            return "Ignoré";
        }

        if (steps.All(s => s.Status == StepOutcome.Ok))
        {
            return "OK";
        }

        if (steps.Any(s => s.Status == StepOutcome.Ok))
        {
            var firstPartial = steps.FirstOrDefault(s => s.Status != StepOutcome.Ok);
            return firstPartial is not null ? $"Partiel ({firstPartial.Details})" : "Partiel";
        }

        var partial = steps.FirstOrDefault(s => s.Status == StepOutcome.Partial);
        return partial is not null ? $"Partiel ({partial.Details})" : "Ignoré";
    }

    private static bool HasSnapshot(PerformanceModeState state)
    {
        var hasTasks = state.DisabledTasks is { Count: > 0 };
        return state.IsActive
            || !string.IsNullOrWhiteSpace(state.PreviousPowerPlanGuid)
            || state.PreviousPrioritySeparation is not null
            || state.PreviousCpuThrottleMinAc is not null
            || hasTasks;
    }

    private static string BuildDetails(List<StepResult> steps, string gpuStatus, bool includeGpu, bool performanceEnabled)
    {
        var sb = new StringBuilder();
        foreach (var step in steps)
        {
            sb.AppendLine($"- {step.Component}: {step.Status} — {step.Details}");
        }

        if (includeGpu)
        {
            sb.AppendLine($"- GPU: {gpuStatus}");
        }

        var remark = performanceEnabled
            ? "Ton PC passe en mode 'je fais des efforts'."
            : "Retour au mode 'je fais moins d'efforts'. C'est cohérent.";
        sb.Append(remark);
        return sb.ToString().TrimEnd();
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
