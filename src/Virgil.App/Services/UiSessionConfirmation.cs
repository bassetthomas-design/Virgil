using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Virgil.App.Interfaces;
using Virgil.Services;

namespace Virgil.App.Services;

public sealed class UiSessionConfirmation : PerformanceService.ICloseSessionConfirmation
{
    private readonly IConfirmationService _confirmation;

    public UiSessionConfirmation(IConfirmationService confirmation)
    {
        _confirmation = confirmation;
    }

    public Task<bool> ConfirmAsync(string proposal, CancellationToken ct = default)
    {
        var ok = _confirmation.Confirm(proposal, "Virgil — Session gaming", MessageBoxImage.Warning);
        return Task.FromResult(ok);
    }
}
