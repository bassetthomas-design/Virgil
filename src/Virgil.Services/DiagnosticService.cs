using System.Threading;
using System.Threading.Tasks;
using Virgil.Services.Abstractions;

namespace Virgil.Services;

/// <summary>
/// Stub de IDiagnosticService – scan express, vérifs disque et intégrité seront branchés ensuite.
/// </summary>
public sealed class DiagnosticService : IDiagnosticService
{
    public Task<ActionExecutionResult> RunExpressAsync(CancellationToken ct = default)
        => Task.FromResult(ActionExecutionResult.NotAvailable("Scan express non implémenté"));

    public Task<ActionExecutionResult> DiskCheckAsync(CancellationToken ct = default)
        => Task.FromResult(ActionExecutionResult.NotAvailable("Vérification disque non implémentée"));

    public Task<ActionExecutionResult> SystemIntegrityCheckAsync(CancellationToken ct = default)
        => Task.FromResult(ActionExecutionResult.NotAvailable("Vérification intégrité système non implémentée"));

    public Task<ActionExecutionResult> RescanSystemAsync(CancellationToken ct = default)
        => Task.FromResult(ActionExecutionResult.NotAvailable("Re-scan système non implémenté"));
}
