using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
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
    private const int DefaultMaxTokens = 768;
    private const int MaxContinuations = 2;
    private const string ContinuationPrompt = "Continue exactement là où tu t'es arrêté, sans répéter.";
    private const int MaxDiagnosticCharacters = 3500;
    private const int DiagnosticTailLines = 30;
    private const int ErrorSnippetLength = 2000;
    private const string ChatCompletionsEndpoint = "/v1/chat/completions";

    private readonly ILocalLlmRuntime _runtimeManager;
    private readonly HttpClient _httpClient;
    private readonly string _providerPreference;
    private readonly bool _localEnabled;
    private readonly bool _openAiEnabled;
    private readonly int _maxTokens;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    private readonly JsonSerializerOptions _jsonRequestOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public EmbeddedLlamaProvider(
        ILocalLlmRuntime runtimeManager,
        string? baseUrl = null,
        TimeSpan? timeout = null,
        HttpClient? httpClient = null,
        string? providerPreference = null,
        bool localEnabled = true,
        bool openAiEnabled = true,
        int? maxTokens = null)
    {
        _runtimeManager = runtimeManager ?? throw new ArgumentNullException(nameof(runtimeManager));
        _providerPreference = string.IsNullOrWhiteSpace(providerPreference) ? "—" : providerPreference;
        _localEnabled = localEnabled;
        _openAiEnabled = openAiEnabled;
        _maxTokens = maxTokens.GetValueOrDefault(DefaultMaxTokens);

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
        string? resolvedModelPath = null;
        try
        {
            var modelLocator = new ModelLocator();
            modelTriedPaths = new List<string>(modelLocator.GetCandidatePaths());
            modelFound = modelLocator.TryResolve(out var modelPath, out _);
            resolvedModelPath = modelFound ? modelPath : null;

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
                    resolvedModelPath,
                    modelTriedPaths,
                    runtimeFound,
                    runtimePathExpected);
            }

            if (!_runtimeManager.IsRuntimeAvailable())
            {
                return BuildUnavailableReply(
                    modelFound,
                    resolvedModelPath,
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
                    resolvedModelPath,
                    modelTriedPaths,
                    runtimeFound,
                    runtimePathExpected);
            }

            ConfigureAuthHeaders();

            var localClient = new LocalLlamaClient(_httpClient);
            var modelResult = await localClient.FetchModelIdAsync(ct).ConfigureAwait(false);
            if (!modelResult.Success || string.IsNullOrWhiteSpace(modelResult.ModelId))
            {
                var message = string.IsNullOrWhiteSpace(modelResult.ErrorMessage)
                    ? "IA locale prête. Erreur génération."
                    : $"IA locale prête. Erreur génération.{Environment.NewLine}{Environment.NewLine}{modelResult.ErrorMessage}";
                return new AssistantReply(message, Array.Empty<ProposedAction>());
            }

            var messages = new List<LocalChatMessage>
            {
                new("system", AssistantPromptBuilder.BuildSystemPrompt(ctx))
            };
            var memoryMessage = ConversationMemoryStore.BuildMemorySystemMessage();
            if (!string.IsNullOrWhiteSpace(memoryMessage))
            {
                messages.Add(new LocalChatMessage("system", memoryMessage));
            }

            messages.Add(new LocalChatMessage("user", userMessage));

            var endpointUrl = _httpClient.BaseAddress is null
                ? ChatCompletionsEndpoint
                : new Uri(_httpClient.BaseAddress, ChatCompletionsEndpoint).ToString();

            Log.Info($"Local Llama chat: POST {endpointUrl} model={modelResult.ModelId} stream=false max_tokens={_maxTokens}");

            var combined = new StringBuilder();
            var continuationCount = 0;
            while (true)
            {
                var responseResult = await SendChatAsync(modelResult.ModelId, messages, endpointUrl, ct).ConfigureAwait(false);
                if (!responseResult.Success)
                {
                    return new AssistantReply(responseResult.ErrorMessage ?? "IA locale prête. Erreur génération.", Array.Empty<ProposedAction>());
                }

                if (!string.IsNullOrWhiteSpace(responseResult.Content))
                {
                    combined.Append(responseResult.Content);
                }

                if (!string.Equals(responseResult.FinishReason, "length", StringComparison.OrdinalIgnoreCase)
                    || continuationCount >= MaxContinuations)
                {
                    return ParseResponse(combined.ToString());
                }

                continuationCount++;
                messages.Add(new LocalChatMessage("assistant", combined.ToString()));
                messages.Add(new LocalChatMessage("system", ContinuationPrompt));
            }
        }
        catch (AssistantProviderUnavailableException)
        {
            return BuildUnavailableReply(modelFound, resolvedModelPath, modelTriedPaths, runtimeFound, runtimePathExpected);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            Log.Warn("Runtime IA local: échec réseau pendant la génération.");
            return BuildUnavailableReply(modelFound, resolvedModelPath, modelTriedPaths, runtimeFound, runtimePathExpected);
        }
        catch (TaskCanceledException)
        {
            Log.Warn("Runtime IA local: timeout pendant la génération.");
            return BuildUnavailableReply(modelFound, resolvedModelPath, modelTriedPaths, runtimeFound, runtimePathExpected);
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
        if (!string.IsNullOrWhiteSpace(content))
        {
            return AssistantResponseParser.Parse(content);
        }

        return AssistantResponseParser.Parse(rawResponse);
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

    private AssistantReply BuildUnavailableReply(
        bool modelFound,
        string? resolvedModelPath,
        IReadOnlyCollection<string> modelTriedPaths,
        bool runtimeFound,
        string runtimePathExpected)
    {
        var diagnostics = BuildDiagnosticReport(modelFound, resolvedModelPath, modelTriedPaths, runtimeFound, runtimePathExpected);
        if (LocalLlamaStateService.Instance.Status == LocalStatus.Ready)
        {
            return new AssistantReply(
                $"IA locale prête. Erreur génération.{Environment.NewLine}{Environment.NewLine}Diagnostic (auto):{Environment.NewLine}{diagnostics}",
                Array.Empty<ProposedAction>());
        }

        return new AssistantReply(
            $"IA indisponible (Local Llama).{Environment.NewLine}{Environment.NewLine}Diagnostic (auto):{Environment.NewLine}{diagnostics}",
            Array.Empty<ProposedAction>());
    }

    private string BuildDiagnosticReport(
        bool modelFound,
        string? resolvedModelPath,
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
        var statusLabel = diagnostics.LastReadinessHttpStatus.HasValue
            ? $"HTTP {diagnostics.LastReadinessHttpStatus.Value}"
            : string.Empty;
        var lastModelsError = string.IsNullOrWhiteSpace(diagnostics.LastModelsErrorMessage)
            ? string.Empty
            : diagnostics.LastModelsErrorMessage;
        var modelsExcerpt = string.IsNullOrWhiteSpace(diagnostics.LastModelsResponseExcerpt)
            ? "—"
            : diagnostics.LastModelsResponseExcerpt;
        var failureCategory = ResolveFailureCategory(modelFound, runtimeFound, diagnostics);

        var sb = new StringBuilder();
        sb.AppendLine($"ProviderPreference: {_providerPreference}");
        sb.AppendLine($"LocalEnabled: {_localEnabled}");
        sb.AppendLine($"OpenAIEnabled: {_openAiEnabled}");
        sb.AppendLine($"Cause: {failureCategory}");
        sb.AppendLine($"RuntimePath: {FormatPathStatus(runtimePath)}");
        sb.AppendLine($"ModelPath: {FormatModelStatus(resolvedModelPath)}");
        sb.AppendLine($"BaseUrl: {FormatBaseUrl(baseUrl)}");
        sb.AppendLine($"ProcessStart: {FormatProcessStart(diagnostics)}");
        sb.AppendLine($"Readiness /v1/models: {FormatReadinessStatus(statusLabel, lastModelsError)}");
        sb.AppendLine($"ExitCode: {(diagnostics.ExitCode.HasValue ? diagnostics.ExitCode.Value.ToString() : "—")}");
        sb.AppendLine($"Stdout tail ({DiagnosticTailLines} lignes): {GetLastLines(diagnostics.Stdout, DiagnosticTailLines)}");
        sb.AppendLine($"Stderr tail ({DiagnosticTailLines} lignes): {GetLastLines(diagnostics.Stderr, DiagnosticTailLines)}");
        sb.AppendLine($"Dernière erreur: {diagnostics.LastErrorMessage ?? "—"}");
        sb.AppendLine($"Arguments: {arguments}");
        sb.AppendLine($"Réponse /v1/models: {modelsExcerpt}");

        if (!modelFound && modelTriedPaths.Count > 0)
        {
            sb.AppendLine($"Chemins modèle testés: {string.Join(" | ", modelTriedPaths)}");
        }

        if (!runtimeFound)
        {
            sb.AppendLine($"Chemin runtime attendu: {runtimePathExpected}");
        }

        return TrimDiagnostics(sb.ToString().TrimEnd());
    }

    private static string FormatReadinessStatus(string statusLabel, string errorMessage)
    {
        if (!string.IsNullOrWhiteSpace(statusLabel))
        {
            return statusLabel;
        }

        if (!string.IsNullOrWhiteSpace(errorMessage))
        {
            return $"Erreur: {errorMessage}";
        }

        return "aucun";
    }

    private static string FormatProcessStart(LlamaRuntimeDiagnostics diagnostics)
    {
        if (diagnostics.ProcessRunning)
        {
            return "success";
        }

        var error = diagnostics.LastErrorMessage;
        return string.IsNullOrWhiteSpace(error) ? "fail" : $"fail ({error})";
    }

    private static string FormatBaseUrl(string baseUrl)
    {
        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
        {
            return $"{uri.Host}:{uri.Port}";
        }

        return baseUrl;
    }

    private static string FormatPathStatus(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "—";
        }

        var exists = File.Exists(path);
        return $"{path} (exists: {exists})";
    }

    private static string FormatModelStatus(string? modelPath)
    {
        if (string.IsNullOrWhiteSpace(modelPath))
        {
            return "—";
        }

        try
        {
            var info = new FileInfo(modelPath);
            var exists = info.Exists;
            var sizeLabel = exists ? $"{info.Length} bytes" : "n/a";
            return $"{modelPath} (exists: {exists}, size: {sizeLabel})";
        }
        catch (Exception)
        {
            return $"{modelPath} (exists: {File.Exists(modelPath)})";
        }
    }

    private static string ResolveFailureCategory(bool modelFound, bool runtimeFound, LlamaRuntimeDiagnostics diagnostics)
    {
        if (LocalLlamaStateService.Instance.Status == LocalStatus.Ready)
        {
            return "None";
        }

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

        if (!diagnostics.ProcessRunning && !string.IsNullOrWhiteSpace(diagnostics.LastErrorMessage))
        {
            return "ProcessStartFailed";
        }

        if (!string.IsNullOrWhiteSpace(diagnostics.LastErrorMessage)
            && diagnostics.LastErrorMessage.Contains("incompatible", StringComparison.OrdinalIgnoreCase))
        {
            return "RuntimeIncompatible";
        }

        if (diagnostics.LastReadinessHttpStatus.HasValue && diagnostics.LastReadinessHttpStatus.Value >= 400)
        {
            return "HttpError";
        }

        if (!string.IsNullOrWhiteSpace(diagnostics.LastModelsErrorMessage))
        {
            return "EndpointUnavailable";
        }

        return "Unknown";
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

    private static string TrimDiagnostics(string value)
    {
        if (value.Length <= MaxDiagnosticCharacters)
        {
            return value;
        }

        var truncatedLength = Math.Max(0, MaxDiagnosticCharacters - 15);
        return $"{value.Substring(0, truncatedLength)}...(truncated)";
    }

    private sealed record EmbeddedLlamaResponse(string? Text, IReadOnlyList<ProposedAction>? ProposedActions);

    private sealed record LocalChatRequest(
        string Model,
        LocalChatMessage[] Messages,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        bool Stream);

    private sealed record LocalChatMessage(string Role, string Content);

    private async Task<ChatCompletionResult> SendChatAsync(
        string modelId,
        List<LocalChatMessage> messages,
        string endpointUrl,
        CancellationToken ct)
    {
        var payload = new LocalChatRequest(
            modelId,
            messages.ToArray(),
            _maxTokens,
            false);

        var payloadJson = JsonSerializer.Serialize(payload, _jsonRequestOptions);
        var stopwatch = Stopwatch.StartNew();
        using var request = new HttpRequestMessage(HttpMethod.Post, ChatCompletionsEndpoint)
        {
            Content = new StringContent(payloadJson, Encoding.UTF8, "application/json")
        };

        using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
        var rawResponse = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        stopwatch.Stop();

        if (response.StatusCode != System.Net.HttpStatusCode.OK)
        {
            var snippet = Truncate(rawResponse, ErrorSnippetLength);
            Log.Warn($"Local Llama chat: POST {endpointUrl} -> HTTP {(int)response.StatusCode} {response.StatusCode} in {stopwatch.ElapsedMilliseconds}ms | {snippet}");
            var statusLabel = $"{(int)response.StatusCode} {response.StatusCode}";
            var message = string.IsNullOrWhiteSpace(snippet)
                ? $"IA locale prête. Erreur génération.{Environment.NewLine}{Environment.NewLine}Erreur /v1/chat/completions: {statusLabel}"
                : $"IA locale prête. Erreur génération.{Environment.NewLine}{Environment.NewLine}Erreur /v1/chat/completions: {statusLabel} {snippet}";
            return new ChatCompletionResult(false, string.Empty, null, message);
        }

        var content = ExtractAssistantContent(rawResponse);
        var finishReason = ExtractFinishReason(rawResponse);
        Log.Info($"Local Llama chat: POST {endpointUrl} -> HTTP {(int)response.StatusCode} {response.StatusCode} in {stopwatch.ElapsedMilliseconds}ms | finish_reason={finishReason ?? "n/a"}");
        return new ChatCompletionResult(true, content, finishReason, null);
    }

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

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value.Substring(0, maxLength);
    }

    private static string? ExtractFinishReason(string rawResponse)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawResponse);
            if (doc.RootElement.TryGetProperty("choices", out var choices)
                && choices.ValueKind == JsonValueKind.Array
                && choices.GetArrayLength() > 0)
            {
                var choice = choices[0];
                if (choice.TryGetProperty("finish_reason", out var finishReason)
                    && finishReason.ValueKind == JsonValueKind.String)
                {
                    return finishReason.GetString();
                }
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }

    private sealed record ChatCompletionResult(bool Success, string Content, string? FinishReason, string? ErrorMessage);
}
