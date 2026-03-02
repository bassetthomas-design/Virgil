using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Virgil.Services.Abstractions;

namespace Virgil.Services.SelfTest;

/// <summary>
/// Collection of stubbed services used to simulate the action pipeline without
/// touching the real system. Each service records invocation counts and
/// guarantees that no side effects are produced.
/// </summary>
public sealed class DryRunServiceProbes
{
    public DryRunServiceProbes()
    {
        Cleanup = new DryRunCleanupService();
        Update = new DryRunUpdateService();
        Network = new DryRunNetworkService();
        Performance = new DryRunPerformanceService();
        Diagnostic = new DryRunDiagnosticService();
        Special = new DryRunSpecialService();
        Chat = new DryRunChatService();
    }

    public DryRunCleanupService Cleanup { get; }
    public DryRunUpdateService Update { get; }
    public DryRunNetworkService Network { get; }
    public DryRunPerformanceService Performance { get; }
    public DryRunDiagnosticService Diagnostic { get; }
    public DryRunSpecialService Special { get; }
    public DryRunChatService Chat { get; }

    public bool HasSideEffects => Cleanup.SideEffectsAttempted
        || Update.SideEffectsAttempted
        || Network.SideEffectsAttempted
        || Performance.SideEffectsAttempted
        || Diagnostic.SideEffectsAttempted
        || Special.SideEffectsAttempted;

    public int TotalInvocations => Cleanup.InvocationCount
        + Update.InvocationCount
        + Network.InvocationCount
        + Performance.InvocationCount
        + Diagnostic.InvocationCount
        + Special.InvocationCount;

    private static ActionExecutionResult Success(string label) => ActionExecutionResult.Ok($"Dry-run: {label}", "DryRun=true");

    public sealed class DryRunCleanupService : ICleanupService
    {
        public int InvocationCount { get; private set; }
        public bool SideEffectsAttempted { get; private set; }

        public Task<ActionExecutionResult> RunSimpleAsync(CancellationToken ct = default)
            => CompleteAsync("Nettoyage rapide (dry-run)");

        public Task<ActionExecutionResult> RunAdvancedAsync(CancellationToken ct = default)
            => CompleteAsync("Nettoyage disque avancé (dry-run)");

        public Task<ActionExecutionResult> RunSystemTempCleanupAsync(CancellationToken ct = default)
            => CompleteAsync("Nettoyage temporaires système (dry-run)");

        public Task<ActionExecutionResult> RunBrowserLightAsync(CancellationToken ct = default)
            => CompleteAsync("Nettoyage navigateur léger (dry-run)");

        public Task<ActionExecutionResult> RunBrowserDeepAsync(CancellationToken ct = default)
            => CompleteAsync("Nettoyage navigateur profond (dry-run)");

        private Task<ActionExecutionResult> CompleteAsync(string label)
        {
            InvocationCount++;
            return Task.FromResult(Success(label));
        }
    }

    public sealed class DryRunUpdateService : IUpdateService
    {
        public int InvocationCount { get; private set; }
        public bool SideEffectsAttempted { get; private set; }

        public Task<ActionExecutionResult> ManageAutomaticUpdatesAsync(AutoUpdateUserIntent? intent = null, CancellationToken ct = default)
            => CompleteAsync("Gestion mises à jour automatiques (dry-run)");

        public Task<ActionExecutionResult> UpdateAppsAsync(CancellationToken ct = default)
            => CompleteAsync("Mise à jour applications (dry-run)");

        public Task<ActionExecutionResult> RunWindowsUpdateAsync(CancellationToken ct = default)
            => CompleteAsync("Windows Update (dry-run)");

        public Task<ActionExecutionResult> ScanDriversAsync(CancellationToken ct = default)
            => CompleteAsync("Vérification drivers (dry-run)");

        public Task<ActionExecutionResult> InstallDriversAsync(CancellationToken ct = default)
            => CompleteAsync("Installation drivers (dry-run)");

        private Task<ActionExecutionResult> CompleteAsync(string label)
        {
            InvocationCount++;
            return Task.FromResult(Success(label));
        }
    }

    public sealed class DryRunNetworkService : INetworkService
    {
        public int InvocationCount { get; private set; }
        public bool SideEffectsAttempted { get; private set; }

        public Task<ActionExecutionResult> RunQuickDiagnosticAsync(CancellationToken ct = default)
            => CompleteAsync("Diagnostic réseau (dry-run)");

        public Task<ActionExecutionResult> SoftResetAsync(CancellationToken ct = default)
            => CompleteAsync("Reset réseau soft (dry-run)");

        public Task<ActionExecutionResult> AdvancedResetAsync(CancellationToken ct = default)
            => CompleteAsync("Reset réseau complet (dry-run)");

        public Task<ActionExecutionResult> RunInternetSpeedTestAsync(CancellationToken ct = default)
            => CompleteAsync("Test débit Internet (dry-run)");

