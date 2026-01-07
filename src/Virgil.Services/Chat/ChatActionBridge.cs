using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Virgil.Domain.Actions;
using Virgil.Services.Abstractions;
using Virgil.Services;

namespace Virgil.Services.Chat;

public interface IConfirmationProvider
{
    Task<bool> ConfirmAsync(string actionId, CancellationToken ct = default);
}

public sealed class ChatActionBridge
{
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
        if (!ActionCatalog.TryGet(actionId, out var descriptor))
        {
            await _chat.WarnAsync($"Commande inconnue: {actionId}.", ct);
            return;
        }

        if (descriptor.IsDestructive)
        {
            var confirmed = await _confirmation.ConfirmAsync(actionId, ct);
            if (!confirmed)
            {
                await _chat.WarnAsync($"Action {actionId} annulée après confirmation.", ct);
                return;
            }
        }

        try
        {
            if (!descriptor.IsImplemented)
            {
                var unavailable = ActionExecutionResult.NotImplemented(descriptor.DisplayName, $"Service: {descriptor.Service}");
                await PostFormattedAsync(unavailable, ct);
                return;
            }

            var resultExec = await _actions.RunAsync(descriptor.VirgilActionId, ct);
            await PostFormattedAsync(resultExec, ct);
        }
        catch (Exception ex)
        {
            await _chat.ErrorAsync($"Echec de l'action {actionId}: {ex.Message}", ct);
        }
    }

    private async Task PostFormattedAsync(ActionExecutionResult result, CancellationToken ct)
    {
        var formatter = new ActionResultToChatFormatter();
        var formatted = formatter.Format(result);
        switch (formatted.Severity)
        {
            case ChatSeverity.Error:
                await _chat.ErrorAsync(formatted.PrimaryMessage, ct);
                break;
            case ChatSeverity.Warning:
                await _chat.WarnAsync(formatted.PrimaryMessage, ct);
                break;
            default:
                await _chat.InfoAsync(formatted.PrimaryMessage, ct);
                break;
        }

        if (!string.IsNullOrWhiteSpace(formatted.Details))
        {
            await _chat.InfoAsync(formatted.Details, ct);
        }
    }

    private sealed class AlwaysConfirmProvider : IConfirmationProvider
    {
        public Task<bool> ConfirmAsync(string actionId, CancellationToken ct = default) => Task.FromResult(true);
    }
}
