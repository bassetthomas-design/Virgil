using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Virgil.Services.Assistant;

public sealed class OllamaAssistantProvider : IAssistantProvider
{
    private const string DefaultBaseUrl = "http://localhost:11434";
    private const string DefaultModel = "llama3.1:8b";
    private const int DefaultTimeoutSeconds = 20;
    private const string UnavailableMessage = "IA locale indisponible (Ollama non démarré).";

    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public OllamaAssistantProvider(
        string? baseUrl = null,
        string? model = null,
        TimeSpan? timeout = null,
        HttpClient? httpClient = null)
    {
        _model = string.IsNullOrWhiteSpace(model) ? DefaultModel : model;
        var resolvedBaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? DefaultBaseUrl : baseUrl;

        if (httpClient is not null)
        {
            _httpClient = httpClient;
            if (_httpClient.BaseAddress is null)
            {
                _httpClient.BaseAddress = new Uri(resolvedBaseUrl, UriKind.Absolute);
            }
        }
        else
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(resolvedBaseUrl, UriKind.Absolute),
                Timeout = timeout ?? TimeSpan.FromSeconds(DefaultTimeoutSeconds)
            };
        }

        if (timeout is not null)
        {
            _httpClient.Timeout = timeout.Value;
        }
    }

    public async Task<AssistantReply> AskAsync(string userMessage, AssistantContext ctx, CancellationToken ct = default)
    {
        var prompt = AssistantPromptBuilder.BuildSystemPrompt(ctx);
        var payload = new OllamaChatRequest(
            _model,
            new[]
            {
                new OllamaChatMessage("system", prompt),
                new OllamaChatMessage("user", userMessage)
            },
            false);

        try
        {
            using var response = await _httpClient.PostAsJsonAsync("/api/chat", payload, _jsonOptions, ct)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return UnavailableReply();
            }

            var rawResponse = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var content = ExtractAssistantContent(rawResponse);
            if (string.IsNullOrWhiteSpace(content))
            {
                return AssistantReply.Empty;
            }

            return AssistantResponseParser.Parse(content);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            return UnavailableReply();
        }
        catch (TaskCanceledException)
        {
            return UnavailableReply();
        }
    }

    private static string ExtractAssistantContent(string rawResponse)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawResponse);
            if (doc.RootElement.TryGetProperty("message", out var message)
                && message.TryGetProperty("content", out var content))
            {
                return content.GetString() ?? string.Empty;
            }

            if (doc.RootElement.TryGetProperty("response", out var response))
            {
                return response.GetString() ?? string.Empty;
            }
        }
        catch (JsonException)
        {
            return string.Empty;
        }

        return string.Empty;
    }

    private static AssistantReply UnavailableReply()
        => new(UnavailableMessage, Array.Empty<ProposedAction>());

    private sealed record OllamaChatRequest(string Model, IEnumerable<OllamaChatMessage> Messages, bool Stream);

    private sealed record OllamaChatMessage(string Role, string Content);

}
