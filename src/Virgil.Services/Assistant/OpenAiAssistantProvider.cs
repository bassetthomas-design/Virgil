using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Virgil.Services.Assistant;

public sealed class OpenAiAssistantProvider : IAssistantProvider
{
    private const string DefaultBaseUrl = "https://api.openai.com/v1/";
    private const string DefaultModel = "gpt-4o-mini";
    private const int DefaultTimeoutSeconds = 30;

    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly string? _apiKey;
    private readonly bool _isProviderEnabled;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public OpenAiAssistantProvider(
        string? apiKey,
        string? model = null,
        TimeSpan? timeout = null,
        HttpClient? httpClient = null,
        bool isProviderEnabled = true)
    {
        _apiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim();
        _model = string.IsNullOrWhiteSpace(model) ? DefaultModel : model;
        _isProviderEnabled = isProviderEnabled;

        if (httpClient is not null)
        {
            _httpClient = httpClient;
            if (_httpClient.BaseAddress is null)
            {
                _httpClient.BaseAddress = new Uri(DefaultBaseUrl, UriKind.Absolute);
            }
        }
        else
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(DefaultBaseUrl, UriKind.Absolute),
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
        if (!_isProviderEnabled || string.IsNullOrWhiteSpace(_apiKey))
        {
            return BuildSettingsReply("OpenAI non configuré.");
        }

        var prompt = AssistantPromptBuilder.BuildSystemPrompt(ctx);
        var messages = new List<OpenAiChatMessage>
        {
            new("system", prompt)
        };
        var memoryMessage = ConversationMemoryStore.BuildMemorySystemMessage();
        if (!string.IsNullOrWhiteSpace(memoryMessage))
        {
            messages.Add(new OpenAiChatMessage("system", memoryMessage));
        }

        messages.Add(new OpenAiChatMessage("user", userMessage));

        var payload = new OpenAiChatRequest(
            _model,
            messages.ToArray(),
            new OpenAiResponseFormat("json_object"));

        using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        request.Content = JsonContent.Create(payload, options: _jsonOptions);

        try
        {
            using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return BuildSettingsReply("Clé OpenAI invalide ou expirée.");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new AssistantProviderUnavailableException($"OpenAI failed with status {response.StatusCode}.");
            }

            var rawResponse = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var content = ExtractAssistantContent(rawResponse);
            return AssistantResponseParser.Parse(content);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException ex)
        {
            throw new AssistantProviderUnavailableException("OpenAI timeout.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new AssistantProviderUnavailableException("OpenAI request failed.", ex);
        }
    }

    private static AssistantReply BuildSettingsReply(string message)
        => new(
            message,
            new[] { new ProposedAction("open_settings", "Ouvrir les paramètres", RequiresConfirmation: false) });

    private static string ExtractAssistantContent(string rawResponse)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawResponse);
            if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var choice = choices[0];
                if (choice.TryGetProperty("message", out var message)
                    && message.TryGetProperty("content", out var content))
                {
                    return content.GetString() ?? string.Empty;
                }
            }
        }
        catch (JsonException)
        {
            return string.Empty;
        }

        return string.Empty;
    }

    private sealed record OpenAiChatRequest(
        string Model,
        OpenAiChatMessage[] Messages,
        [property: JsonPropertyName("response_format")] OpenAiResponseFormat ResponseFormat);

    private sealed record OpenAiChatMessage(string Role, string Content);

    private sealed record OpenAiResponseFormat(string Type);
}
