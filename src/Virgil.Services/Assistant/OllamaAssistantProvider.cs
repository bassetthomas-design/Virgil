using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
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
        var prompt = BuildSystemPrompt(ctx);
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

            return ParseAssistantReply(content);
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

    private AssistantReply ParseAssistantReply(string content)
    {
        try
        {
            var parsed = JsonSerializer.Deserialize<OllamaAssistantResponse>(content, _jsonOptions);
            if (parsed is null)
            {
                return new AssistantReply(content, Array.Empty<ProposedAction>());
            }

            var actions = parsed.ProposedActions
                ?.Where(action => !string.IsNullOrWhiteSpace(action.ActionId))
                .Select(action => new ProposedAction(
                    action.ActionId!.Trim(),
                    string.IsNullOrWhiteSpace(action.Title) ? action.ActionId!.Trim() : action.Title!.Trim(),
                    NormalizeParameters(action.Parameters)))
                .ToList()
                ?? new List<ProposedAction>();

            return new AssistantReply(parsed.Text ?? string.Empty, actions);
        }
        catch (JsonException)
        {
            return new AssistantReply(content, Array.Empty<ProposedAction>());
        }
    }

    private static IReadOnlyDictionary<string, string>? NormalizeParameters(Dictionary<string, JsonElement>? parameters)
    {
        if (parameters is null || parameters.Count == 0)
        {
            return null;
        }

        var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in parameters)
        {
            normalized[key] = value.ValueKind switch
            {
                JsonValueKind.String => value.GetString() ?? string.Empty,
                JsonValueKind.Number => value.ToString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => value.ToString()
            };
        }

        return normalized;
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

    private static string BuildSystemPrompt(AssistantContext ctx)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Tu es l'assistant système Virgil.");
        builder.AppendLine("Réponds uniquement en JSON strict, sans texte additionnel.");
        builder.AppendLine("Format attendu:");
        builder.AppendLine("{ \"text\": \"...\", \"proposedActions\": [ { \"actionId\": \"...\", \"title\": \"...\", \"parameters\": { ... } } ] }");
        builder.AppendLine("Règles:");
        builder.AppendLine("- Ne propose QUE des actionId présents dans le catalogue.");
        builder.AppendLine("- Maximum 3 actions proposées.");
        builder.AppendLine("- Si aucune action pertinente, proposedActions doit être [].");
        builder.AppendLine();
        builder.AppendLine("Catalogue d'actions disponibles:");

        foreach (var item in ctx.ActionCatalog)
        {
            builder.AppendLine($"- Id: {item.Id} | Label: {item.Label} | Description: {item.Description} | Admin: {(item.RequiresAdmin ? "oui" : "non")} | Destructif: {(item.DestructiveFlag ? "oui" : "non")}");
        }

        builder.AppendLine();
        builder.AppendLine("Contexte système:");
        builder.AppendLine($"CPU: {ctx.Telemetry.Cpu} (stale: {ctx.Telemetry.CpuStale})");
        builder.AppendLine($"RAM: {ctx.Telemetry.Ram} (stale: {ctx.Telemetry.RamStale})");
        builder.AppendLine($"Température: {ctx.Telemetry.Temperature} (stale: {ctx.Telemetry.TemperatureStale})");
        builder.AppendLine($"Disque: {ctx.Telemetry.Disk} (stale: {ctx.Telemetry.DiskStale})");

        if (ctx.LastActionResult is not null)
        {
            builder.AppendLine($"Dernière action: {ctx.LastActionResult.Title} ({ctx.LastActionResult.Status})");
            if (ctx.LastActionResult.Lines is not null && ctx.LastActionResult.Lines.Count > 0)
            {
                builder.AppendLine("Résumé: " + string.Join(" | ", ctx.LastActionResult.Lines.Take(3)));
            }
        }

        return builder.ToString();
    }

    private static AssistantReply UnavailableReply()
        => new(UnavailableMessage, Array.Empty<ProposedAction>());

    private sealed record OllamaChatRequest(string Model, IEnumerable<OllamaChatMessage> Messages, bool Stream);

    private sealed record OllamaChatMessage(string Role, string Content);

    private sealed class OllamaAssistantResponse
    {
        public string? Text { get; set; }
        public List<OllamaAssistantAction>? ProposedActions { get; set; }
    }

    private sealed class OllamaAssistantAction
    {
        public string? ActionId { get; set; }
        public string? Title { get; set; }
        public Dictionary<string, JsonElement>? Parameters { get; set; }
    }
}
