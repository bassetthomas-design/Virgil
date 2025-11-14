using System.Threading;
using System.Threading.Tasks;
using Virgil.Services.Abstractions;

namespace Virgil.Services;

/// <summary>
/// Stub de IPerformanceService – gestion des profils de performance / gaming plus tard.
/// </summary>
public sealed class PerformanceService : IPerformanceService
{
    public Task EnableGamingModeAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task RestoreNormalModeAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task AnalyzeStartupAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task CloseGamingSessionAsync(CancellationToken ct = default) => Task.CompletedTask;
}
