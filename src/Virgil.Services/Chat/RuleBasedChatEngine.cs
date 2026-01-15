namespace Virgil.Services.Chat;

public sealed class RuleBasedChatEngine : IChatEngine
{
    private readonly IChatCommandParser _parser;

    public RuleBasedChatEngine(IChatCommandParser? parser = null)
    {
        _parser = parser ?? new ChatCommandParser();
    }

    public Task<ChatEngineResult> GenerateAsync(string userText, ChatContext context, CancellationToken ct = default)
    {
        var suggestedAction = SuggestAction(userText);
        var text = suggestedAction is null
            ? "Je reste en veille, aucun modèle local disponible."
            : $"Je peux lancer '{suggestedAction}' si tu veux.";

        var payload = suggestedAction is null
            ? $"{{\"text\":\"{text}\",\"command\":{{\"type\":\"none\"}}}}"
            : $"{{\"text\":\"{text}\",\"command\":{{\"type\":\"action\",\"action\":\"{suggestedAction}\"}}}}";

        return Task.FromResult(_parser.ParseResponse(payload));
    }

    private static string? SuggestAction(string userText)
    {
        if (string.IsNullOrWhiteSpace(userText))
        {
            return null;
        }

        var trimmed = userText.TrimStart();
        if (!IsCommandMode(trimmed, out var commandText))
        {
            return null;
        }

        var lower = commandText.ToLowerInvariant();
        if (lower.Contains("nettoyage") || lower.Contains("clean"))
        {
            return "clean_quick";
        }

        if (lower.Contains("navigateur") || lower.Contains("browser"))
        {
            return "clean_browsers";
        }

        if (lower.Contains("analyse") || lower.Contains("scan"))
        {
            return "status";
        }

        return null;
    }

    private static bool IsCommandMode(string userText, out string commandText)
    {
        commandText = userText;
        if (string.IsNullOrWhiteSpace(userText))
        {
            return false;
        }

        var prefixes = new[] { "/cmd", "!cmd" };
        foreach (var prefix in prefixes)
        {
            if (userText.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                commandText = userText[prefix.Length..].TrimStart();
                return true;
            }
        }

        return false;
    }
}
