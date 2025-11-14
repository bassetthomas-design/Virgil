using System.Threading;
using System.Threading.Tasks;
using Virgil.Services.Abstractions;

namespace Virgil.Services;

/// <summary>
/// Stub de IUpdateService – à remplacer par la vraie logique (winget, Windows Update, etc.).
/// </summary>
public sealed class UpdateService : IUpdateService
{
    public Task UpdateAppsAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task RunWindowsUpdateAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task CheckGpuDriversAsync(CancellationToken ct = default) => Task.CompletedTask;
}
