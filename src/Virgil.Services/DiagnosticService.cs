using System.Threading;
using System.Threading.Tasks;
using Virgil.Services.Abstractions;

namespace Virgil.Services;

/// <summary>
/// Stub de IDiagnosticService – scan express, vérifs disque et intégrité seront branchés ensuite.
/// </summary>
public sealed class DiagnosticService : IDiagnosticService
{
    public Task RunExpressAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task DiskCheckAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task SystemIntegrityCheckAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task RescanSystemAsync(CancellationToken ct = default) => Task.CompletedTask;
}
