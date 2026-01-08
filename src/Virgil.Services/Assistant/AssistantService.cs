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
        return ValidateReply(reply, ctx);
    }

    private static AssistantReply ValidateReply(AssistantReply reply, AssistantContext ctx)
    {
        if (reply is null)
        {
            return AssistantReply.Empty;
        }

        var allowedIds = new HashSet<string>(ctx.ActionCatalog.Select(item => item.Id), StringComparer.OrdinalIgnoreCase);
        var proposed = reply.ProposedActions ?? Array.Empty<ProposedAction>();

        var validated = proposed
            .Where(action => action is not null && allowedIds.Contains(action.ActionId))
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
