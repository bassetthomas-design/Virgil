using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Virgil.Services.Assistant;

internal static class AssistantResponseParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static AssistantReply Parse(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return AssistantReply.Empty;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<AssistantResponse>(content, JsonOptions);
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

    private sealed class AssistantResponse
    {
        public string? Text { get; set; }
        public List<AssistantAction>? ProposedActions { get; set; }
    }

    private sealed class AssistantAction
    {
        public string? ActionId { get; set; }
        public string? Title { get; set; }
        public Dictionary<string, JsonElement>? Parameters { get; set; }
    }
}
