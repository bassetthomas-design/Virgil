using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Virgil.Core.Logging;

namespace Virgil.Services.Assistant;

public sealed record LocalChatCompletionResult(bool Success, string Content, string? ErrorMessage);
public sealed record LocalModelFetchResult(bool Success, string? ModelId, string? ErrorMessage);

public static class LocalChatCompletionProbe
{
    private const string Endpoint = "/v1/chat/completions";
    private const string ModelsEndpoint = "/v1/models";
    private const int ErrorSnippetLength = 2000;

    public static async Task<LocalChatCompletionResult> RunAsync(HttpClient client, string modelId, CancellationToken ct = default)
    {
        if (client is null)
        {
            throw new ArgumentNullException(nameof(client));
        }

        var payload = new
        {
            model = modelId,
            messages = new[]
            {
                new { role = "system", content = "You are Virgil, a helpful Windows assistant." },
                new { role = "user", content = "ping" }
            },
            temperature = 0.2,
            max_tokens = 64,
            stream = false
        };

        var payloadJson = JsonSerializer.Serialize(payload);
        var endpointUrl = client.BaseAddress is null
            ? Endpoint
            : new Uri(client.BaseAddress, Endpoint).ToString();

        Log.Info($"Local Llama smoke test: POST {endpointUrl} model={modelId} stream=false max_tokens=64 payload={payloadJson}");

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
            {
                Content = new StringContent(payloadJson, Encoding.UTF8, "application/json")
            };

            using var response = await client.SendAsync(request, ct).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            stopwatch.Stop();

            if (!response.IsSuccessStatusCode)
            {
                var snippet = Truncate(body, ErrorSnippetLength);
                Log.Warn($"Local Llama smoke test: POST {endpointUrl} -> HTTP {(int)response.StatusCode} {response.StatusCode} in {stopwatch.ElapsedMilliseconds}ms | {snippet}");
                var statusLabel = $"{(int)response.StatusCode} {response.StatusCode}";
                var message = string.IsNullOrWhiteSpace(snippet)
                    ? $"Erreur /v1/chat/completions: {statusLabel}"
                    : $"Erreur /v1/chat/completions: {statusLabel} {snippet}";
                return new LocalChatCompletionResult(false, string.Empty, message);
            }

            Log.Info($"Local Llama smoke test: POST {endpointUrl} -> HTTP {(int)response.StatusCode} {response.StatusCode} in {stopwatch.ElapsedMilliseconds}ms");

            var content = TryExtractChatContent(body);
            if (string.IsNullOrWhiteSpace(content))
            {
                Log.Warn($"Local Llama smoke test: empty content, full response={body}");
            }

            return new LocalChatCompletionResult(true, content ?? string.Empty, null);
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            Log.Warn($"Local Llama smoke test: POST {endpointUrl} -> exception after {stopwatch.ElapsedMilliseconds}ms: {ex.GetBaseException().Message}");
            return new LocalChatCompletionResult(false, string.Empty, "generation failed");
        }
        catch (TaskCanceledException ex)
        {
            stopwatch.Stop();
            Log.Warn($"Local Llama smoke test: POST {endpointUrl} -> timeout after {stopwatch.ElapsedMilliseconds}ms: {ex.GetBaseException().Message}");
            return new LocalChatCompletionResult(false, string.Empty, "generation failed");
        }
    }

    public static async Task<LocalModelFetchResult> FetchModelIdAsync(HttpClient client, CancellationToken ct = default)
    {
        if (client is null)
        {
            throw new ArgumentNullException(nameof(client));
        }

        var endpointUrl = client.BaseAddress is null
            ? ModelsEndpoint
            : new Uri(client.BaseAddress, ModelsEndpoint).ToString();

        try
        {
            using var response = await client.GetAsync(ModelsEndpoint, ct).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var snippet = Truncate(body, ErrorSnippetLength);
                Log.Warn($"Local Llama models: GET {endpointUrl} -> HTTP {(int)response.StatusCode} {response.StatusCode} | {snippet}");
                var statusLabel = $"{(int)response.StatusCode} {response.StatusCode}";
                var message = string.IsNullOrWhiteSpace(snippet)
                    ? $"Erreur /v1/models: {statusLabel}"
                    : $"Erreur /v1/models: {statusLabel} {snippet}";
                return new LocalModelFetchResult(false, null, message);
            }

            var modelId = TryExtractModelId(body);
            if (string.IsNullOrWhiteSpace(modelId))
            {
                Log.Warn($"Local Llama models: GET {endpointUrl} -> no model id in response.");
                return new LocalModelFetchResult(false, null, "Modèle IA local introuvable.");
            }

            return new LocalModelFetchResult(true, modelId, null);
        }
        catch (HttpRequestException ex)
        {
            Log.Warn($"Local Llama models: GET {endpointUrl} -> exception: {ex.GetBaseException().Message}");
            return new LocalModelFetchResult(false, null, "generation failed");
        }
        catch (TaskCanceledException ex)
        {
            Log.Warn($"Local Llama models: GET {endpointUrl} -> timeout: {ex.GetBaseException().Message}");
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

    private static string TryExtractChatContent(string json)
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

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value.Substring(0, maxLength);
    }
}
