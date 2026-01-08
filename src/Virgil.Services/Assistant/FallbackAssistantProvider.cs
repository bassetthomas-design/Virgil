using System;
using System.Threading;
using System.Threading.Tasks;

namespace Virgil.Services.Assistant;

public sealed class FallbackAssistantProvider : IAssistantProvider
{
    private readonly IAssistantProvider _primary;
    private readonly IAssistantProvider _fallback;
    private readonly string _fallbackMessage;

    public FallbackAssistantProvider(IAssistantProvider primary, IAssistantProvider fallback, string fallbackMessage)
    {
        _primary = primary ?? throw new ArgumentNullException(nameof(primary));
        _fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
        _fallbackMessage = fallbackMessage ?? throw new ArgumentNullException(nameof(fallbackMessage));
    }

    public async Task<AssistantReply> AskAsync(string userMessage, AssistantContext ctx, CancellationToken ct = default)
    {
        try
        {
            return await _primary.AskAsync(userMessage, ctx, ct).ConfigureAwait(false);
        }
        catch (AssistantProviderUnavailableException)
        {
            return await AskFallbackAsync(userMessage, ctx, ct).ConfigureAwait(false);
        }
    }

    private async Task<AssistantReply> AskFallbackAsync(string userMessage, AssistantContext ctx, CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
        {
            ct.ThrowIfCancellationRequested();
        }

        try
        {
            var reply = await _fallback.AskAsync(userMessage, ctx, ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(reply.Text))
            {
                return new AssistantReply(_fallbackMessage, reply.ProposedActions);
            }

            return new AssistantReply($"{_fallbackMessage}\n{reply.Text}", reply.ProposedActions);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return new AssistantReply(_fallbackMessage, Array.Empty<ProposedAction>());
        }
    }
}
