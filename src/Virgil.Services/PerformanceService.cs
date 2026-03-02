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
    private readonly IBackgroundProcessPolicyProvider _policyProvider;
    private readonly IAppMemoryTrimmer _appMemoryTrimmer;
    private readonly IPlatformInfo _platformInfo;
    private readonly IPrivilegeChecker _privilegeChecker;
    private readonly ISystemCommandRunner _commandRunner;
    private readonly IPerformanceStateStore _stateStore;
    private readonly IStartupAnalyzer _startupAnalyzer;
    private readonly StartupOptimizationService _startupOptimizationService;
    private readonly ICloseSessionConfirmation _sessionConfirmation;
    private readonly IProcessController _processController;
    private StartupAnalysis? _lastStartupAnalysis;

    private const string HighPerformancePlan = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    private static readonly TimeSpan DefaultCommandTimeout = TimeSpan.FromSeconds(10);

    public PerformanceService(
        IProcessProvider? processProvider = null,
        IMemoryReader? memoryReader = null,
        IStandbyMemoryReleaser? standbyMemoryReleaser = null,
        IProcessWhitelistProvider? whitelistProvider = null,
        IBackgroundProcessPolicyProvider? policyProvider = null,
        IAppMemoryTrimmer? appMemoryTrimmer = null,
        IPlatformInfo? platformInfo = null,
        IPrivilegeChecker? privilegeChecker = null,
        ISystemCommandRunner? commandRunner = null,
        IPerformanceStateStore? stateStore = null,
        IStartupAnalyzer? startupAnalyzer = null,
        StartupOptimizationService? startupOptimizationService = null,
        ICloseSessionConfirmation? sessionConfirmation = null,
        IProcessController? processController = null)
    {
        _processProvider = processProvider ?? new WindowsProcessProvider();
        _memoryReader = memoryReader ?? new WindowsMemoryReader();
        _standbyReleaser = standbyMemoryReleaser ?? new NoAdminStandbyReleaser();
        _whitelistProvider = whitelistProvider ?? new ProcessMapWhitelistProvider();
        _policyProvider = policyProvider ?? new BackgroundProcessPolicyProvider();
        _appMemoryTrimmer = appMemoryTrimmer ?? new AppMemoryTrimmer();
        _platformInfo = platformInfo ?? new RuntimePlatformInfo();
        _privilegeChecker = privilegeChecker ?? new WindowsPrivilegeChecker();
        _commandRunner = commandRunner ?? new SystemCommandRunner();
        _stateStore = stateStore ?? new FilePerformanceStateStore();
        _startupAnalyzer = startupAnalyzer ?? new StartupAnalyzer();
        _startupOptimizationService = startupOptimizationService ?? new StartupOptimizationService();
        _sessionConfirmation = sessionConfirmation ?? new AlwaysConfirmSession();
        _processController = processController ?? new WindowsProcessController();
    }

    public async Task<ActionExecutionResult> EnableGamingModeAsync(CancellationToken ct = default)
    {
        if (!_platformInfo.IsWindows())
        {
            return ActionExecutionResult.NotAvailable("Mode performance", "Disponible uniquement sur Windows");
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
        var anyOk = steps.Any(s => s.Status is StepOutcome.Ok or StepOutcome.Partial);
        var anyFail = steps.Any(s => s.Status == StepOutcome.Fail);
        var overallSummary = summary.ToString();
        if (anyOk && anyFail)
        {
            return ActionExecutionResult.Partial("Mode performance activé", $"{overallSummary}\n{details}");
        }

        if (anyFail)
        {
            return ActionExecutionResult.Failure("Mode performance activé", $"{overallSummary}\n{details}");
        }

        return ActionExecutionResult.Ok("Mode performance activé", $"{overallSummary}\n{details}");
    }

    public async Task<ActionExecutionResult> RestoreNormalModeAsync(CancellationToken ct = default)
    {
        if (!_platformInfo.IsWindows())
        {
            return ActionExecutionResult.NotAvailable("Retour au mode normal", "Uniquement disponible sur Windows");
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
            return ActionExecutionResult.Partial("Retour au mode normal", $"{missingSnapshotSummary}\n{missingDetails}");
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
        var anyOkRestore = steps.Any(s => s.Status is StepOutcome.Ok or StepOutcome.Partial);
        var anyFailRestore = steps.Any(s => s.Status == StepOutcome.Fail);
        var combined = $"{summary}\n{details}";
        if (anyOkRestore && anyFailRestore)
        {
            return ActionExecutionResult.Partial("Retour au mode normal terminé", combined);
        }

        if (anyFailRestore)
        {
            return ActionExecutionResult.Failure("Retour au mode normal terminé", combined);
        }

        return ActionExecutionResult.Ok("Retour au mode normal terminé", combined);
    }

    public Task<ActionExecutionResult> AnalyzeStartupAsync(CancellationToken ct = default)
    {
        if (!_platformInfo.IsWindows())
        {
            return Task.FromResult(ActionExecutionResult.NotAvailable("Analyse du démarrage", "Uniquement disponible sur Windows."));
        }

        try
        {
            var report = _startupAnalyzer.Analyze(ct);
            if (!report.Items.Any())
            {
                _lastStartupAnalysis = null;
                return Task.FromResult(ActionExecutionResult.NotAvailable("Analyse du démarrage", "Aucun élément de démarrage détecté ou accessible."));
            }

            var startupItems = report.Items
                .Where(item => item.Type.Contains("Registre Run", StringComparison.OrdinalIgnoreCase)
                               || item.Type.Contains("RunOnce", StringComparison.OrdinalIgnoreCase)
                               || item.Type.Contains("Dossier démarrage", StringComparison.OrdinalIgnoreCase)
                               || item.Type.Contains("Tâche planifiée", StringComparison.OrdinalIgnoreCase))
                .Select(item =>
                {
                    var type = ResolveStartupItemType(item.Type);
                    var location = ResolveLocation(item);
                    var id = BuildStartupItemId(item, location, type);
                    var isEssential = StartupOptimizationService.IsEssential(item.Name, item.Publisher, item.Path, item.Command)
                                      || (type == "ScheduledTask" && id.StartsWith("\\Microsoft\\", StringComparison.OrdinalIgnoreCase));
                    var isRecommended = item.RecommendedForDisable && !isEssential;
                    return new StartupItem(id, item.Name, location, item.Command ?? item.Path ?? string.Empty, type, isEssential, isRecommended, isRecommended);
                })
                .ToList();

            _lastStartupAnalysis = new StartupAnalysis(startupItems);
            var message = StartupAnalysisFormatter.BuildMessage(report);
            var recommendations = startupItems
                .Where(item => item.IsRecommended)
                .Select(item => $"{item.Name} ({item.Location})")
                .Take(10)
                .ToList();
            var debugInfo = JsonSerializer.Serialize(_lastStartupAnalysis);
            return Task.FromResult(ActionExecutionResult.Ok("Analyse du démarrage terminée", message, recommendations: recommendations, debugInfo: debugInfo));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ActionExecutionResult.Failure("Analyse du démarrage", $"Impossible : {ex.Message}"));
        }
    }

    public async Task<ActionExecutionResult> OptimizeStartupAsync(CancellationToken ct = default)
    {
        if (!_platformInfo.IsWindows())
        {
            return ActionExecutionResult.NotAvailable("Optimisation du démarrage", "Uniquement disponible sur Windows.");
        }

        if (_lastStartupAnalysis is null)
        {
            return ActionExecutionResult.NotAvailable("Optimisation du démarrage", "Analyse du démarrage requise avant l'optimisation.");
        }

        if (!_lastStartupAnalysis.HasRecommendations)
        {
            return ActionExecutionResult.NotAvailable("Optimisation du démarrage", "Rien à optimiser.");
        }

        var result = await _startupOptimizationService.OptimizeAsync(_lastStartupAnalysis, ct).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            var summary = string.IsNullOrWhiteSpace(result.FailureReason) ? result.Summary : result.FailureReason;
            return ActionExecutionResult.Failure("Optimisation du démarrage", summary);
        }

        var totalDisabled = result.ItemsDisabled + result.ServicesSetToManual + result.TasksDisabled;
        var summaryMessage = $"Démarrage: {totalDisabled} élément(s) désactivé(s). Tu peux revenir en arrière si besoin.";
        return ActionExecutionResult.Ok("Optimisation du démarrage terminée", summaryMessage);
    }

    public async Task<ActionExecutionResult> RestoreStartupAsync(CancellationToken ct = default)
    {
        if (!_platformInfo.IsWindows())
        {
            return ActionExecutionResult.NotAvailable("Restauration du démarrage", "Uniquement disponible sur Windows.");
        }

        var result = await _startupOptimizationService.RestoreRunEntriesAsync(ct).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            var summary = string.IsNullOrWhiteSpace(result.FailureReason) ? result.Summary : result.FailureReason;
            return ActionExecutionResult.Failure("Restauration du démarrage", summary);
        }

        return ActionExecutionResult.Ok("Restauration du démarrage terminée", result.Summary);
    }


    private static string ResolveStartupItemType(string sourceType)
    {
        if (sourceType.Contains("Tâche planifiée", StringComparison.OrdinalIgnoreCase))
        {
            return "ScheduledTask";
        }

        if (sourceType.Contains("Dossier démarrage", StringComparison.OrdinalIgnoreCase))
        {
            return "StartupFolder";
        }

        return "Registry";
    }

    private static string ResolveLocation(StartupAnalysisItem item)
    {
        if (item.Type.Contains("RunOnce", StringComparison.OrdinalIgnoreCase))
        {
            return item.Type.Contains("HKLM", StringComparison.OrdinalIgnoreCase) ? "HKLM RunOnce" : "HKCU RunOnce";
        }

        if (item.Type.Contains("Registre Run", StringComparison.OrdinalIgnoreCase))
        {
            return item.Type.Contains("HKLM", StringComparison.OrdinalIgnoreCase) ? "HKLM Run" : "HKCU Run";
        }

        if (item.Type.Contains("commun", StringComparison.OrdinalIgnoreCase))
        {
            return "Common Startup Folder";
        }

        if (item.Type.Contains("Dossier démarrage", StringComparison.OrdinalIgnoreCase))
        {
            return "Startup Folder";
        }

        return "Scheduled Task";
    }

    private static string BuildStartupItemId(StartupAnalysisItem item, string location, string type)
    {
        if (type == "StartupFolder")
        {
            return item.Path ?? item.Command ?? item.Name;
        }

        if (type == "ScheduledTask")
        {
            return item.Command ?? item.Name;
        }

        return $"{location}|{item.Name}";
    }

    public async Task<ActionExecutionResult> CloseGamingSessionAsync(CancellationToken ct = default)
    {
        if (!_platformInfo.IsWindows())
        {
            return ActionExecutionResult.NotAvailable("Fermeture session gaming", "Uniquement disponible sur Windows");
        }

        var policy = _policyProvider.GetPolicy();
        var normalizedWhitelist = policy.Whitelist.Select(ProcessNameHelper.Normalize).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var normalizedBlacklist = policy.Blacklist.Select(ProcessNameHelper.Normalize).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var normalizedGames = policy.Games.Select(ProcessNameHelper.Normalize).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var foregroundPid = _processProvider.TryGetForegroundProcessId();
        var candidates = new List<ProcessCandidate>();
        var ignored = new List<string>();
        string? foregroundGame = null;

        foreach (var process in _processProvider.EnumerateProcesses())
        {
            using var handle = process;
            ct.ThrowIfCancellationRequested();

            try
            {
                if (handle.HasExited)
                    continue;

                var normalizedName = ProcessNameHelper.Normalize(handle.ProcessName);
                if (string.IsNullOrWhiteSpace(normalizedName))
                {
                    ignored.Add("Processus sans nom exploitable");
                    continue;
                }

                if (foregroundPid.HasValue && handle.Id == foregroundPid.Value)
                {
                    if (normalizedGames.Contains(normalizedName))
                    {
                        foregroundGame = normalizedName;
                    }

                    ignored.Add($"{normalizedName}: fenêtre active, intouchable");
                    continue;
                }

                if (IsSystemProcess(handle))
                {
                    ignored.Add($"{normalizedName}: processus système");
                    continue;
                }

                if (!IsBackgroundProcess(handle, foregroundPid))
                {
                    ignored.Add($"{normalizedName}: pas en arrière-plan");
                    continue;
                }

                if (normalizedBlacklist.Contains(normalizedName) || policy.ProtectedTags.Any(tag => normalizedName.Contains(tag, StringComparison.OrdinalIgnoreCase)))
                {
                    ignored.Add($"{normalizedName}: protégé (blacklist/sécurité)");
                    continue;
                }

                if (!normalizedWhitelist.Contains(normalizedName))
                {
                    ignored.Add($"{normalizedName}: hors liste blanche");
                    continue;
                }

                candidates.Add(new ProcessCandidate(handle.Id, normalizedName));
            }
            catch
            {
                ignored.Add("Processus ignoré (inspection impossible)");
            }
        }

        if (candidates.Count == 0)
        {
            var nothingMessage = foregroundGame is null
                ? "Aucune app de fond candidate à fermer."
                : $"Jeu actif détecté ({foregroundGame}) : rien à toucher.";
            var ignoredDetails = string.Join("\n", ignored.Distinct());
            var unavailableSummary = string.IsNullOrWhiteSpace(ignoredDetails) ? nothingMessage : $"{nothingMessage}\n{ignoredDetails}";
            return ActionExecutionResult.NotAvailable("Fermeture session gaming", unavailableSummary);
        }

        var proposal = BuildProposal(candidates);
        var confirmed = await _sessionConfirmation.ConfirmAsync(proposal, ct).ConfigureAwait(false);
        if (!confirmed)
        {
            var ignoredSummary = string.Join("\n", ignored.Distinct());
            var declineSummary = string.IsNullOrWhiteSpace(ignoredSummary)
                ? "Aucune app fermée : l'utilisateur a dit non."
                : $"Aucune app fermée : l'utilisateur a dit non.\n{ignoredSummary}";
            return ActionExecutionResult.Failure("Fermeture session gaming", declineSummary);
        }

        var closed = new List<string>();
        var throttled = new List<string>();
        var blocked = new List<string>();

        foreach (var candidate in candidates)
        {
            ct.ThrowIfCancellationRequested();

            var actions = new List<string>();

            if (_processController.TryRequestClose(candidate.Id, out var closeNote))
            {
                actions.Add(closeNote ?? "fermeture demandée");
            }

            if (_processController.TrySuspend(candidate.Id, out var suspendNote))
            {
                actions.Add(suspendNote ?? "suspension légère");
            }

            if (_processController.TryLowerPriority(candidate.Id, out var priorityNote))
            {
                actions.Add(priorityNote ?? "priorité réduite");
                throttled.Add(candidate.Name);
            }

            if (actions.Count > 0)
            {
                closed.Add($"{candidate.Name} ({string.Join(", ", actions)})");
            }
            else
            {
                blocked.Add(candidate.Name);
            }
        }

        var summary = new StringBuilder();
        summary.Append("Session gaming nettoyée : ");
        summary.Append(closed.Count == 0 ? "rien à fermer" : string.Join(", ", closed));

        var details = new StringBuilder();
        if (throttled.Count > 0)
        {
            details.AppendLine($"Priorité abaissée : {string.Join(", ", throttled)}");
        }

        if (blocked.Count > 0)
        {
            details.AppendLine($"Ignorés (refus/lock) : {string.Join(", ", blocked)}");
        }

        if (ignored.Count > 0)
        {
            details.AppendLine("Ignorés (filtre) :");
            foreach (var reason in ignored.Distinct())
            {
                details.AppendLine($"- {reason}");
            }
        }

        details.Append("Pas de crash, pas de redémarrage. Tu peux retourner frag sans culpabilité.");

        var finalSummary = $"{summary}\n{details.ToString().TrimEnd()}";
        return ActionExecutionResult.Ok("Fermeture session gaming terminée", finalSummary);
    }

    public async Task<ActionExecutionResult> SoftRamFlushAsync(CancellationToken ct = default)
    {
        if (!_memoryReader.IsSupportedPlatform)
        {
            return ActionExecutionResult.NotAvailable("Libération RAM", "Uniquement supportée sur Windows");
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

        return ActionExecutionResult.Ok("Libération RAM terminée", $"{summary}\n{details}");
        }
        catch (OperationCanceledException)
        {
            return ActionExecutionResult.Failure("Libération RAM", "Libération RAM annulée");
        }
        catch (Exception ex)
        {
            return ActionExecutionResult.Failure("Libération RAM", $"Impossible : {ex.Message}");
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

    public record BackgroundProcessPolicy(
        IReadOnlySet<string> Whitelist,
        IReadOnlySet<string> Blacklist,
        IReadOnlySet<string> Games,
        IReadOnlySet<string> ProtectedTags);

    public interface IBackgroundProcessPolicyProvider
    {
        BackgroundProcessPolicy GetPolicy();
    }

    public interface ICloseSessionConfirmation
    {
        Task<bool> ConfirmAsync(string proposal, CancellationToken ct = default);
    }

    public interface IProcessController
    {
        bool TryRequestClose(int pid, out string? note);
        bool TrySuspend(int pid, out string? note);
        bool TryLowerPriority(int pid, out string? note);
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

    public sealed class WindowsMemoryReader : IMemoryReader
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

    private sealed class BackgroundProcessPolicyProvider : IBackgroundProcessPolicyProvider
    {
        public BackgroundProcessPolicy GetPolicy()
        {
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var path = Path.Combine(baseDir, "assets", "performance", "background-policy.json");
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var map = JsonSerializer.Deserialize<PolicyMap>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (map is not null)
                    {
                        return MapToPolicy(map);
                    }
                }
            }
            catch
            {
                // Fallback to defaults below.
            }

            return MapToPolicy(PolicyMap.Default());
        }

        private static BackgroundProcessPolicy MapToPolicy(PolicyMap map)
        {
            return new BackgroundProcessPolicy(
                map.Whitelist.Select(ProcessNameHelper.Normalize).Where(n => !string.IsNullOrWhiteSpace(n)).ToHashSet(StringComparer.OrdinalIgnoreCase),
                map.Blacklist.Select(ProcessNameHelper.Normalize).Where(n => !string.IsNullOrWhiteSpace(n)).ToHashSet(StringComparer.OrdinalIgnoreCase),
                map.Games.Select(ProcessNameHelper.Normalize).Where(n => !string.IsNullOrWhiteSpace(n)).ToHashSet(StringComparer.OrdinalIgnoreCase),
                map.ProtectedTags.Select(t => t.Trim().ToLowerInvariant()).Where(t => !string.IsNullOrWhiteSpace(t)).ToHashSet(StringComparer.OrdinalIgnoreCase));
        }

        private sealed class PolicyMap
        {
            public string[] Games { get; set; } = Array.Empty<string>();
            public string[] Whitelist { get; set; } = Array.Empty<string>();
            public string[] Blacklist { get; set; } = Array.Empty<string>();
            public string[] ProtectedTags { get; set; } = Array.Empty<string>();

            public static PolicyMap Default() => new()
            {
                Games = new[] { "eldenring.exe", "valorant.exe", "fortniteclient-win64-shipping.exe", "cs2.exe", "wow.exe", "lol.launcher.exe" },
                Whitelist = new[] { "discord.exe", "steamwebhelper.exe", "epicgameslauncher.exe", "battle.net.exe", "origin.exe", "uplay.exe", "goggalaxy.exe", "chrome.exe", "msedge.exe", "firefox.exe", "opera.exe", "brave.exe", "teamspeak.exe", "telegram.exe", "spotify.exe" },
                Blacklist = new[] { "system", "idle", "svchost.exe", "lsass.exe", "csrss.exe", "wininit.exe", "services.exe", "fontdrvhost.exe", "dwm.exe", "securityhealthservice.exe", "audiodg.exe", "nvcontainer.exe" },
                ProtectedTags = new[] { "driver", "audio", "network", "antivirus", "input", "service", "windows" }
            };
        }
    }

    private sealed class AlwaysConfirmSession : ICloseSessionConfirmation
    {
        public Task<bool> ConfirmAsync(string proposal, CancellationToken ct = default) => Task.FromResult(true);
    }

    private sealed class WindowsProcessController : IProcessController
    {
        public bool TryRequestClose(int pid, out string? note)
        {
            note = null;
            try
            {
                using var process = Process.GetProcessById(pid);
                if (process.HasExited)
                    return false;

                if (process.MainWindowHandle != IntPtr.Zero && process.CloseMainWindow())
                {
                    note = "fermeture demandée";
                    return true;
                }
            }
            catch
            {
                // Ignore errors, best effort only.
            }

            return false;
        }

        public bool TrySuspend(int pid, out string? note)
        {
            note = "Suspension non supportée (sans droits admin)";
            return false;
        }

        public bool TryLowerPriority(int pid, out string? note)
        {
            note = null;
            try
            {
                using var process = Process.GetProcessById(pid);
                if (process.HasExited)
                    return false;

                process.PriorityClass = ProcessPriorityClass.BelowNormal;
                note = "priorité réduite";
                return true;
            }
            catch
            {
                return false;
            }
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

    private sealed record ProcessCandidate(int Id, string Name);

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

    private static string BuildProposal(IEnumerable<ProcessCandidate> candidates)
    {
        var names = candidates.Select(c => c.Name).Distinct().ToList();
        return names.Count == 0
            ? "Aucun candidat à fermer"
            : $"Candidats à calmer/fermer : {string.Join(", ", names)}. On tente ?";
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
