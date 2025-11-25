using System.Threading;
using System.Threading.Tasks;
using Virgil.Services.Abstractions;

namespace Virgil.Services;

/// <summary>
/// Stub de INetworkService – la logique réseau réelle viendra ensuite.
/// </summary>
public sealed class NetworkService : INetworkService
{
    public Task RunQuickDiagnosticAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task SoftResetAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task AdvancedResetAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task RunLatencyTestAsync(CancellationToken ct = default) => Task.CompletedTask;
}
