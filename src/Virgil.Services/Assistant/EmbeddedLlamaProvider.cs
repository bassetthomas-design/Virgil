using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Virgil.Core.Config;
using Virgil.Core.Logging;

namespace Virgil.Services.Assistant;

public sealed class EmbeddedLlamaProvider : IAssistantProvider
{
    private const string DefaultBaseUrl = "http://localhost:8080";
    private const int DefaultTimeoutSeconds = 30;

    private readonly ILocalLlmRuntime _runtimeManager;
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public EmbeddedLlamaProvider(
        ILocalLlmRuntime runtimeManager,
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
            var modelLocator = new ModelLocator();
            var modelTriedPaths = new List<string>(modelLocator.GetCandidatePaths());
            var modelFound = modelLocator.TryResolve(out var modelPath, out _);

            Log.Info($"Chemins modèle testés: {string.Join(" | ", modelTriedPaths)}");
            if (modelFound)
            {
                Log.Info($"Modèle GGUF utilisé: {modelPath}");
            }

            var runtimePathExpected = Path.Combine(AppContext.BaseDirectory, "AI", "Runtime", "llama-server.exe");
            var runtimeFound = File.Exists(runtimePathExpected);

            Log.Info($"Runtime IA attendu: {runtimePathExpected}");
            if (runtimeFound)
            {
                Log.Info($"Runtime IA utilisé: {runtimePathExpected}");
            }

            if (!modelFound || !runtimeFound)
            {
                return UnavailableReply(
                    BuildUnavailableMessage(modelFound, modelTriedPaths, runtimeFound, runtimePathExpected));
            }

            if (!_runtimeManager.IsRuntimeAvailable())
            {
                return UnavailableReply(
                    BuildUnavailableMessage(modelFound, modelTriedPaths, runtimeFound, runtimePathExpected));
            }

            if (modelFound)
            {
                _runtimeManager.SetModelPath(modelPath);
            }

            await _runtimeManager.StartAsync(ct).ConfigureAwait(false);
            var healthy = await _runtimeManager.HealthCheckAsync(ct).ConfigureAwait(false);
            if (!healthy)
            {
                return UnavailableReply(
                    BuildUnavailableMessage(modelFound, modelTriedPaths, runtimeFound, runtimePathExpected));
            }

            var prompt = BuildPrompt(ctx, userMessage);
            var payload = new EmbeddedLlamaRequest(
                prompt,
                false);

            ConfigureAuthHeaders();

            using var response = await _httpClient.PostAsJsonAsync("/completion", payload, _jsonOptions, ct)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return UnavailableReply(
                    BuildUnavailableMessage(modelFound, modelTriedPaths, runtimeFound, runtimePathExpected));
            }

            var rawResponse = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return ParseResponse(rawResponse);
        }
        catch (AssistantProviderUnavailableException)
        {
            return UnavailableReply(AppendRuntimeDiagnostics("IA indisponible."));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            return UnavailableReply("IA indisponible.");
        }
        catch (TaskCanceledException)
        {
            return UnavailableReply("IA indisponible.");
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

    private static AssistantReply UnavailableReply(string message)
        => new(message, Array.Empty<ProposedAction>());

    private static string BuildUnavailableMessage(
        bool modelFound,
        IReadOnlyCollection<string> modelTriedPaths,
        bool runtimeFound,
        string runtimePathExpected)
    {
        if (!modelFound && !runtimeFound)
        {
            return "IA indisponible: Modèle GGUF manquant + Runtime llama-server.exe manquant."
                + $"{Environment.NewLine}Chemins modèle testés: {string.Join(" | ", modelTriedPaths)}"
                + $"{Environment.NewLine}Chemin runtime attendu: {runtimePathExpected}";
        }

        if (!modelFound)
        {
            return "IA indisponible: Modèle GGUF manquant."
                + $"{Environment.NewLine}Chemins modèle testés: {string.Join(" | ", modelTriedPaths)}";
        }

        if (!runtimeFound)
        {
            return "IA indisponible: Runtime IA manquant (llama-server.exe)."
                + $"{Environment.NewLine}Chemin runtime attendu: {runtimePathExpected}";
        }

        return AppendRuntimeDiagnostics("IA indisponible.");
    }

    private static string AppendRuntimeDiagnostics(string message)
    {
        var diagnostics = LlamaRuntimeDiagnosticsStore.Latest;
        if (!string.IsNullOrWhiteSpace(diagnostics.WarningMessage))
        {
            message = $"{message}{Environment.NewLine}Warning runtime: {diagnostics.WarningMessage}";
        }

        if (!string.IsNullOrWhiteSpace(diagnostics.CommandLine))
        {
            message = $"{message}{Environment.NewLine}Commande runtime: {diagnostics.CommandLine}";
        }

        if (diagnostics.ExitCode.HasValue)
        {
            message = $"{message}{Environment.NewLine}ExitCode runtime: {diagnostics.ExitCode}";
        }

        if (!string.IsNullOrWhiteSpace(diagnostics.Stderr))
        {
            message = $"{message}{Environment.NewLine}STDERR runtime: {Truncate(diagnostics.Stderr, 2000)}";
        }

        var lastError = diagnostics.LastErrorMessage;
        if (string.IsNullOrWhiteSpace(lastError))
        {
            lastError = GetLastNonWarningLine(diagnostics.Stderr);
        }

        if (string.IsNullOrWhiteSpace(lastError))
        {
            return message;
        }

        return $"{message}{Environment.NewLine}Erreur runtime bloquante: {lastError}";
    }

    private static string GetLastNonWarningLine(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var lines = value.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        for (var index = lines.Length - 1; index >= 0; index--)
        {
            if (!IsRuntimeWarningLine(lines[index]))
            {
                return lines[index];
            }
        }

        return string.Empty;
    }

    private static bool IsRuntimeWarningLine(string line)
        => line.Contains("untrusted environments", StringComparison.OrdinalIgnoreCase)
            || line.Contains("not recommended", StringComparison.OrdinalIgnoreCase)
            || line.Contains("note:", StringComparison.OrdinalIgnoreCase);

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
        {
            return value;
        }

        return $"{value.Substring(0, maxLength)}…";
    }

    private sealed record EmbeddedLlamaRequest(
        [property: JsonPropertyName("prompt")] string Prompt,
        [property: JsonPropertyName("stream")] bool Stream);

    private sealed record EmbeddedLlamaResponse(string? Text, IReadOnlyList<ProposedAction>? ProposedActions);

    private void ConfigureAuthHeaders()
    {
        _httpClient.DefaultRequestHeaders.Authorization = null;
        _httpClient.DefaultRequestHeaders.Remove("X-API-Key");

        if (string.IsNullOrWhiteSpace(_runtimeManager.ApiKey))
        {
            return;
        }

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _runtimeManager.ApiKey);
        _httpClient.DefaultRequestHeaders.Add("X-API-Key", _runtimeManager.ApiKey);
    }
}
