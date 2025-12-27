using System.Threading;
using System.Threading.Tasks;
using Virgil.Services.Abstractions;

namespace Virgil.Services;

/// <summary>
/// Stub de IPerformanceService – gestion des profils de performance / gaming plus tard.
/// </summary>
public sealed class PerformanceService : IPerformanceService
{
    public Task<ActionExecutionResult> EnableGamingModeAsync(CancellationToken ct = default)
        => Task.FromResult(ActionExecutionResult.NotAvailable("Mode performance non disponible"));

    public Task<ActionExecutionResult> RestoreNormalModeAsync(CancellationToken ct = default)
        => Task.FromResult(ActionExecutionResult.NotAvailable("Retour au mode normal non disponible"));

    public Task<ActionExecutionResult> AnalyzeStartupAsync(CancellationToken ct = default)
        => Task.FromResult(ActionExecutionResult.NotAvailable("Analyse démarrage non implémentée"));

    public Task<ActionExecutionResult> CloseGamingSessionAsync(CancellationToken ct = default)
        => Task.FromResult(ActionExecutionResult.NotAvailable("Fermeture session gaming non implémentée"));
}
