using System.Text.Json;
using Virgil.Core.Config;
using Virgil.Core.Logging;
using Virgil.Services;

namespace Virgil.Services.Chat;

public sealed class LocalLlmChatEngine : IChatEngine
{
    private readonly string _assetsRoot;
    private readonly ModelLocator _modelLocator;
    private readonly string _systemPromptPath;
    private readonly IChatCommandParser _parser;
    private readonly PerformanceService.IMemoryReader _memoryReader;

    public LocalLlmChatEngine(
        string? assetsRoot = null,
        IChatCommandParser? parser = null,
        PerformanceService.IMemoryReader? memoryReader = null,
        ModelLocator? modelLocator = null)
    {
        _assetsRoot = assetsRoot ?? AppPaths.UserDataRoot;
        _modelLocator = modelLocator ?? new ModelLocator();
        _systemPromptPath = Path.Combine(_assetsRoot, "assets", "prompts", "system_prompt.txt");
        _parser = parser ?? new ChatCommandParser();
        _memoryReader = memoryReader ?? new PerformanceService.WindowsMemoryReader();
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
        EnsureMinimumMemory();

        if (!_modelLocator.TryResolve(out _, out var reason))
        {
            throw new ChatEngineUnavailableException(reason);
        }

        if (!File.Exists(_systemPromptPath))
        {
            throw new ChatEngineUnavailableException($"Prompt système introuvable: {_systemPromptPath}");
        }
    }

    private void EnsureMinimumMemory()
    {
        if (!_memoryReader.IsSupportedPlatform)
        {
            return;
        }

        var snapshot = _memoryReader.GetSnapshot();
        var totalGb = snapshot.TotalPhysicalMb / 1024.0;
        if (totalGb < 8)
        {
            throw new ChatEngineUnavailableException("IA offline indisponible: 8 Go de RAM minimum requis.");
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
