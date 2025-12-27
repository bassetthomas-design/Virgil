using System.Text.Json;
using Virgil.Core.Config;
using Virgil.Core.Logging;

namespace Virgil.Services.Chat;

public sealed class LocalLlmChatEngine : IChatEngine
{
    private readonly string _assetsRoot;
    private readonly string _modelPath;
    private readonly string _systemPromptPath;
    private readonly IChatCommandParser _parser;

    public LocalLlmChatEngine(string? assetsRoot = null, IChatCommandParser? parser = null)
    {
        _assetsRoot = assetsRoot ?? AppPaths.UserDataRoot;
        _modelPath = Path.Combine(_assetsRoot, "assets", "models", "virgil-model.gguf");
        _systemPromptPath = Path.Combine(_assetsRoot, "assets", "prompts", "system_prompt.txt");
        _parser = parser ?? new ChatCommandParser();
    }

    public async Task<ChatEngineResult> GenerateAsync(string userText, ChatContext context, CancellationToken ct = default)
    {
        EnsureAssetsPresent();

        var prompt = await File.ReadAllTextAsync(_systemPromptPath, ct).ConfigureAwait(false);
        var rawResponse = BuildOfflineResponse(prompt, userText, context);
        return _parser.ParseResponse(rawResponse);
    }

    private void EnsureAssetsPresent()
    {
        if (!File.Exists(_modelPath))
        {
            throw new ChatEngineUnavailableException($"Modèle introuvable: {_modelPath}");
        }

        if (!File.Exists(_systemPromptPath))
        {
            throw new ChatEngineUnavailableException($"Prompt système introuvable: {_systemPromptPath}");
        }
    }

    private static string BuildOfflineResponse(string prompt, string userText, ChatContext context)
    {
        var replyText = $"[Offline LLM] {userText}";
        var payload = new
        {
            text = replyText,
            command = new { type = "none" }
        };

        Log.Info($"LLM offline: prompt chargé ({prompt.Length} chars), historique {context.History.Count} messages.");
        return JsonSerializer.Serialize(payload);
    }
}
