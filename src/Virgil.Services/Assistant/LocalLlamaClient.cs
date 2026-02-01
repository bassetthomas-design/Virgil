using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Virgil.Core.Logging;

namespace Virgil.Services.Assistant;

public sealed record LocalLlamaChatResult(bool Success, string Content, string? ErrorMessage);
public sealed record LocalModelFetchResult(bool Success, string? ModelId, string? ErrorMessage);

public sealed class LocalLlamaClient
{
    private const string ModelsEndpoint = "/v1/models";
    private const string ChatCompletionsEndpoint = "/v1/chat/completions";
    private const int DefaultMaxTokens = 768;
    private const int ErrorSnippetLength = 2000;
    private const int MaxContinuations = 2;
    private const string SystemPrompt =
        "Tu es VIRGIL, un assistant Windows local, utile et franc. " +
        "Tu réponds en français avec un ton légèrement sarcastique et punchliner, mais tu restes utile. " +
        "Commente brièvement chaque action système lancée (ex: \"Je lance le scan…\", \"Nettoyage terminé…\"). " +
        "N'invente pas et propose une vérification si besoin. " +
        "N'exécute JAMAIS de commandes système par défaut; mode commande uniquement si le message commence par /cmd. " +
        "Si le message ressemble à une commande mais ne commence pas par /cmd, demande ce que l'utilisateur veut faire.";
    private const string ContinuationPrompt = "Continue exactement là où tu t'es arrêté, sans répéter.";

    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonRequestOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public LocalLlamaClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<LocalLlamaChatResult> ChatAsync(string message, int? maxTokens = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return new LocalLlamaChatResult(false, string.Empty, "Message vide.");
        }

        var modelResult = await FetchModelIdAsync(ct).ConfigureAwait(false);
        if (!modelResult.Success || string.IsNullOrWhiteSpace(modelResult.ModelId))
        {
            var messageText = string.IsNullOrWhiteSpace(modelResult.ErrorMessage)
                ? "Modèle IA local introuvable."
                : modelResult.ErrorMessage;
            return new LocalLlamaChatResult(false, string.Empty, messageText);
        }

        var memoryMessage = ConversationMemoryStore.BuildMemorySystemMessage();
        var messages = new List<object>
        {
            new { role = "system", content = SystemPrompt }
        };
        if (!string.IsNullOrWhiteSpace(memoryMessage))
        {
            messages.Add(new { role = "system", content = memoryMessage });
        }

        messages.Add(new { role = "user", content = message });

        var resolvedMaxTokens = maxTokens.GetValueOrDefault(DefaultMaxTokens);
        var combined = new StringBuilder();
        var continuationCount = 0;

