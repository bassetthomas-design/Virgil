using System.Threading;
using System.Threading.Tasks;
using Virgil.App.Interfaces;
using Virgil.Services.Chat;

namespace Virgil.App.Services;

public sealed class UiConfirmationProvider : IConfirmationProvider
{
    private readonly IConfirmationService _confirmation;

    public UiConfirmationProvider(IConfirmationService confirmation)
    {
        _confirmation = confirmation;
    }

    public Task<bool> ConfirmAsync(string actionId, CancellationToken ct = default)
    {
        var message = $"Confirmer l'action {actionId} ?";
        var ok = _confirmation.Confirm(message);
        return Task.FromResult(ok);
    }
}
