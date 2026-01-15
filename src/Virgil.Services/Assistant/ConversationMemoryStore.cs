using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Virgil.Core.Config;

namespace Virgil.Services.Assistant;

public static class ConversationMemoryStore
{
    private const int MaxInjectedCharacters = 1200;
    private const int MaxSummaryCharacters = 1200;
    private static readonly object LockObj = new();
    private static readonly string MemoryPath = Path.Combine(AppPaths.UserDataRoot, "memory.json");
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };
    private static readonly string[] SensitiveKeywords =
    {
        "password",
        "mot de passe",
        "mdp",
        "token",
        "secret",
        "api key",
        "apikey",
        "clé",
        "cle",
        "key",
        "bearer"
    };

    public static ConversationMemory Load()
    {
        lock (LockObj)
        {
            try
            {
                if (!File.Exists(MemoryPath))
                {
                    return new ConversationMemory();
                }

                var json = File.ReadAllText(MemoryPath);
                var memory = JsonSerializer.Deserialize<ConversationMemory>(json, JsonOptions);
                return memory ?? new ConversationMemory();
            }
            catch (Exception)
            {
                return new ConversationMemory();
            }
        }
    }

    public static void Save(ConversationMemory memory)
    {
        if (memory is null)
        {
            return;
        }

        lock (LockObj)
        {
            Directory.CreateDirectory(AppPaths.UserDataRoot);
            var json = JsonSerializer.Serialize(memory, JsonOptions);
            File.WriteAllText(MemoryPath, json);
        }
    }

    public static void Clear()
    {
        lock (LockObj)
        {
            if (File.Exists(MemoryPath))
            {
                File.Delete(MemoryPath);
            }
        }
    }

    public static string BuildMemorySystemMessage()
    {
        var memory = Load();
        var parts = new[]
        {
            SanitizeText(memory.UserProfile),
            SanitizeText(memory.CurrentProject),
            SanitizeText(memory.SessionSummary)
        };

        var combined = string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part))).Trim();
        if (string.IsNullOrWhiteSpace(combined))
        {
            return string.Empty;
        }

        var message = $"Contexte mémoire: {combined}";
        return Truncate(message, MaxInjectedCharacters);
    }

    public static void UpdateSessionSummary(string userMessage, string assistantReply)
    {
        if (string.IsNullOrWhiteSpace(userMessage) && string.IsNullOrWhiteSpace(assistantReply))
        {
            return;
        }

        var objective = ExtractObjective(userMessage);
        var decisions = ExtractDecisionSnippet(assistantReply);
        var nextActions = ExtractNextActions(assistantReply);

        var summaryBuilder = new StringBuilder();
        summaryBuilder.Append("Objectif: ").Append(string.IsNullOrWhiteSpace(objective) ? "—" : objective).AppendLine();
        summaryBuilder.Append("Décisions: ").Append(string.IsNullOrWhiteSpace(decisions) ? "—" : decisions).AppendLine();
        summaryBuilder.Append("Prochaines actions: ").Append(string.IsNullOrWhiteSpace(nextActions) ? "—" : nextActions);

        var summary = Truncate(summaryBuilder.ToString().Trim(), MaxSummaryCharacters);
        var memory = Load();
        memory.SessionSummary = summary;
        Save(memory);
    }

    private static string ExtractObjective(string userMessage)
    {
        var cleaned = SanitizeText(userMessage);
        if (IsSensitive(cleaned))
        {
            return string.Empty;
        }

        return ExtractFirstSentence(cleaned, 240);
    }

    private static string ExtractDecisionSnippet(string assistantReply)
    {
        var cleaned = SanitizeText(assistantReply);
        if (IsSensitive(cleaned))
        {
            return string.Empty;
        }

        return ExtractFirstSentence(cleaned, 360);
    }

    private static string ExtractNextActions(string assistantReply)
    {
        if (string.IsNullOrWhiteSpace(assistantReply))
        {
            return string.Empty;
        }

        var lines = assistantReply.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        var actions = new List<string>();
        foreach (var line in lines)
        {
            if (!IsBullet(line))
            {
                continue;
            }

            var cleaned = SanitizeText(TrimBullet(line));
            if (string.IsNullOrWhiteSpace(cleaned) || IsSensitive(cleaned))
            {
                continue;
            }

            actions.Add(Truncate(cleaned, 160));
            if (actions.Count >= 3)
            {
                break;
            }
        }

        return actions.Count == 0 ? string.Empty : string.Join(" | ", actions);
    }

    private static bool IsBullet(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var trimmed = line.TrimStart();
        if (trimmed.StartsWith("-", StringComparison.Ordinal)
            || trimmed.StartsWith("•", StringComparison.Ordinal)
            || trimmed.StartsWith("*", StringComparison.Ordinal))
        {
            return true;
        }

        if (trimmed.Length >= 2 && char.IsDigit(trimmed[0]) && (trimmed[1] == '.' || trimmed[1] == ')'))
        {
            return true;
        }

        return false;
    }

    private static string TrimBullet(string line)
    {
        var trimmed = line.TrimStart();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        if (trimmed.StartsWith("-", StringComparison.Ordinal)
            || trimmed.StartsWith("•", StringComparison.Ordinal)
            || trimmed.StartsWith("*", StringComparison.Ordinal))
        {
            return trimmed.TrimStart('-', '•', '*', ' ', '\t');
        }

        if (trimmed.Length >= 2 && char.IsDigit(trimmed[0]) && (trimmed[1] == '.' || trimmed[1] == ')'))
        {
            return trimmed.Substring(2).TrimStart();
        }

        return trimmed;
    }

    private static string ExtractFirstSentence(string text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var endIndex = text.IndexOfAny(new[] { '.', '!', '?' });
        var candidate = endIndex > 0 ? text.Substring(0, endIndex + 1) : text;
        return Truncate(candidate.Trim(), maxLength);
    }

    private static string SanitizeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalized = text.Replace("\r", " ").Replace("\n", " ").Trim();
        var parts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", parts);
    }

    private static bool IsSensitive(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return SensitiveKeywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
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

public sealed class ConversationMemory
{
    [JsonPropertyName("user_profile")]
    public string UserProfile { get; set; } = string.Empty;

    [JsonPropertyName("current_project")]
    public string CurrentProject { get; set; } = string.Empty;

    [JsonPropertyName("session_summary")]
    public string SessionSummary { get; set; } = string.Empty;
}
