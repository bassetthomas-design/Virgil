using System.Threading;
using System.Threading.Tasks;
using Virgil.Services.Abstractions;

namespace Virgil.Services;

/// <summary>
/// Stub de ICleanupService.
/// Pour l’instant, les méthodes sont vides et servent uniquement à valider le câblage.
/// </summary>
public sealed class CleanupService : ICleanupService
{
    public Task RunSimpleAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task RunAdvancedAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task RunBrowserLightAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task RunBrowserDeepAsync(CancellationToken ct = default) => Task.CompletedTask;
}
