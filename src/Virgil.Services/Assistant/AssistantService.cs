using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Virgil.Services.Assistant;

public sealed class AssistantService : IAssistantService
{
    private readonly IAssistantProvider _provider;

    public AssistantService(IAssistantProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public async Task<AssistantReply> AskAsync(string userMessage, AssistantContext ctx, CancellationToken ct = default)
    {
        var reply = await _provider.AskAsync(userMessage, ctx, ct).ConfigureAwait(false);
        var validated = ValidateReply(reply, ctx, userMessage);
        ConversationMemoryStore.UpdateSessionSummary(userMessage, validated.Text);
        return validated;
    }

    private static AssistantReply ValidateReply(AssistantReply reply, AssistantContext ctx, string userMessage)
    {
        if (reply is null)
        {
            return AssistantReply.Empty;
        }

        var routed = IntentRouter.SuggestActions(userMessage, ctx.ActionCatalog);
        var combined = (reply.ProposedActions ?? Array.Empty<ProposedAction>())
            .Concat(routed)
            .ToList();
        var allowedIds = new HashSet<string>(ctx.ActionCatalog.Select(item => item.Id), StringComparer.OrdinalIgnoreCase);
        var validated = combined
            .Where(action => action is not null && allowedIds.Contains(action.ActionId))
            .GroupBy(action => action.ActionId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Select(action => action with
            {
                RequiresConfirmation = true,
                Parameters = action.Parameters is null
                    ? null
                    : new Dictionary<string, string>(action.Parameters, StringComparer.OrdinalIgnoreCase)
            })
            .Take(3)
            .ToList();

        return new AssistantReply(reply.Text ?? string.Empty, validated);
    }
}
