using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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
    private readonly PerformanceModeStateStore _stateStore = new();

    private const string HighPerformancePlanGuid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    private const string BalancedPlanGuid = "381b4222-f694-41f0-9685-ff5bb260df2e";

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

    public async Task<ActionExecutionResult> EnableGamingModeAsync(CancellationToken ct = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            return ActionExecutionResult.NotAvailable("Mode performance uniquement disponible sur Windows.");
        }

        var statusLines = new List<string>();
        var state = await _stateStore.LoadAsync(ct).ConfigureAwait(false);
        var powerPlan = await ApplyHighPerformancePlanAsync(state, ct).ConfigureAwait(false);
        statusLines.Add($"- Alimentation: {powerPlan.Status}");

        var systemBoost = BoostForegroundPriority(state);
        statusLines.Add($"- Système: {systemBoost.Status}");

        var gpuStatus = "- GPU: Proposition seulement (profil perf à activer manuellement si besoin).";
        statusLines.Add(gpuStatus);

        var overallSuccess = powerPlan.Applied || systemBoost.Applied;
        state.IsPerformanceModeActive = overallSuccess;
        await _stateStore.SaveAsync(state, ct).ConfigureAwait(false);

        var message = BuildPerformanceMessage("ACTIVÉ", statusLines, appendDisableCta: true);
        return overallSuccess
            ? ActionExecutionResult.Ok(message)
            : ActionExecutionResult.Failure(message);
    }

    public async Task<ActionExecutionResult> RestoreNormalModeAsync(CancellationToken ct = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            return ActionExecutionResult.NotAvailable("Mode performance uniquement disponible sur Windows.");
        }

        var statusLines = new List<string>();
        var state = await _stateStore.LoadAsync(ct).ConfigureAwait(false);
        if (!state.IsPerformanceModeActive)
        {
            return ActionExecutionResult.NotAvailable("Mode performance déjà désactivé.");
        }

        var revertedPower = await RestorePowerPlanAsync(state, ct).ConfigureAwait(false);
        statusLines.Add($"- Alimentation: {revertedPower.Status}");

        var restoredSystem = RestoreForegroundPriority(state);
        statusLines.Add($"- Système: {restoredSystem.Status}");

        var gpuStatus = "- GPU: Proposition seulement (rien n'avait été touché).";
        statusLines.Add(gpuStatus);

        var overallSuccess = revertedPower.Applied || restoredSystem.Applied;
        state.IsPerformanceModeActive = false;
        await _stateStore.SaveAsync(state, ct).ConfigureAwait(false);

        var message = BuildPerformanceMessage("DÉSACTIVÉ", statusLines, appendDisableCta: false);
        return overallSuccess
            ? ActionExecutionResult.Ok(message)
            : ActionExecutionResult.Failure(message);
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

    private static string BuildPerformanceMessage(string state, IEnumerable<string> statusLines, bool appendDisableCta)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Mode performance: {state}");
        foreach (var line in statusLines)
        {
            sb.AppendLine(line);
        }

        if (appendDisableCta)
        {
            sb.Append("Prochaine étape : Désactiver le mode performance.");
        }

        return sb.ToString().TrimEnd();
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

    private async Task<StepOutcome> ApplyCpuPerformanceTuningAsync(PerformanceModeState state, CancellationToken ct)
    {
        var query = await RunCommandAsync("powercfg", "/q SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMIN", ct).ConfigureAwait(false);
        if (query.Success)
        {
            state.PreviousMinProcessorAc ??= ExtractPowerIndex(query.StdOut, "Current AC Power Setting Index")
                                            ?? ExtractPowerIndex(query.StdErr, "Current AC Power Setting Index");
            state.PreviousMinProcessorDc ??= ExtractPowerIndex(query.StdOut, "Current DC Power Setting Index")
                                            ?? ExtractPowerIndex(query.StdErr, "Current DC Power Setting Index");
        }

        var setAc = await RunCommandAsync("powercfg", "/setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMIN 100", ct).ConfigureAwait(false);
        var setDc = await RunCommandAsync("powercfg", "/setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMIN 100", ct).ConfigureAwait(false);
        var apply = await RunCommandAsync("powercfg", "/S SCHEME_CURRENT", ct).ConfigureAwait(false);

        var applied = setAc.Success && setDc.Success && apply.Success;
        return applied
            ? new StepOutcome(true, "CPU réveillé, pas de sieste.")
            : new StepOutcome(false, "Réglages CPU inchangés (droits ou plan verrouillé).");
    }

    private async Task<StepOutcome> RestoreCpuSettingsAsync(PerformanceModeState state, CancellationToken ct)
    {
        if (state.PreviousMinProcessorAc is null && state.PreviousMinProcessorDc is null)
        {
            return new StepOutcome(false, "Réglages CPU laissés par défaut (aucun snapshot).");
        }

        var acValue = state.PreviousMinProcessorAc ?? 0;
        var dcValue = state.PreviousMinProcessorDc ?? 0;

        var setAc = await RunCommandAsync("powercfg", $"/setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMIN {acValue}", ct).ConfigureAwait(false);
        var setDc = await RunCommandAsync("powercfg", $"/setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMIN {dcValue}", ct).ConfigureAwait(false);
        var apply = await RunCommandAsync("powercfg", "/S SCHEME_CURRENT", ct).ConfigureAwait(false);

        var applied = setAc.Success && setDc.Success && apply.Success;
        if (applied)
        {
            state.PreviousMinProcessorAc = null;
            state.PreviousMinProcessorDc = null;
        }

        return applied
            ? new StepOutcome(true, "Réglages CPU remis comme avant.")
            : new StepOutcome(false, "Réglages CPU non restaurés (droits ou plan verrouillé).");
    }

    private async Task<StepOutcome> ApplyHighPerformancePlanAsync(PerformanceModeState state, CancellationToken ct)
    {
        var activePlan = await GetActivePowerPlanAsync(ct).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(activePlan) && string.IsNullOrWhiteSpace(state.PreviousPowerPlanGuid))
        {
            state.PreviousPowerPlanGuid = activePlan;
        }

        var powerResult = await RunCommandAsync("powercfg", $"/S {HighPerformancePlanGuid}", ct).ConfigureAwait(false);
        var cpuTuning = await ApplyCpuPerformanceTuningAsync(state, ct).ConfigureAwait(false);

        if (powerResult.Success)
        {
            var cpuPart = cpuTuning.Applied
                ? "CPU réveillé, pas de sieste."
                : "CPU non modifié (droits/restrictions).";
            var status = cpuTuning.Applied
                ? $"OK (plan Hautes performances appliqué, {cpuPart})"
                : $"Partiel (plan Hautes performances appliqué, {cpuPart})";
            return new StepOutcome(true, status);
        }

        var failureDetail = !string.IsNullOrWhiteSpace(powerResult.StdErr)
            ? powerResult.StdErr.Trim()
            : "powercfg indisponible";
        return new StepOutcome(false, $"Ignoré (impossible d'activer le plan perf : {failureDetail})");
    }

    private async Task<StepOutcome> RestorePowerPlanAsync(PerformanceModeState state, CancellationToken ct)
    {
        var targetPlan = string.IsNullOrWhiteSpace(state.PreviousPowerPlanGuid)
            ? BalancedPlanGuid
            : state.PreviousPowerPlanGuid;

        var powerResult = await RunCommandAsync("powercfg", $"/S {targetPlan}", ct).ConfigureAwait(false);
        var cpuRestore = await RestoreCpuSettingsAsync(state, ct).ConfigureAwait(false);

        if (powerResult.Success)
        {
            var cpuPart = cpuRestore.Applied
                ? "réglages CPU remis au calme"
                : "réglages CPU laissés tels quels";
            var status = cpuRestore.Applied
                ? $"OK (plan précédent restauré, {cpuPart})"
                : $"Partiel (plan précédent restauré, {cpuPart})";
            state.PreviousPowerPlanGuid = null;
            return new StepOutcome(true, status);
        }

        var failureDetail = !string.IsNullOrWhiteSpace(powerResult.StdErr)
            ? powerResult.StdErr.Trim()
            : "powercfg indisponible";
        return new StepOutcome(false, $"Ignoré (impossible de restaurer le plan: {failureDetail})");
    }

    private StepOutcome BoostForegroundPriority(PerformanceModeState state)
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            if (string.IsNullOrWhiteSpace(state.PreviousPriorityClass))
            {
                state.PreviousPriorityClass = process.PriorityClass.ToString();
            }

            process.PriorityClass = ProcessPriorityClass.High;
            return new StepOutcome(true, "Partiel (priorité de Virgil boostée; aucune liste de tâches non critiques à geler)");
        }
        catch (Exception ex)
        {
            return new StepOutcome(false, $"Ignoré (priorité avant-plan inchangée : {ex.Message})");
        }
    }

    private StepOutcome RestoreForegroundPriority(PerformanceModeState state)
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            if (!string.IsNullOrWhiteSpace(state.PreviousPriorityClass)
                && Enum.TryParse(state.PreviousPriorityClass, out ProcessPriorityClass parsed))
            {
                process.PriorityClass = parsed;
            }
            else
            {
                process.PriorityClass = ProcessPriorityClass.Normal;
            }

            state.PreviousPriorityClass = null;
            return new StepOutcome(true, "OK (priorité avant-plan remise à la normale)");
        }
        catch (Exception ex)
        {
            return new StepOutcome(false, $"Ignoré (priorité avant-plan non restaurée : {ex.Message})");
        }
    }

    private static int? ExtractPowerIndex(string source, string label)
    {
        var match = Regex.Match(source, $"{Regex.Escape(label)}:\\s*0x([0-9a-fA-F]+)");
        if (!match.Success)
        {
            return null;
        }

        return int.TryParse(match.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static async Task<string> GetActivePowerPlanAsync(CancellationToken ct)
    {
        var result = await RunCommandAsync("powercfg", "/getactivescheme", ct).ConfigureAwait(false);
        if (!result.Success)
        {
            return string.Empty;
        }

        var match = Regex.Match(result.StdOut, "Power Scheme GUID:\\s*([0-9a-fA-F-]+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private static async Task<CommandResult> RunCommandAsync(string fileName, string arguments, CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        var tcs = new TaskCompletionSource<int>();
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                outputBuilder.AppendLine(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                errorBuilder.AppendLine(e.Data);
            }
        };
        process.Exited += (_, _) => tcs.TrySetResult(process.ExitCode);

        try
        {
            if (!process.Start())
            {
                return CommandResult.Failure("Process start failed");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var registration = ct.Register(() => tcs.TrySetCanceled(ct));
            var exitCode = await tcs.Task.ConfigureAwait(false);
            return CommandResult.From(exitCode, outputBuilder.ToString(), errorBuilder.ToString());
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            return CommandResult.Failure("Commande annulée");
        }
        catch (Exception ex)
        {
            return CommandResult.Failure(ex.Message);
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

    private sealed record StepOutcome(bool Applied, string Status);

    private sealed record CommandResult(bool Success, int ExitCode, string StdOut, string StdErr)
    {
        public static CommandResult From(int exitCode, string stdOut, string stdErr)
            => new(exitCode == 0, exitCode, stdOut ?? string.Empty, stdErr ?? string.Empty);

        public static CommandResult Failure(string message)
            => new(false, -1, string.Empty, message);
    }

    public sealed class PerformanceModeState
    {
        public bool IsPerformanceModeActive { get; set; }
        public string? PreviousPowerPlanGuid { get; set; }
        public int? PreviousMinProcessorAc { get; set; }
        public int? PreviousMinProcessorDc { get; set; }
        public string? PreviousPriorityClass { get; set; }
    }

    public sealed class PerformanceModeStateStore
    {
        private static readonly string StatePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Virgil",
            "performance-mode.json");

        public async Task<PerformanceModeState> LoadAsync(CancellationToken ct)
        {
            try
            {
                if (!File.Exists(StatePath))
                {
                    return new PerformanceModeState();
                }

                await using var stream = File.OpenRead(StatePath);
                var state = await JsonSerializer.DeserializeAsync<PerformanceModeState>(stream, cancellationToken: ct).ConfigureAwait(false);
                return state ?? new PerformanceModeState();
            }
            catch
            {
                return new PerformanceModeState();
            }
        }

        public async Task SaveAsync(PerformanceModeState state, CancellationToken ct)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(StatePath)!);
                await using var stream = File.Create(StatePath);
                await JsonSerializer.SerializeAsync(stream, state, new JsonSerializerOptions { WriteIndented = true }, ct).ConfigureAwait(false);
            }
            catch
            {
                // Swallow persistence errors: best-effort only.
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
