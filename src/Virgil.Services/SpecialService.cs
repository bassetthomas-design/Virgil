using System.Threading;
using System.Threading.Tasks;
using Virgil.Services.Abstractions;

namespace Virgil.Services;

/// <summary>
/// Stub de ISpecialService – Rambo mode, reload config, etc. viendront ensuite.
/// </summary>
public sealed class SpecialService : ISpecialService
{
    public Task RamboModeAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task ReloadConfigurationAsync(CancellationToken ct = default) => Task.CompletedTask;
}
