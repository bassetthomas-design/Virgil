using System.Threading;
using System.Threading.Tasks;
using Virgil.Domain.Actions;

namespace Virgil.Services.Abstractions;

public interface IActionOrchestrator
{
    Task<ActionExecutionResult> RunAsync(VirgilActionId actionId, CancellationToken cancellationToken = default);
}

public interface ICleanupService
{
    Task<ActionExecutionResult> RunSimpleAsync(CancellationToken ct = default);   // Nettoyage rapide
    Task<ActionExecutionResult> RunAdvancedAsync(CancellationToken ct = default); // Nettoyage disque avancé
    Task<ActionExecutionResult> RunBrowserLightAsync(CancellationToken ct = default);
    Task<ActionExecutionResult> RunBrowserDeepAsync(CancellationToken ct = default);
}

public interface IUpdateService
{
    Task<ActionExecutionResult> UpdateAppsAsync(CancellationToken ct = default);
    Task<ActionExecutionResult> RunWindowsUpdateAsync(CancellationToken ct = default);
    Task<ActionExecutionResult> CheckGpuDriversAsync(CancellationToken ct = default);
}

public interface INetworkService
{
    Task<ActionExecutionResult> RunQuickDiagnosticAsync(CancellationToken ct = default);
    Task<ActionExecutionResult> SoftResetAsync(CancellationToken ct = default);
    Task<ActionExecutionResult> AdvancedResetAsync(CancellationToken ct = default);
    Task<ActionExecutionResult> RunLatencyTestAsync(CancellationToken ct = default);
}

public interface IPerformanceService
{
    Task<ActionExecutionResult> EnableGamingModeAsync(CancellationToken ct = default);
    Task<ActionExecutionResult> RestoreNormalModeAsync(CancellationToken ct = default);
    Task<ActionExecutionResult> AnalyzeStartupAsync(CancellationToken ct = default);
    Task<ActionExecutionResult> CloseGamingSessionAsync(CancellationToken ct = default);
    Task<ActionExecutionResult> SoftRamFlushAsync(CancellationToken ct = default);
}

public interface IDiagnosticService
{
    Task<ActionExecutionResult> RunExpressAsync(CancellationToken ct = default);
    Task<ActionExecutionResult> DiskCheckAsync(CancellationToken ct = default);
    Task<ActionExecutionResult> SystemIntegrityCheckAsync(CancellationToken ct = default);
    Task<ActionExecutionResult> RescanSystemAsync(CancellationToken ct = default);
}

public interface ISpecialService
{
    Task<ActionExecutionResult> RamboModeAsync(CancellationToken ct = default);
    Task<ActionExecutionResult> ReloadConfigurationAsync(CancellationToken ct = default);
}

public interface IChatService
{
    Task InfoAsync(string message, CancellationToken ct = default);
    Task WarnAsync(string message, CancellationToken ct = default);
    Task ErrorAsync(string message, CancellationToken ct = default);

    Task ThanosWipeAsync(bool preservePinned = true, CancellationToken ct = default);
}
