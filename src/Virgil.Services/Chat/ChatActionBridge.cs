using Virgil.Domain.Actions;
using Virgil.Services.Abstractions;

namespace Virgil.Services.Chat;

public interface IConfirmationProvider
{
    Task<bool> ConfirmAsync(string actionId, CancellationToken ct = default);
}

public sealed class ChatActionBridge
{
    private static readonly HashSet<string> _whitelist = new(StringComparer.OrdinalIgnoreCase)
    {
        "status",
        "monitor_toggle",
        "monitor_rescan",
        "clean_quick",
        "clean_browsers",
        "maintenance_full",
        "open_settings",
        "show_hud",
        "hide_hud",
    };

    private readonly IActionOrchestrator _actions;
    private readonly IChatService _chat;
    private readonly IConfirmationProvider _confirmation;

    public ChatActionBridge(IActionOrchestrator actions, IChatService chat, IConfirmationProvider? confirmation = null)
    {
        _actions = actions;
        _chat = chat;
        _confirmation = confirmation ?? new AlwaysConfirmProvider();
    }

    public async Task RouteAsync(ChatEngineResult result, CancellationToken ct = default)
    {
        await _chat.InfoAsync(result.Text, ct);

        if (result.Command.Type != ChatCommandType.Action || string.IsNullOrWhiteSpace(result.Command.Action))
        {
            return;
        }

        var actionId = result.Command.Action.Trim();
        if (!_whitelist.Contains(actionId))
        {
            await _chat.WarnAsync($"Commande refusée (hors whitelist): {actionId}.", ct);
            return;
        }

        if (RequiresConfirmation(actionId))
        {
            var confirmed = await _confirmation.ConfirmAsync(actionId, ct);
            if (!confirmed)
            {
                await _chat.WarnAsync($"Action {actionId} annulée après confirmation.", ct);
                return;
            }
        }

        if (!TryMapAction(actionId, out var virgilAction))
        {
            await _chat.WarnAsync($"Commande reconnue mais non mappée: {actionId}.", ct);
            return;
        }

        try
        {
            await _actions.RunAsync(virgilAction, ct);
            await _chat.InfoAsync($"Action {actionId} exécutée.", ct);
        }
        catch (Exception ex)
        {
            await _chat.ErrorAsync($"Echec de l'action {actionId}: {ex.Message}", ct);
        }
    }

    private static bool RequiresConfirmation(string actionId)
    {
        return actionId.StartsWith("clean_", StringComparison.OrdinalIgnoreCase)
            || string.Equals(actionId, "maintenance_full", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryMapAction(string actionId, out VirgilActionId mapped)
    {
        switch (actionId.ToLowerInvariant())
        {
            case "clean_quick":
                mapped = VirgilActionId.QuickClean;
                return true;
            case "clean_browsers":
                mapped = VirgilActionId.LightBrowserClean;
                return true;
            case "maintenance_full":
                mapped = VirgilActionId.AdvancedDiskClean;
                return true;
            case "status":
                mapped = VirgilActionId.ScanSystemExpress;
                return true;
            case "monitor_rescan":
                mapped = VirgilActionId.RescanSystem;
                return true;
            case "monitor_toggle":
                mapped = VirgilActionId.RamboMode;
                return true;
            case "open_settings":
                mapped = VirgilActionId.ReloadConfiguration;
                return true;
            default:
                mapped = default;
                return false;
        }
    }

    private sealed class AlwaysConfirmProvider : IConfirmationProvider
    {
        public Task<bool> ConfirmAsync(string actionId, CancellationToken ct = default) => Task.FromResult(true);
    }
}
