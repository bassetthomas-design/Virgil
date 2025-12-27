using System.Threading;
using System.Threading.Tasks;
using Virgil.Services.Abstractions;

namespace Virgil.Services;

/// <summary>
/// Stub de ISpecialService – Rambo mode, reload config, etc. viendront ensuite.
/// </summary>
public sealed class SpecialService : ISpecialService
{
    public Task<ActionExecutionResult> RamboModeAsync(CancellationToken ct = default)
        => Task.FromResult(ActionExecutionResult.NotAvailable("Mode RAMBO non implémenté"));

    public Task<ActionExecutionResult> ReloadConfigurationAsync(CancellationToken ct = default)
        => Task.FromResult(ActionExecutionResult.NotAvailable("Rechargement configuration non implémenté"));
}
