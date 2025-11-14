using System.Threading;
using System.Threading.Tasks;
using Virgil.Domain.Actions;

namespace Virgil.Services.Abstractions;

public interface IActionOrchestrator
{
    Task RunAsync(VirgilActionId actionId, CancellationToken cancellationToken = default);
}

public interface ICleanupService
{
    Task RunSimpleAsync(CancellationToken ct = default);   // Nettoyage rapide
    Task RunAdvancedAsync(CancellationToken ct = default); // Nettoyage disque avancé
    Task RunBrowserLightAsync(CancellationToken ct = default);
    Task RunBrowserDeepAsync(CancellationToken ct = default);
}

public interface IUpdateService
{
    Task UpdateAppsAsync(CancellationToken ct = default);
    Task RunWindowsUpdateAsync(CancellationToken ct = default);
    Task CheckGpuDriversAsync(CancellationToken ct = default);
}

public interface INetworkService
{
    Task RunQuickDiagnosticAsync(CancellationToken ct = default);
    Task SoftResetAsync(CancellationToken ct = default);
    Task AdvancedResetAsync(CancellationToken ct = default);
    Task RunLatencyTestAsync(CancellationToken ct = default);
}

public interface IPerformanceService
{
    Task EnableGamingModeAsync(CancellationToken ct = default);
    Task RestoreNormalModeAsync(CancellationToken ct = default);
    Task AnalyzeStartupAsync(CancellationToken ct = default);
    Task CloseGamingSessionAsync(CancellationToken ct = default);
}

public interface IDiagnosticService
{
    Task RunExpressAsync(CancellationToken ct = default);
    Task DiskCheckAsync(CancellationToken ct = default);
    Task SystemIntegrityCheckAsync(CancellationToken ct = default);
    Task RescanSystemAsync(CancellationToken ct = default);
}

public interface ISpecialService
{
    Task RamboModeAsync(CancellationToken ct = default);
    Task ReloadConfigurationAsync(CancellationToken ct = default);
}

public interface IChatService
{
    Task InfoAsync(string message, CancellationToken ct = default);
    Task WarnAsync(string message, CancellationToken ct = default);
    Task ErrorAsync(string message, CancellationToken ct = default);

    Task ThanosWipeAsync(bool preservePinned = true, CancellationToken ct = default);
}