        public Task<ActionExecutionResult> RunLatencyTestAsync(CancellationToken ct = default)
            => CompleteAsync("Test latence (dry-run)");

        private Task<ActionExecutionResult> CompleteAsync(string label)
        {
            InvocationCount++;
            return Task.FromResult(Success(label));
        }
    }

    public sealed class DryRunPerformanceService : IPerformanceService
    {
        public int InvocationCount { get; private set; }
        public bool SideEffectsAttempted { get; private set; }

        public Task<ActionExecutionResult> EnableGamingModeAsync(CancellationToken ct = default)
            => CompleteAsync("Mode performance on (dry-run)");

        public Task<ActionExecutionResult> RestoreNormalModeAsync(CancellationToken ct = default)
            => CompleteAsync("Mode performance off (dry-run)");

        public Task<ActionExecutionResult> AnalyzeStartupAsync(CancellationToken ct = default)
            => CompleteAsync("Analyse démarrage (dry-run)");

        public Task<ActionExecutionResult> OptimizeStartupAsync(CancellationToken ct = default)
            => CompleteAsync("Optimisation démarrage (dry-run)");

        public Task<ActionExecutionResult> RestoreStartupAsync(CancellationToken ct = default)
            => CompleteAsync("Restauration démarrage (dry-run)");

        public Task<ActionExecutionResult> CloseGamingSessionAsync(CancellationToken ct = default)
            => CompleteAsync("Fermeture session gaming (dry-run)");

        public Task<ActionExecutionResult> SoftRamFlushAsync(CancellationToken ct = default)
            => CompleteAsync("Libération RAM (dry-run)");

        private Task<ActionExecutionResult> CompleteAsync(string label)
        {
            InvocationCount++;
            return Task.FromResult(Success(label));
        }
    }

    public sealed class DryRunDiagnosticService : IDiagnosticService
    {
        public int InvocationCount { get; private set; }
        public bool SideEffectsAttempted { get; private set; }

        public Task<ActionExecutionResult> RunExpressAsync(CancellationToken ct = default)
            => CompleteAsync("Scan express (dry-run)");

        public Task<ActionExecutionResult> RunHardwareQuickCheckAsync(CancellationToken ct = default)
            => CompleteAsync("Diagnostic matériel (dry-run)");

        public Task<ActionExecutionResult> DiskCheckAsync(CancellationToken ct = default)
            => CompleteAsync("Vérification disque (dry-run)");

        public Task<ActionExecutionResult> SystemIntegrityCheckAsync(CancellationToken ct = default)
            => CompleteAsync("Vérification intégrité système (dry-run)");

        public Task<ActionExecutionResult> RescanSystemAsync(CancellationToken ct = default)
            => CompleteAsync("Re-scan système (dry-run)");

        private Task<ActionExecutionResult> CompleteAsync(string label)
        {
            InvocationCount++;
            return Task.FromResult(Success(label));
        }
    }

    public sealed class DryRunSpecialService : ISpecialService
    {
        public int InvocationCount { get; private set; }
        public bool SideEffectsAttempted { get; private set; }

        public Task<ActionExecutionResult> RamboModeAsync(CancellationToken ct = default)
            => CompleteAsync("Mode RAMBO (dry-run)");

        public Task<ActionExecutionResult> ReloadConfigurationAsync(CancellationToken ct = default)
            => CompleteAsync("Reload config (dry-run)");

        private Task<ActionExecutionResult> CompleteAsync(string label)
        {
            InvocationCount++;
            return Task.FromResult(Success(label));
        }
    }

    public sealed class DryRunChatService : IChatService
    {
        private readonly List<string> _messages = new();

        public IReadOnlyCollection<string> Messages => _messages;

        public Task InfoAsync(string message, CancellationToken ct = default)
        {
            _messages.Add(message);
            return Task.CompletedTask;
        }

        public Task WarnAsync(string message, CancellationToken ct = default)
        {
            _messages.Add(message);
            return Task.CompletedTask;
        }

        public Task ErrorAsync(string message, CancellationToken ct = default)
        {
            _messages.Add(message);
            return Task.CompletedTask;
        }

        public Task ThanosWipeAsync(bool preservePinned = true, CancellationToken ct = default)
        {
            _messages.Add("Dry-run: ThanosWipe");
            return Task.CompletedTask;
        }
    }
}

/// <summary>
/// Wrapper that exposes a dry-run orchestrator alongside its probe services.
/// </summary>
public sealed record DryRunOrchestratorBundle(IActionOrchestrator Orchestrator, DryRunServiceProbes Probes)
{
    public static DryRunOrchestratorBundle Create()
    {
        var probes = new DryRunServiceProbes();
        var orchestrator = new ActionOrchestrator(
            probes.Cleanup,
            probes.Update,
            probes.Network,
            probes.Performance,
            probes.Diagnostic,
            probes.Special,
            probes.Chat);

        return new DryRunOrchestratorBundle(orchestrator, probes);
    }
}
