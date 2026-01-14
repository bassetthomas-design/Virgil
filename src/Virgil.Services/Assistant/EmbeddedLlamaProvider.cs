using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text;
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
        var modelFound = false;
        var runtimeFound = false;
        var runtimePathExpected = Path.Combine(AppContext.BaseDirectory, "AI", "Runtime", "llama-server.exe");
        IReadOnlyCollection<string> modelTriedPaths = Array.Empty<string>();
        try
        {
            var modelLocator = new ModelLocator();
            modelTriedPaths = new List<string>(modelLocator.GetCandidatePaths());
            modelFound = modelLocator.TryResolve(out var modelPath, out _);

            Log.Info($"Chemins modèle testés: {string.Join(" | ", modelTriedPaths)}");
            if (modelFound)
            {
                Log.Info($"Modèle GGUF utilisé: {modelPath}");
            }

            runtimeFound = File.Exists(runtimePathExpected);

            Log.Info($"Runtime IA attendu: {runtimePathExpected}");
            if (runtimeFound)
            {
                Log.Info($"Runtime IA utilisé: {runtimePathExpected}");
            }

            if (!modelFound || !runtimeFound)
            {
                return BuildUnavailableReply(
                    modelFound,
                    modelTriedPaths,
                    runtimeFound,
                    runtimePathExpected);
            }

            if (!_runtimeManager.IsRuntimeAvailable())
            {
                return BuildUnavailableReply(
                    modelFound,
                    modelTriedPaths,
                    runtimeFound,
                    runtimePathExpected);
            }

            if (modelFound)
            {
                _runtimeManager.SetModelPath(modelPath);
            }

            await _runtimeManager.StartAsync(ct).ConfigureAwait(false);
            var healthy = await _runtimeManager.HealthCheckAsync(ct).ConfigureAwait(false);
            if (!healthy)
            {
                return BuildUnavailableReply(
                    modelFound,
                    modelTriedPaths,
                    runtimeFound,
                    runtimePathExpected);
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
                return BuildUnavailableReply(
                    modelFound,
                    modelTriedPaths,
                    runtimeFound,
                    runtimePathExpected);
            }

            var rawResponse = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return ParseResponse(rawResponse);
        }
        catch (AssistantProviderUnavailableException)
        {
            return BuildUnavailableReply(modelFound, modelTriedPaths, runtimeFound, runtimePathExpected);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            return BuildUnavailableReply(modelFound, modelTriedPaths, runtimeFound, runtimePathExpected);
        }
        catch (TaskCanceledException)
        {
            return BuildUnavailableReply(modelFound, modelTriedPaths, runtimeFound, runtimePathExpected);
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

    private AssistantReply BuildUnavailableReply(
        bool modelFound,
        IReadOnlyCollection<string> modelTriedPaths,
        bool runtimeFound,
        string runtimePathExpected)
    {
        var diagnostics = BuildDiagnosticReport(modelFound, modelTriedPaths, runtimeFound, runtimePathExpected);
        var action = new ProposedAction(
            "copy_diagnostic",
            "Copier diagnostic",
            new Dictionary<string, string> { ["text"] = diagnostics },
            RequiresConfirmation: false);
        return new AssistantReply(
            "IA indisponible. Utilisez « Copier diagnostic » pour obtenir le rapport détaillé.",
            new[] { action });
    }

    private string BuildDiagnosticReport(
        bool modelFound,
        IReadOnlyCollection<string> modelTriedPaths,
        bool runtimeFound,
        string runtimePathExpected)
    {
        var diagnostics = LlamaRuntimeDiagnosticsStore.Latest;
        var baseUrl = string.IsNullOrWhiteSpace(diagnostics.BaseUrl)
            ? _httpClient.BaseAddress?.ToString() ?? string.Empty
            : diagnostics.BaseUrl;
        var runtimePath = string.IsNullOrWhiteSpace(diagnostics.ExecutablePath)
            ? runtimePathExpected
            : diagnostics.ExecutablePath;
        var arguments = string.IsNullOrWhiteSpace(diagnostics.Arguments) ? "—" : diagnostics.Arguments;
        var statusLabel = diagnostics.LastModelsStatusCode.HasValue
            ? diagnostics.LastModelsStatusCode.Value.ToString()
            : "aucun";
        var modelsExcerpt = string.IsNullOrWhiteSpace(diagnostics.LastModelsResponseExcerpt)
            ? "—"
            : diagnostics.LastModelsResponseExcerpt;
        var failureCategory = ResolveFailureCategory(modelFound, runtimeFound, diagnostics);

        var sb = new StringBuilder();
        sb.AppendLine("Diagnostic IA locale");
        sb.AppendLine($"Cause: {failureCategory}");
        sb.AppendLine($"Runtime: {runtimePath}");
        sb.AppendLine($"Arguments: {arguments}");
        sb.AppendLine($"Base URL: {baseUrl}");
        sb.AppendLine($"Ping /v1/models: HTTP {statusLabel}");
        sb.AppendLine($"Réponse /v1/models: {modelsExcerpt}");
        sb.AppendLine($"Dernière erreur: {diagnostics.LastErrorMessage ?? "—"}");

        if (!modelFound && modelTriedPaths.Count > 0)
        {
            sb.AppendLine($"Chemins modèle testés: {string.Join(" | ", modelTriedPaths)}");
        }

        if (!runtimeFound)
        {
            sb.AppendLine($"Chemin runtime attendu: {runtimePathExpected}");
        }

        var stdoutTail = GetLastLines(diagnostics.Stdout, 12);
        var stderrTail = GetLastLines(diagnostics.Stderr, 12);
        sb.AppendLine($"Logs stdout (12 dernières lignes): {stdoutTail}");
        sb.AppendLine($"Logs stderr (12 dernières lignes): {stderrTail}");

        return sb.ToString().TrimEnd();
    }

    private static string ResolveFailureCategory(bool modelFound, bool runtimeFound, LlamaRuntimeDiagnostics diagnostics)
    {
        if (!runtimeFound)
        {
            LlamaRuntimeDiagnosticsStore.Update(existing => existing with { FailureCategory = "RuntimeMissing" });
            return "RuntimeMissing";
        }

        if (!modelFound)
        {
            LlamaRuntimeDiagnosticsStore.Update(existing => existing with { FailureCategory = "ModelMissing" });
            return "ModelMissing";
        }

        if (!string.IsNullOrWhiteSpace(diagnostics.FailureCategory))
        {
            return diagnostics.FailureCategory;
        }

        return "EndpointUnavailable";
    }

    private static string GetLastLines(string value, int maxLines)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "—";
        }

        var lines = value.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length <= maxLines)
        {
            return string.Join(Environment.NewLine, lines);
        }

        var start = Math.Max(0, lines.Length - maxLines);
        return string.Join(Environment.NewLine, lines[start..]);
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
