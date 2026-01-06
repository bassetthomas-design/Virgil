using System.Threading;
using System.Threading.Tasks;
using Virgil.Services;

namespace Virgil.App.Services
{
    public sealed class UiConfirmationPrompt : IConfirmationPrompt
    {
        private readonly IConfirmationService _confirmation;

        public UiConfirmationPrompt(IConfirmationService confirmation)
        {
            _confirmation = confirmation;
        }

        public Task<bool> ConfirmAsync(string message, CancellationToken ct = default)
        {
            var confirmed = _confirmation.Confirm(message, "Confirmation", System.Windows.MessageBoxImage.Warning);
            return Task.FromResult(confirmed);
        }
    }
}
