using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Virgil.Core.Logging;

namespace Virgil.Services.Assistant;

public sealed record LocalLlamaChatResult(bool Success, string Content, string? ErrorMessage);

public sealed class LocalLlamaClient
{
    private const string ModelsEndpoint = "/v1/models";
    private const string ChatCompletionsEndpoint = "/v1/chat/completions";
    private const int ErrorSnippetLength = 2000;
    private const string SystemPrompt =
        "You are Virgil, a helpful conversational Windows assistant. Do not execute commands unless explicitly asked.";

    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonRequestOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public LocalLlamaClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<LocalLlamaChatResult> ChatAsync(string message, CancellationToken ct = default)
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

        var payload = new
        {
            model = modelResult.ModelId,
            messages = new[]
            {
                new { role = "system", content = SystemPrompt },
                new { role = "user", content = message }
            },
            temperature = 0.3,
            max_tokens = 128,
            stream = false
        };

        var payloadJson = JsonSerializer.Serialize(payload, _jsonRequestOptions);
        var endpointUrl = _httpClient.BaseAddress is null
            ? ChatCompletionsEndpoint
            : new Uri(_httpClient.BaseAddress, ChatCompletionsEndpoint).ToString();

        Log.Info($"Local Llama chat test: POST {endpointUrl} payload={payloadJson}");

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

            if (!response.IsSuccessStatusCode)
            {
                Log.Warn($"Local Llama chat test: POST {endpointUrl} -> HTTP {(int)response.StatusCode} {response.StatusCode} in {stopwatch.ElapsedMilliseconds}ms | {responseSnippet}");
                return new LocalLlamaChatResult(false, string.Empty, BuildErrorMessage("/v1/chat/completions", response, responseSnippet));
            }

            Log.Info($"Local Llama chat test: POST {endpointUrl} -> HTTP {(int)response.StatusCode} {response.StatusCode} in {stopwatch.ElapsedMilliseconds}ms | {responseSnippet}");

            var content = TryExtractChatContent(body);
            return new LocalLlamaChatResult(true, content ?? string.Empty, null);
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            Log.Warn($"Local Llama chat test: POST {endpointUrl} -> exception after {stopwatch.ElapsedMilliseconds}ms: {ex.GetBaseException().Message}");
            return new LocalLlamaChatResult(false, string.Empty, "generation failed");
        }
        catch (TaskCanceledException ex)
        {
            stopwatch.Stop();
            Log.Warn($"Local Llama chat test: POST {endpointUrl} -> timeout after {stopwatch.ElapsedMilliseconds}ms: {ex.GetBaseException().Message}");
            return new LocalLlamaChatResult(false, string.Empty, "generation failed");
        }
    }

    private async Task<LocalModelFetchResult> FetchModelIdAsync(CancellationToken ct)
    {
        var endpointUrl = _httpClient.BaseAddress is null
            ? ModelsEndpoint
            : new Uri(_httpClient.BaseAddress, ModelsEndpoint).ToString();

        Log.Info($"Local Llama chat test: GET {endpointUrl}");

        try
        {
            using var response = await _httpClient.GetAsync(ModelsEndpoint, ct).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var responseSnippet = Truncate(body, ErrorSnippetLength);

            if (!response.IsSuccessStatusCode)
            {
                Log.Warn($"Local Llama chat test: GET {endpointUrl} -> HTTP {(int)response.StatusCode} {response.StatusCode} | {responseSnippet}");
                return new LocalModelFetchResult(false, null, BuildErrorMessage("/v1/models", response, responseSnippet));
            }

            Log.Info($"Local Llama chat test: GET {endpointUrl} -> HTTP {(int)response.StatusCode} {response.StatusCode} | {responseSnippet}");

            var modelId = TryExtractModelId(body);
            if (string.IsNullOrWhiteSpace(modelId))
            {
                Log.Warn($"Local Llama chat test: GET {endpointUrl} -> no model id in response.");
                return new LocalModelFetchResult(false, null, "Modèle IA local introuvable.");
            }

            LocalLlamaState.Instance.UpdateModelId(modelId);
            return new LocalModelFetchResult(true, modelId, null);
        }
        catch (HttpRequestException ex)
        {
            Log.Warn($"Local Llama chat test: GET {endpointUrl} -> exception: {ex.GetBaseException().Message}");
            return new LocalModelFetchResult(false, null, "generation failed");
        }
        catch (TaskCanceledException ex)
        {
            Log.Warn($"Local Llama chat test: GET {endpointUrl} -> timeout: {ex.GetBaseException().Message}");
            return new LocalModelFetchResult(false, null, "generation failed");
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

    private static string? TryExtractChatContent(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return string.Empty;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("choices", out var choices)
                || choices.ValueKind != JsonValueKind.Array
                || choices.GetArrayLength() == 0)
            {
                return string.Empty;
            }

            var first = choices[0];
            if (first.TryGetProperty("message", out var message)
                && message.TryGetProperty("content", out var content))
            {
                return content.GetString() ?? string.Empty;
            }
        }
        catch (JsonException)
        {
        }

        return string.Empty;
    }

    private static string BuildErrorMessage(string endpoint, HttpResponseMessage response, string snippet)
    {
        var statusLabel = $"{(int)response.StatusCode} {response.StatusCode}";
        return string.IsNullOrWhiteSpace(snippet)
            ? $"Erreur {endpoint}: {statusLabel}"
            : $"Erreur {endpoint}: {statusLabel} {snippet}";
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value.Substring(0, maxLength);
    }
}
