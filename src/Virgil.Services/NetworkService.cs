using System.Threading;
using System.Threading.Tasks;
using Virgil.Services.Abstractions;

namespace Virgil.Services;

/// <summary>
/// Stub de INetworkService – la logique réseau réelle viendra ensuite.
/// </summary>
public sealed class NetworkService : INetworkService
{
    public Task<ActionExecutionResult> RunQuickDiagnosticAsync(CancellationToken ct = default)
        => Task.FromResult(ActionExecutionResult.NotAvailable("Diagnostic réseau non implémenté"));

    public Task<ActionExecutionResult> SoftResetAsync(CancellationToken ct = default)
        => Task.FromResult(ActionExecutionResult.NotAvailable("Reset réseau (soft) non implémenté"));

    public Task<ActionExecutionResult> AdvancedResetAsync(CancellationToken ct = default)
        => Task.FromResult(ActionExecutionResult.NotAvailable("Reset réseau avancé non implémenté"));

    public Task<ActionExecutionResult> RunLatencyTestAsync(CancellationToken ct = default)
        => Task.FromResult(ActionExecutionResult.NotAvailable("Test de latence non implémenté"));
}