        while (true)
        {
            var responseResult = await SendChatAsync(modelResult.ModelId, messages, resolvedMaxTokens, ct).ConfigureAwait(false);
            if (!responseResult.Success)
            {
                return new LocalLlamaChatResult(false, string.Empty, responseResult.ErrorMessage ?? "Erreur génération.");
            }

            if (!string.IsNullOrWhiteSpace(responseResult.Content))
            {
                combined.Append(responseResult.Content);
            }

            if (!string.Equals(responseResult.FinishReason, "length", StringComparison.OrdinalIgnoreCase)
                || continuationCount >= MaxContinuations)
            {
                var combinedText = combined.ToString();
                if (!string.IsNullOrWhiteSpace(combinedText))
                {
                    ConversationMemoryStore.UpdateSessionSummary(message, combinedText);
                }

                return new LocalLlamaChatResult(true, combinedText, null);
            }

            continuationCount++;
            messages.Add(new { role = "assistant", content = combined.ToString() });
            messages.Add(new { role = "system", content = ContinuationPrompt });
        }
    }

    public async Task<LocalModelFetchResult> FetchModelIdAsync(CancellationToken ct = default)
    {
        Log.Info($"[LLAMA] GET /v1/models");

        try
        {
            using var response = await _httpClient.GetAsync(ModelsEndpoint, ct).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var responseSnippet = Truncate(body, ErrorSnippetLength);

            if (!response.IsSuccessStatusCode)
            {
                Log.Warn($"[LLAMA] GET /v1/models -> {(int)response.StatusCode} {response.StatusCode} | {responseSnippet}");
                RecordModelsFailure(response, responseSnippet);
                return new LocalModelFetchResult(false, null, BuildErrorMessage("/v1/models", response, responseSnippet));
            }

            Log.Info($"[LLAMA] GET /v1/models -> {(int)response.StatusCode} {response.StatusCode}");

            var modelId = TryExtractModelId(body);
            if (string.IsNullOrWhiteSpace(modelId))
            {
                Log.Warn("[LLAMA] GET /v1/models -> no model id in response.");
                return new LocalModelFetchResult(false, null, "Modèle IA local introuvable.");
            }

            var baseUrl = _httpClient.BaseAddress?.ToString();
            LocalLlamaStateService.Instance.MarkReadyFromModels(baseUrl, modelId);
            return new LocalModelFetchResult(true, modelId, null);
        }
        catch (Exception ex)
        {
            Log.Warn($"[LLAMA] GET /v1/models -> exception: {ex}");
            RecordModelsFailure(ex.GetBaseException().Message);
            return new LocalModelFetchResult(false, null, $"Erreur génération: {ex.GetBaseException().Message}");
        }
    }

    private static string TryExtractModelId(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return string.Empty;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("data", out var data)
                || data.ValueKind != JsonValueKind.Array
                || data.GetArrayLength() == 0)
            {
                return string.Empty;
            }

            var first = data[0];
            if (first.TryGetProperty("id", out var id))
            {
                return id.GetString() ?? string.Empty;
            }
        }
        catch (JsonException)
        {
        }

        return string.Empty;
    }

    private static ChatResult TryExtractChatResult(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new ChatResult(string.Empty, null);
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("choices", out var choices)
                || choices.ValueKind != JsonValueKind.Array
                || choices.GetArrayLength() == 0)
            {
                return new ChatResult(string.Empty, null);
            }

            var first = choices[0];
            string? finishReason = null;
            if (first.TryGetProperty("finish_reason", out var finishReasonElement)
                && finishReasonElement.ValueKind == JsonValueKind.String)
            {
                finishReason = finishReasonElement.GetString();
            }

            if (first.TryGetProperty("message", out var message)
                && message.TryGetProperty("content", out var content))
            {
                return new ChatResult(content.GetString() ?? string.Empty, finishReason);
            }
        }
        catch (JsonException)
        {
        }

        return new ChatResult(string.Empty, null);
    }

    private static string BuildErrorMessage(string endpoint, HttpResponseMessage response, string snippet)
    {
        var statusLabel = $"{(int)response.StatusCode} {response.StatusCode}";
        return $"Erreur {endpoint}: {statusLabel}";
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value.Substring(0, maxLength);
    }

    private async Task<ChatCompletionResult> SendChatAsync(
        string modelId,
        List<object> messages,
        int maxTokens,
        CancellationToken ct)
    {
        var payload = new
        {
            model = modelId,
            messages,
            temperature = 0.3,
            max_tokens = maxTokens,
            stream = false
        };

        var payloadJson = JsonSerializer.Serialize(payload, _jsonRequestOptions);
        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, ChatCompletionsEndpoint)
            {
                Content = new StringContent(payloadJson, Encoding.UTF8, "application/json")
            };

            using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            stopwatch.Stop();

            var responseSnippet = Truncate(body, ErrorSnippetLength);
            var statusLabel = $"{(int)response.StatusCode} {response.StatusCode}";
            var elapsedMs = stopwatch.ElapsedMilliseconds;

            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                Log.Warn($"[LLAMA] POST /v1/chat/completions -> {statusLabel} in {elapsedMs}ms | {responseSnippet}");
                RecordChatFailure(statusLabel, responseSnippet);
                return new ChatCompletionResult(false, string.Empty, null, BuildErrorMessage("/v1/chat/completions", response, responseSnippet));
            }

            var result = TryExtractChatResult(body);
            Log.Info($"[LLAMA] POST /v1/chat/completions -> {statusLabel} in {elapsedMs}ms | finish_reason={result.FinishReason ?? "n/a"}");

            return new ChatCompletionResult(true, result.Content, result.FinishReason, null);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Log.Warn($"[LLAMA] POST /v1/chat/completions -> exception after {stopwatch.ElapsedMilliseconds}ms: {ex}");
            RecordChatFailure("exception", ex.GetBaseException().Message);
            return new ChatCompletionResult(false, string.Empty, null, $"Erreur génération: {ex.GetBaseException().Message}");
        }
    }

    private static void RecordModelsFailure(HttpResponseMessage response, string responseSnippet)
    {
        LlamaRuntimeDiagnosticsStore.Update(existing => existing with
        {
            LastModelsResponseExcerpt = string.IsNullOrWhiteSpace(responseSnippet)
                ? existing.LastModelsResponseExcerpt
                : responseSnippet,
            LastModelsErrorMessage = $"HTTP {(int)response.StatusCode} {response.StatusCode}",
            LastReadinessHttpStatus = (int)response.StatusCode
        });
    }

    private static void RecordModelsFailure(string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            return;
        }

        LlamaRuntimeDiagnosticsStore.Update(existing => existing with
        {
            LastModelsErrorMessage = errorMessage
        });
    }

    private static void RecordChatFailure(string statusLabel, string details)
    {
        var composed = string.IsNullOrWhiteSpace(details) ? statusLabel : $"{statusLabel} | {details}";
        LlamaRuntimeDiagnosticsStore.Update(existing => existing with
        {
            LastErrorMessage = string.IsNullOrWhiteSpace(composed) ? existing.LastErrorMessage : composed
        });
    }

    private sealed record ChatCompletionResult(bool Success, string Content, string? FinishReason, string? ErrorMessage);
    private sealed record ChatResult(string Content, string? FinishReason);
}
