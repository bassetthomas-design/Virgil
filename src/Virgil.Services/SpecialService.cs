using System;
using System.Threading;
using System.Threading.Tasks;
using Virgil.Services.Abstractions;

namespace Virgil.Services;

/// <summary>
/// Stub de ISpecialService – Rambo mode, reload config, etc. viendront ensuite.
/// </summary>
public sealed class SpecialService : ISpecialService
{
    private readonly IConfigurationReloader _reloader;
    private readonly IConfirmationPrompt _confirmation;
    private readonly IChatService _chat;

    public SpecialService(
        IConfigurationReloader reloader,
        IConfirmationPrompt confirmation,
        IChatService chat)
    {
        _reloader = reloader ?? throw new ArgumentNullException(nameof(reloader));
        _confirmation = confirmation ?? throw new ArgumentNullException(nameof(confirmation));
        _chat = chat ?? throw new ArgumentNullException(nameof(chat));
    }

    public Task<ActionExecutionResult> RamboModeAsync(CancellationToken ct = default)
        => Task.FromResult(ActionExecutionResult.NotAvailable("Mode RAMBO", "Service SpecialService indisponible"));

    public async Task<ActionExecutionResult> ReloadConfigurationAsync(CancellationToken ct = default)
    {
        var confirmed = await _confirmation.ConfirmAsync("les changements seront appliqués", ct).ConfigureAwait(false);
        if (!confirmed)
        {
            const string cancelled = "Résultat: Échec — rien rechargé, votre veto est respecté.";
            await _chat.InfoAsync(cancelled, ct).ConfigureAwait(false);
            return ActionExecutionResult.Failure(cancelled);
        }

        try
        {
            var outcome = await _reloader.ReloadAsync(ct).ConfigureAwait(false);
            var status = outcome.GetOverallStatus();
            var message = status switch
            {
                ConfigurationReloadStatus.Ok => "Résultat: OK — configuration rafraîchie sans drama.",
                ConfigurationReloadStatus.Partial => "Résultat: Partiel — ça a tiqué mais on s'en contente.",
                _ => "Résultat: Échec — personne n'aime tout refaire pour rien.",
            };

            await _chat.InfoAsync(message, ct).ConfigureAwait(false);
            return status switch
            {
                ConfigurationReloadStatus.Ok => ActionExecutionResult.Ok("Configuration rechargée", message),
                ConfigurationReloadStatus.Partial => ActionExecutionResult.Partial("Configuration rechargée", message),
                _ => ActionExecutionResult.Failure("Configuration rechargée", message),
            };
        }
        catch (Exception ex)
        {
            var message = $"Résultat: Échec — {ex.Message}";
            await _chat.WarnAsync(message, ct).ConfigureAwait(false);
            return ActionExecutionResult.Failure(message);
        }
    }
}
