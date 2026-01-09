using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Virgil.Services.Assistant;

public sealed class EmbeddedLlamaProvider : IAssistantProvider
{
    private const string DefaultBaseUrl = "http://localhost:8080";
    private const int DefaultTimeoutSeconds = 30;
    private const string UnavailableMessage = "Pack Full manquant.";

    private readonly LlamaRuntimeManager _runtimeManager;
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public EmbeddedLlamaProvider(
        LlamaRuntimeManager runtimeManager,
        string? baseUrl = null,
        TimeSpan? timeout = null,
        HttpClient? httpClient = null)
    {
        _runtimeManager = runtimeManager ?? throw new ArgumentNullException(nameof(runtimeManager));

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
        try
        {
            await _runtimeManager.StartAsync(ct).ConfigureAwait(false);
            var healthy = await _runtimeManager.HealthCheckAsync(ct).ConfigureAwait(false);
            if (!healthy)
            {
                return UnavailableReply();
            }

            var prompt = BuildPrompt(ctx, userMessage);
            var payload = new EmbeddedLlamaRequest(
                prompt,
                false);

            using var response = await _httpClient.PostAsJsonAsync("/completion", payload, _jsonOptions, ct)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return UnavailableReply();
            }

            var rawResponse = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return ParseResponse(rawResponse);
        }
        catch (AssistantProviderUnavailableException)
        {
            return UnavailableReply();
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

    private AssistantReply ParseResponse(string rawResponse)
    {
        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            return AssistantReply.Empty;
        }

        var direct = TryParseDirectResponse(rawResponse);
        if (direct is not null)
        {
            return direct.Value;
        }

        var content = ExtractAssistantContent(rawResponse);
        if (string.IsNullOrWhiteSpace(content))
        {
            return AssistantReply.Empty;
        }

        return AssistantResponseParser.Parse(content);
    }

    private AssistantReply? TryParseDirectResponse(string rawResponse)
    {
        try
        {
            var parsed = JsonSerializer.Deserialize<EmbeddedLlamaResponse>(rawResponse, _jsonOptions);
            if (parsed is null)
            {
                return null;
            }

            var actions = parsed.ProposedActions ?? Array.Empty<ProposedAction>();
            return new AssistantReply(parsed.Text ?? string.Empty, actions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string ExtractAssistantContent(string rawResponse)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawResponse);
            if (doc.RootElement.TryGetProperty("content", out var content))
            {
                return content.GetString() ?? string.Empty;
            }

            if (doc.RootElement.TryGetProperty("response", out var response))
            {
                return response.GetString() ?? string.Empty;
            }

            if (doc.RootElement.TryGetProperty("message", out var message)
                && message.TryGetProperty("content", out var messageContent))
            {
                return messageContent.GetString() ?? string.Empty;
            }

            if (doc.RootElement.TryGetProperty("choices", out var choices)
                && choices.ValueKind == JsonValueKind.Array
                && choices.GetArrayLength() > 0)
            {
                var choice = choices[0];
                if (choice.TryGetProperty("message", out var choiceMessage)
                    && choiceMessage.TryGetProperty("content", out var choiceContent))
                {
                    return choiceContent.GetString() ?? string.Empty;
                }
            }
        }
        catch (JsonException)
        {
            return string.Empty;
        }

        return string.Empty;
    }

    private static string BuildPrompt(AssistantContext ctx, string userMessage)
    {
        var systemPrompt = AssistantPromptBuilder.BuildSystemPrompt(ctx);
        return $"{systemPrompt}\nUtilisateur: {userMessage}\nAssistant:";
    }

    private static AssistantReply UnavailableReply()
        => new(UnavailableMessage, Array.Empty<ProposedAction>());

    private sealed record EmbeddedLlamaRequest(
        [property: JsonPropertyName("prompt")] string Prompt,
        [property: JsonPropertyName("stream")] bool Stream);

    private sealed record EmbeddedLlamaResponse(string? Text, IReadOnlyList<ProposedAction>? ProposedActions);
}
