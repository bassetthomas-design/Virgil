using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Virgil.Core.Logging;

namespace Virgil.Services.Assistant;

public sealed class LlamaRuntimeManager : IAsyncDisposable, ILocalLlmRuntime
{
    private static readonly TimeSpan DefaultHealthTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DefaultReadinessTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan DefaultShutdownTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DefaultStartupDelay = TimeSpan.FromMilliseconds(1500);
    private const string LocalHostAddress = "127.0.0.1";
    private const int DefaultPort = 8080;
    private const string RuntimeExecutableName = "llama-server.exe";
    private const string MissingModelStderrMessage = "no model will be loaded in this process";
    private const string MissingModelErrorMessage = "Modèle non chargé: argument --model manquant.";
    private const string ModelsEndpoint = "/v1/models";
    private const string FailureCategoryNone = "None";
    private static readonly string[] ReadinessEndpoints = { "/health", "/v1/health", ModelsEndpoint };
    private readonly string _baseUrl;
    private readonly string _executablePath;
    private readonly string _baseArguments;
    private readonly string? _configuredApiKey;
    private readonly HttpClient _httpClient;
    private readonly TimeSpan _healthTimeout;
    private readonly TimeSpan _readinessTimeout;
    private readonly TimeSpan _startupDelay;
    private readonly IRuntimeProcessRunner _processRunner;
    private readonly bool _skipCompatibilityCheck;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _outputLock = new();
    private IRuntimeProcess? _process;
    private StringBuilder _stdoutBuffer = new();
    private StringBuilder _stderrBuffer = new();
    private string? _tempApiKeyFilePath;
    private string? _apiKey;
    private string? _modelPath;
    private string _securityFlagsDetected = "—";
    private string _securityStrategy = "auth désactivée";
    private string _warningMessage = string.Empty;
    private bool _disposed;
    private bool _readyConfirmed;

    public LlamaRuntimeManager(
        string baseUrl,
        string? executablePath = null,
        string? arguments = null,
        string? apiKey = null,
        TimeSpan? healthTimeout = null,
        TimeSpan? readinessTimeout = null,
        HttpClient? httpClient = null,
        IRuntimeProcessRunner? processRunner = null,
        bool skipCompatibilityCheck = false,
        TimeSpan? startupDelay = null)
    {
        _baseUrl = baseUrl;
        var defaultRuntimePath = Path.GetFullPath(DefaultRuntimePath);
        if (!string.IsNullOrWhiteSpace(executablePath))
        {
            var requestedPath = Path.GetFullPath(executablePath);
            if (!string.Equals(requestedPath, defaultRuntimePath, StringComparison.OrdinalIgnoreCase))
            {
                Log.Warn($"Llama runtime path override ignored. Using packaged runtime at '{defaultRuntimePath}'. Requested: '{requestedPath}'.");
            }
        }

        _executablePath = defaultRuntimePath;
        _baseArguments = SanitizeRuntimeArguments(arguments);
        _configuredApiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim();
        _healthTimeout = healthTimeout ?? DefaultHealthTimeout;
        _readinessTimeout = readinessTimeout ?? DefaultReadinessTimeout;
        _startupDelay = startupDelay ?? DefaultStartupDelay;
        _processRunner = processRunner ?? new RuntimeProcessRunner();
        _skipCompatibilityCheck = skipCompatibilityCheck;

        _httpClient = httpClient ?? new HttpClient
        {
            BaseAddress = new Uri(_baseUrl, UriKind.Absolute),
            Timeout = _healthTimeout
        };

        if (httpClient is not null && _httpClient.BaseAddress is null)
        {
            _httpClient.BaseAddress = new Uri(_baseUrl, UriKind.Absolute);
        }
    }

    public static string DefaultRuntimePath
        => Path.Combine(AppContext.BaseDirectory, "AI", "Runtime", RuntimeExecutableName);

    public bool IsRuntimeAvailable()
        => File.Exists(_executablePath);

    public string RuntimePathUsed => _executablePath;

    public LlamaRuntimeDiagnostics Diagnostics => LlamaRuntimeDiagnosticsStore.Latest;

    public string? ApiKey => _apiKey;

    public void SetModelPath(string modelPath)
    {
        _modelPath = string.IsNullOrWhiteSpace(modelPath) ? null : modelPath.Trim();
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (IsProcessRunning())
            {
                return;
            }

            _readyConfirmed = false;

            var runtimeFullPath = Path.GetFullPath(_executablePath);
            var runtimeExists = File.Exists(runtimeFullPath);
            var workingDirectory = Path.GetDirectoryName(_executablePath) ?? AppContext.BaseDirectory;
            LocalAiFileLog.Write($"RuntimePath: {runtimeFullPath} (exists={runtimeExists})");
            LocalAiFileLog.Write($"WorkingDirectory: {workingDirectory}");
            if (Uri.TryCreate(_baseUrl, UriKind.Absolute, out var baseUri))
            {
                LocalAiFileLog.Write($"BaseUrl: {_baseUrl} (host={baseUri.Host}, port={baseUri.Port})");
            }
            else
            {
                LocalAiFileLog.Write($"BaseUrl: {_baseUrl}");
            }

            var modelPath = _modelPath;
            var resolvedModelPath = string.IsNullOrWhiteSpace(modelPath) ? null : Path.GetFullPath(modelPath);
            if (string.IsNullOrWhiteSpace(resolvedModelPath))
            {
                LocalAiFileLog.Write("ModelPath: (absent) (exists=false, length=—)");
            }
            else
            {
                var modelExistsAtStart = File.Exists(resolvedModelPath);
                var modelLengthAtStart = modelExistsAtStart ? new FileInfo(resolvedModelPath).Length : (long?)null;
                LocalAiFileLog.Write($"ModelPath: {resolvedModelPath} (exists={modelExistsAtStart}, length={(modelLengthAtStart.HasValue ? modelLengthAtStart.Value.ToString() : "—")})");
            }

            if (!IsRuntimeAvailable())
            {
                var missingRuntimeMessage = $"Llama runtime not found at '{_executablePath}'.";
                UpdateDiagnostics(
                    processRunning: false,
                    portOpen: false,
                    exitCode: null,
                    lastErrorMessage: missingRuntimeMessage,
                    localStatus: LocalStatus.Failed,
                    failureCategory: "RuntimeMissing");
                LocalAiFileLog.Write("Arguments: (non construits, runtime manquant)");
                LocalAiFileLog.Write("Process.Start: skipped (runtime manquant)");
                LocalAiFileLog.Write("FAILED Cause=RuntimeMissing");
                throw new AssistantProviderUnavailableException(missingRuntimeMessage);
            }

            if (!_skipCompatibilityCheck)
            {
                try
                {
                    await ValidateRuntimeCompatibilityAsync(ct).ConfigureAwait(false);
                    await LogRuntimeVersionAsync(ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LocalAiFileLog.Write($"Process.Start: skipped (compatibility check failed: {ex.Message})");
                    LocalAiFileLog.Write("FAILED Cause=EndpointUnavailable");
                    throw;
                }
            }

            Log.Info($"Llama runtime path: {_executablePath}");
            var port = ResolvePort(_baseUrl);
            _apiKey = null;

            if (string.IsNullOrWhiteSpace(resolvedModelPath))
            {
                UpdateDiagnostics(
                    processRunning: false,
                    portOpen: false,
                    exitCode: null,
                    lastErrorMessage: MissingModelErrorMessage,
                    localStatus: LocalStatus.Failed,
                    failureCategory: "ModelMissing");
                LocalAiFileLog.Write("Arguments: (non construits, modèle manquant)");
                LocalAiFileLog.Write("Process.Start: skipped (modèle manquant)");
                LocalAiFileLog.Write("FAILED Cause=ModelMissing");
                throw new AssistantProviderUnavailableException(MissingModelErrorMessage);
            }

            var modelExists = File.Exists(resolvedModelPath);
            var modelLength = modelExists ? new FileInfo(resolvedModelPath).Length : (long?)null;
            if (!modelExists)
            {
                var missingModelMessage = $"Modèle introuvable: {resolvedModelPath}";
                UpdateDiagnostics(
                    processRunning: false,
                    portOpen: false,
                    exitCode: null,
                    lastErrorMessage: MissingModelErrorMessage,
                    localStatus: LocalStatus.Failed,
                    failureCategory: "ModelMissing");
                LocalAiFileLog.Write("Arguments: (non construits, modèle manquant)");
                LocalAiFileLog.Write("Process.Start: skipped (modèle manquant)");
                LocalAiFileLog.Write("FAILED Cause=ModelMissing");
                throw new AssistantProviderUnavailableException(missingModelMessage);
            }

            var arguments = BuildArguments(_baseArguments, port, resolvedModelPath);
            var commandLine = BuildCommandLine(_executablePath, arguments);

            Log.Info($"Llama runtime args: {arguments}");
            Log.Info($"Llama runtime command line: {commandLine}");
            LocalAiFileLog.Write($"Arguments: {SanitizeArgumentsForLog(arguments)}");

            var result = await TryStartProcessAsync(arguments, commandLine, ct).ConfigureAwait(false);
            if (!result.Success)
            {
                CleanupTempApiKeyFile();
                if (TryDetectPortInUse(result.LastErrorMessage, result.Stderr))
                {
                    LocalAiFileLog.Write("FAILED Cause=PortInUse");
                }
                throw new AssistantProviderUnavailableException(result.LastErrorMessage ?? "Aucune configuration de sécurité valide trouvée.");
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_process is null)
            {
                return;
            }

            if (!_process.HasExited)
            {
                _process.CloseMainWindow();
                var waitTask = _process.WaitForExitAsync(ct);
                var completed = await Task.WhenAny(waitTask, Task.Delay(DefaultShutdownTimeout, ct)).ConfigureAwait(false);
                if (completed != waitTask)
                {
                    _process.Kill(entireProcessTree: true);
                    await _process.WaitForExitAsync(ct).ConfigureAwait(false);
                }
                else
                {
                    await waitTask.ConfigureAwait(false);
                }
            }
        }
        finally
        {
            _readyConfirmed = false;
            _process?.Dispose();
            _process = null;
            UpdateDiagnostics(processRunning: false, portOpen: false, exitCode: null, lastErrorMessage: null, localStatus: LocalStatus.Stopped);
            _gate.Release();
        }
    }

    public async Task<bool> HealthCheckAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();

        await EnsureProcessRunningAsync(ct).ConfigureAwait(false);

        if (_readyConfirmed && IsProcessRunning())
        {
            return true;
        }

        var healthy = await ProbeHealthAsync(ct).ConfigureAwait(false);
        if (!healthy && _readyConfirmed && IsProcessRunning())
        {
            Log.Warn("Runtime IA local: échec de health check après readiness (connection potentiellement refusée).");
            return true;
        }

        UpdateDiagnostics(
            processRunning: null,
            portOpen: healthy,
            exitCode: null,
            lastErrorMessage: healthy ? string.Empty : "Port fermé ou runtime non prêt.",
            localStatus: healthy ? LocalStatus.Ready : null);
        if (!healthy)
        {
            if (IsProcessRunning())
            {
                return false;
            }

            await EnsureProcessRunningAsync(ct).ConfigureAwait(false);
            healthy = await ProbeHealthAsync(ct).ConfigureAwait(false);
            UpdateDiagnostics(
                processRunning: null,
                portOpen: healthy,
                exitCode: null,
                lastErrorMessage: healthy ? string.Empty : "Port fermé ou runtime non prêt.",
                localStatus: healthy ? LocalStatus.Ready : null);
            return healthy;
        }

        return true;
    }

    private async Task EnsureProcessRunningAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (IsProcessRunning())
            {
                return;
            }

            if (_process is not null)
            {
                _process.Dispose();
                _process = null;
            }
        }
        finally
        {
            _gate.Release();
        }

        await StartAsync(ct).ConfigureAwait(false);
    }

    private async Task<bool> ProbeHealthAsync(CancellationToken ct)
    {
        foreach (var endpoint in ReadinessEndpoints)
        {
            try
            {
                using var response = await _httpClient.GetAsync(endpoint, ct).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
            }
            catch (HttpRequestException)
            {
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
            }
        }

        return false;
    }

    private async Task<ModelsProbeResult> ProbeModelsAsync(CancellationToken ct)
    {
        try
        {
            using var response = await _httpClient.GetAsync(ModelsEndpoint, ct).ConfigureAwait(false);
            var responseContent = await ReadResponseContentAsync(response, ct).ConfigureAwait(false);
            var responseExcerpt = responseContent.Excerpt;
            var modelId = TryExtractModelId(responseContent.Content);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                return new ModelsProbeResult(true, false, response.StatusCode, null, responseExcerpt, modelId, null);
            }

            if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                return new ModelsProbeResult(false, false, response.StatusCode, null, responseExcerpt, modelId, null);
            }

            if ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500)
            {
                return new ModelsProbeResult(
                    false,
                    true,
                    response.StatusCode,
                    $"Runtime IA incompatible: endpoint {ModelsEndpoint} indisponible (HTTP {(int)response.StatusCode}).",
                    responseExcerpt,
                    modelId,
                    null);
            }

            return new ModelsProbeResult(false, false, response.StatusCode, null, responseExcerpt, modelId, null);
        }
        catch (HttpRequestException ex)
        {
            return new ModelsProbeResult(false, false, null, null, null, null, ex.GetBaseException().Message);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            return new ModelsProbeResult(false, false, null, null, null, null, ex.GetBaseException().Message);
        }
    }

    private static async Task<ResponseReadResult> ReadResponseContentAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.Content is null)
        {
            return new ResponseReadResult(null, null);
        }

        try
        {
            var content = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return new ResponseReadResult(content, TruncateForDiagnostics(content, 400));
        }
        catch (Exception)
        {
            return new ResponseReadResult(null, null);
        }
    }

    private static string? TruncateForDiagnostics(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Length <= maxLength ? value : $"{value.Substring(0, maxLength)}…";
    }

    private static string SanitizeRuntimeArguments(string? arguments)
    {
        var sanitizedArguments = arguments ?? string.Empty;
        sanitizedArguments = RemoveArgumentWithValue(sanitizedArguments, "--host");
        sanitizedArguments = RemoveArgumentWithValue(sanitizedArguments, "--port");
        sanitizedArguments = RemoveArgumentWithValue(sanitizedArguments, "--api-key");
        sanitizedArguments = RemoveFlag(sanitizedArguments, "--no-auth");
        sanitizedArguments = RemoveArgumentWithValue(sanitizedArguments, "--api-key-file");
        sanitizedArguments = RemoveArgumentWithValue(sanitizedArguments, "--auth-token");
        sanitizedArguments = RemoveArgumentWithValue(sanitizedArguments, "--token");
        sanitizedArguments = RemoveFlag(sanitizedArguments, "--require-api-key");
        sanitizedArguments = RemoveArgumentWithValue(sanitizedArguments, "--model");
        sanitizedArguments = RemoveFlag(sanitizedArguments, "--ssl");
        sanitizedArguments = RemoveFlag(sanitizedArguments, "--tls");
        sanitizedArguments = RemoveArgumentWithValue(sanitizedArguments, "--cert");
        sanitizedArguments = RemoveArgumentWithValue(sanitizedArguments, "--key");
        sanitizedArguments = RemoveRouterArguments(sanitizedArguments);
        return sanitizedArguments.Trim();
    }

    private static string RemoveArgumentWithValue(string arguments, string flag)
    {
        if (string.IsNullOrWhiteSpace(arguments))
        {
            return string.Empty;
        }

        var pattern = $@"(?<!\S){Regex.Escape(flag)}(?:\s+|=)(\""[^\""]*\""|'[^']*'|\S+)";
        var updated = Regex.Replace(arguments, pattern, string.Empty, RegexOptions.IgnoreCase);
        return NormalizeWhitespace(updated);
    }

    private static string RemoveFlag(string arguments, string flag)
    {
        if (string.IsNullOrWhiteSpace(arguments))
        {
            return string.Empty;
        }

        var pattern = $@"(?<!\S){Regex.Escape(flag)}(?!\S)";
        var updated = Regex.Replace(arguments, pattern, string.Empty, RegexOptions.IgnoreCase);
        return NormalizeWhitespace(updated);
    }

    private static int ResolvePort(string baseUrl)
    {
        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
        {
            return uri.Port;
        }

        return DefaultPort;
    }

    private static string NormalizeWhitespace(string value)
        => string.Join(' ', value.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));

    private static string RemoveRouterArguments(string arguments)
    {
        var updated = arguments;
        foreach (var flag in new[] { "--router", "--rpc", "--proxy", "--multi", "--routes", "--route" })
        {
            updated = RemoveArgumentWithValue(updated, flag);
            updated = RemoveFlag(updated, flag);
        }

        return updated;
    }

    private static string BuildCommandLine(string executablePath, string arguments)
    {
        var safeExecutable = QuoteIfNeeded(executablePath);
        return string.IsNullOrWhiteSpace(arguments) ? safeExecutable : $"{safeExecutable} {arguments}";
    }

    private static string QuoteIfNeeded(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return value.Contains(' ', StringComparison.Ordinal) ? $"\"{value}\"" : value;
    }

    private bool IsProcessRunning()
        => _process is not null && !_process.HasExited;

    private void InitializeDiagnostics(string arguments, string commandLine)
    {
        lock (_outputLock)
        {
            _stdoutBuffer = new StringBuilder();
            _stderrBuffer = new StringBuilder();
        }
        _warningMessage = string.Empty;

        var diagnostics = new LlamaRuntimeDiagnostics(
            _executablePath,
            arguments,
            commandLine,
            _baseUrl,
            _securityFlagsDetected,
            _securityStrategy,
            _warningMessage,
            string.Empty,
            string.Empty,
            null,
            false,
            false,
            null,
            null,
            null,
            null,
            LocalStatus.Starting,
            null,
            null);
        LlamaRuntimeDiagnosticsStore.Set(diagnostics);
    }

    private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
        => AppendOutput(isError: false, e.Data);

    private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
        => AppendOutput(isError: true, e.Data);

    private void AppendOutput(bool isError, string? data)
    {
        if (string.IsNullOrWhiteSpace(data))
        {
            return;
        }

        LocalAiFileLog.Write(isError ? $"STDERR: {data}" : $"STDOUT: {data}");

        if (isError && IsMissingModelLine(data))
        {
            UpdateDiagnostics(
                processRunning: null,
                portOpen: null,
                exitCode: null,
                lastErrorMessage: MissingModelErrorMessage);
        }

        string stdout;
        string stderr;
        lock (_outputLock)
        {
            if (isError)
            {
                AppendOutputLine(_stderrBuffer, data);
                if (IsRuntimeWarningLine(data))
                {
                    _warningMessage = data;
                }
            }
            else
            {
                AppendOutputLine(_stdoutBuffer, data);
            }

            stdout = _stdoutBuffer.ToString();
            stderr = _stderrBuffer.ToString();
        }

        LlamaRuntimeDiagnosticsStore.Update(existing => existing with
        {
            WarningMessage = string.IsNullOrWhiteSpace(_warningMessage)
                ? existing.WarningMessage
                : _warningMessage,
            Stdout = stdout,
            Stderr = stderr
        });
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        if (_process is null)
        {
            return;
        }

        var exitCode = _process.ExitCode;
        var stderr = string.Empty;
        lock (_outputLock)
        {
            stderr = _stderrBuffer.ToString();
        }

        string? errorMessage = null;
        var missingModelError = GetMissingModelErrorMessage(stderr);
        if (!string.IsNullOrWhiteSpace(missingModelError))
        {
            errorMessage = missingModelError;
        }
        else if (exitCode != 0)
        {
            errorMessage = string.IsNullOrWhiteSpace(stderr)
                ? $"Runtime terminé avec le code {exitCode}."
                : $"Runtime terminé avec le code {exitCode}: {GetLastNonWarningLine(stderr)}";
        }

        UpdateDiagnostics(
            processRunning: false,
            portOpen: false,
            exitCode: exitCode,
            lastErrorMessage: errorMessage,
            localStatus: exitCode == 0 ? LocalStatus.Stopped : LocalStatus.Failed);
        _readyConfirmed = false;
    }

    private static void AppendOutputLine(StringBuilder builder, string line)
    {
        if (builder.Length > 0)
        {
            builder.AppendLine();
        }

        builder.Append(line);
    }

    private static string GetLastLine(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var lines = value.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        return lines.Length == 0 ? value : lines[^1];
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

    private static string? ClassifyFailureCategory(string? message, string stderr)
    {
        var combined = $"{message}{Environment.NewLine}{stderr}".Trim();
        if (string.IsNullOrWhiteSpace(combined))
        {
            return null;
        }

        if (combined.Contains("no model will be loaded", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("argument --model", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("modèle", StringComparison.OrdinalIgnoreCase))
        {
            return "ModelMissing";
        }

        if (combined.Contains("address already in use", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("eaddrinuse", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("only one usage", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("bind", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("port already", StringComparison.OrdinalIgnoreCase))
        {
            return "PortInUse";
        }

        if (combined.Contains("endpoint", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("incompatible", StringComparison.OrdinalIgnoreCase))
        {
            return "EndpointUnavailable";
        }

        return null;
    }

    private void UpdateDiagnostics(
        bool? processRunning,
        bool? portOpen,
        int? exitCode,
        string? lastErrorMessage,
        int? lastReadinessHttpStatus = null,
        string? lastModelsResponseExcerpt = null,
        string? lastModelsErrorMessage = null,
        LocalStatus? localStatus = null,
        string? localModelId = null,
        string? failureCategory = null)
    {
        var resolvedLastError = lastErrorMessage is null
            ? LlamaRuntimeDiagnosticsStore.Latest.LastErrorMessage
            : string.IsNullOrWhiteSpace(lastErrorMessage)
                ? null
                : lastErrorMessage;

        if (LlamaRuntimeDiagnosticsStore.Latest.LastErrorMessage == MissingModelErrorMessage
            && !string.IsNullOrWhiteSpace(resolvedLastError)
            && resolvedLastError != MissingModelErrorMessage)
        {
            resolvedLastError = MissingModelErrorMessage;
        }

        var currentDiagnostics = LlamaRuntimeDiagnosticsStore.Latest;
        var resolvedLocalStatus = localStatus ?? currentDiagnostics.LocalStatus;
        var resolvedProcessRunning = processRunning ?? IsProcessRunning();
        if (resolvedProcessRunning
            && (currentDiagnostics.LocalStatus == LocalStatus.Ready || _readyConfirmed)
            && resolvedLocalStatus != LocalStatus.Ready)
        {
            resolvedLocalStatus = LocalStatus.Ready;
        }
        if (resolvedLocalStatus == LocalStatus.Ready)
        {
            _readyConfirmed = true;
        }

        var resolvedFailureCategory = failureCategory == string.Empty
            ? null
            : string.IsNullOrWhiteSpace(failureCategory)
                ? currentDiagnostics.FailureCategory
                : failureCategory;

        if (string.IsNullOrWhiteSpace(resolvedFailureCategory) && !string.IsNullOrWhiteSpace(resolvedLastError))
        {
            resolvedFailureCategory = ClassifyFailureCategory(resolvedLastError, currentDiagnostics.Stderr);
        }
        if (resolvedLocalStatus == LocalStatus.Ready && string.IsNullOrWhiteSpace(resolvedFailureCategory))
        {
            resolvedFailureCategory = FailureCategoryNone;
        }

        var resolvedPortOpen = portOpen ?? currentDiagnostics.PortOpen;
        if (!resolvedProcessRunning && resolvedPortOpen)
        {
            resolvedPortOpen = false;
        }

        LlamaRuntimeDiagnosticsStore.Update(existing => existing with
        {
            ProcessRunning = resolvedProcessRunning,
            PortOpen = resolvedPortOpen,
            ExitCode = exitCode ?? existing.ExitCode,
            SecurityFlagsDetected = string.IsNullOrWhiteSpace(_securityFlagsDetected)
                ? existing.SecurityFlagsDetected
                : _securityFlagsDetected,
            SecurityStrategy = string.IsNullOrWhiteSpace(_securityStrategy)
                ? existing.SecurityStrategy
                : _securityStrategy,
            LastErrorMessage = resolvedLastError,
            LastReadinessHttpStatus = lastReadinessHttpStatus ?? existing.LastReadinessHttpStatus,
            LastModelsResponseExcerpt = lastModelsResponseExcerpt ?? existing.LastModelsResponseExcerpt,
            LastModelsErrorMessage = lastModelsErrorMessage ?? existing.LastModelsErrorMessage,
            LocalStatus = resolvedLocalStatus,
            LocalModelId = string.IsNullOrWhiteSpace(localModelId) ? existing.LocalModelId : localModelId,
            FailureCategory = resolvedFailureCategory
        });
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(LlamaRuntimeManager));
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await StopAsync().ConfigureAwait(false);
        CleanupTempApiKeyFile();
        _httpClient.Dispose();
        _gate.Dispose();
    }

    private async Task<RuntimeAttemptResult> TryStartProcessAsync(string arguments, string commandLine, CancellationToken ct)
    {
        InitializeDiagnostics(arguments, commandLine);
        _process?.Dispose();

        var workingDirectory = Path.GetDirectoryName(_executablePath) ?? AppContext.BaseDirectory;
        var startInfo = new ProcessStartInfo
        {
            FileName = _executablePath,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        try
        {
            _process = _processRunner.Start(startInfo);
        }
        catch (Exception ex)
        {
            UpdateDiagnostics(
                processRunning: false,
                portOpen: false,
                exitCode: null,
                lastErrorMessage: $"Impossible de lancer le runtime: {ex.Message}",
                localStatus: LocalStatus.Failed);
            LocalAiFileLog.Write($"Process.Start: fail ({ex.GetType().Name}: {ex.Message})");
            return new RuntimeAttemptResult(false, null, string.Empty, $"Impossible de lancer le runtime: {ex.Message}", TimedOut: false);
        }

        if (_process is null)
        {
            UpdateDiagnostics(
                processRunning: false,
                portOpen: false,
                exitCode: null,
                lastErrorMessage: "Impossible de lancer le runtime.",
                localStatus: LocalStatus.Failed);
            LocalAiFileLog.Write("Process.Start: fail (process null)");
            return new RuntimeAttemptResult(false, null, string.Empty, "Impossible de lancer le runtime.", TimedOut: false);
        }

        LocalAiFileLog.Write($"Process.Start: success (pid={_process.Id})");

        _process.EnableRaisingEvents = true;
        _process.OutputDataReceived += OnOutputDataReceived;
        _process.ErrorDataReceived += OnErrorDataReceived;
        _process.Exited += OnProcessExited;
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        UpdateDiagnostics(processRunning: true, portOpen: null, exitCode: null, lastErrorMessage: null, localStatus: LocalStatus.Starting);

        await Task.Delay(_startupDelay, ct).ConfigureAwait(false);

        if (_process.HasExited)
        {
            var exitCode = _process.ExitCode;
            var stderr = GetCapturedStderr();
            var missingModelError = GetMissingModelErrorMessage(stderr);
            UpdateDiagnostics(
                processRunning: false,
                portOpen: false,
                exitCode: exitCode,
                lastErrorMessage: missingModelError,
                localStatus: LocalStatus.Failed);
            await StopProcessAsync(ct).ConfigureAwait(false);
            var lastError = !string.IsNullOrWhiteSpace(missingModelError)
                ? missingModelError
                : string.IsNullOrWhiteSpace(stderr)
                    ? $"Runtime terminé avec le code {exitCode}."
                    : $"Runtime terminé avec le code {exitCode}: {GetLastNonWarningLine(stderr)}";
            if (TryDetectPortInUse(lastError, stderr))
            {
                LocalAiFileLog.Write("FAILED Cause=PortInUse");
            }
            return new RuntimeAttemptResult(false, exitCode, stderr, lastError, TimedOut: false);
        }

        var healthy = await ProbeHealthAsync(ct).ConfigureAwait(false);
        UpdateDiagnostics(
            processRunning: true,
            portOpen: healthy,
            exitCode: null,
            lastErrorMessage: healthy ? string.Empty : "Port fermé ou runtime non prêt.",
            localStatus: LocalStatus.Starting);

        var readinessResult = await WaitForReadinessAsync(ct).ConfigureAwait(false);
        if (!readinessResult.Success)
        {
            var stderr = readinessResult.Stderr;
            var exitCode = readinessResult.ExitCode;
            UpdateDiagnostics(
                processRunning: false,
                portOpen: false,
                exitCode: exitCode,
                lastErrorMessage: readinessResult.LastErrorMessage,
                localStatus: LocalStatus.Failed);
            if (exitCode.HasValue || readinessResult.TimedOut || readinessResult.ShouldStopProcess)
            {
                await StopProcessAsync(ct).ConfigureAwait(false);
            }
            return readinessResult;
        }

        return new RuntimeAttemptResult(true, null, string.Empty, null, TimedOut: false);
    }

    private async Task StopProcessAsync(CancellationToken ct)
    {
        if (_process is null)
        {
            return;
        }

        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync(ct).ConfigureAwait(false);
            }
        }
        catch (Exception)
        {
        }
        finally
        {
            _process.Dispose();
            _process = null;
            UpdateDiagnostics(processRunning: false, portOpen: false, exitCode: null, lastErrorMessage: null, localStatus: LocalStatus.Stopped);
            _readyConfirmed = false;
        }
    }

    private string GetCapturedStderr()
    {
        lock (_outputLock)
        {
            return _stderrBuffer.ToString();
        }
    }

    private static string BuildArguments(string baseArguments, int port, string modelPath)
    {
        var builder = new StringBuilder(baseArguments);
        AppendArgument(builder, $"--model {QuoteIfNeeded(modelPath)}");
        AppendArgument(builder, $"--host {LocalHostAddress}");
        AppendArgument(builder, $"--port {port}");
        return builder.ToString().Trim();
    }

    private static void AppendArgument(StringBuilder builder, string argument)
    {
        if (builder.Length > 0 && !char.IsWhiteSpace(builder[^1]))
        {
            builder.Append(' ');
        }

        builder.Append(argument);
    }

    private void CleanupTempApiKeyFile()
    {
        if (string.IsNullOrWhiteSpace(_tempApiKeyFilePath))
        {
            return;
        }

        try
        {
            if (File.Exists(_tempApiKeyFilePath))
            {
                File.Delete(_tempApiKeyFilePath);
            }
        }
        catch (Exception)
        {
        }
        finally
        {
            _tempApiKeyFilePath = null;
        }
    }

    private async Task<RuntimeSecurityConfiguration> DetectSecurityConfigurationAsync(CancellationToken ct)
    {
        var helpResult = await GetHelpOutputAsync(ct).ConfigureAwait(false);
        var helpText = helpResult.HelpText;
        var detectedFlags = DetectFlags(helpText);
        _securityFlagsDetected = detectedFlags.Count == 0 ? "—" : string.Join(", ", detectedFlags);

        if (!string.IsNullOrWhiteSpace(_configuredApiKey) && detectedFlags.Contains("--api-key"))
        {
            return new RuntimeSecurityConfiguration(
                "api-key",
                $"--api-key {QuoteIfNeeded(_configuredApiKey)}",
                _configuredApiKey,
                _securityFlagsDetected,
                null);
        }

        if (!string.IsNullOrWhiteSpace(_configuredApiKey) && detectedFlags.Contains("--api-key-file"))
        {
            var tempFile = CreateTempApiKeyFile(_configuredApiKey);
            if (string.IsNullOrWhiteSpace(tempFile))
            {
                return new RuntimeSecurityConfiguration(
                    "api-key-file",
                    string.Empty,
                    _configuredApiKey,
                    _securityFlagsDetected,
                    "Impossible de créer le fichier temporaire pour --api-key-file.",
                    CanStart: false);
            }

            return new RuntimeSecurityConfiguration(
                "api-key-file",
                $"--api-key-file {QuoteIfNeeded(tempFile)}",
                _configuredApiKey,
                _securityFlagsDetected,
                null);
        }

        if (!string.IsNullOrWhiteSpace(_configuredApiKey) && detectedFlags.Contains("--auth-token"))
        {
            return new RuntimeSecurityConfiguration(
                "auth-token",
                $"--auth-token {QuoteIfNeeded(_configuredApiKey)}",
                _configuredApiKey,
                _securityFlagsDetected,
                null);
        }

        if (!string.IsNullOrWhiteSpace(_configuredApiKey) && detectedFlags.Contains("--token"))
        {
            return new RuntimeSecurityConfiguration(
                "token",
                $"--token {QuoteIfNeeded(_configuredApiKey)}",
                _configuredApiKey,
                _securityFlagsDetected,
                null);
        }

        if (!string.IsNullOrWhiteSpace(_configuredApiKey) && detectedFlags.Contains("--require-api-key"))
        {
            return new RuntimeSecurityConfiguration(
                "require-api-key",
                "--require-api-key",
                _configuredApiKey,
                _securityFlagsDetected,
                null);
        }

        if (string.IsNullOrWhiteSpace(_configuredApiKey) && detectedFlags.Contains("--no-auth"))
        {
            return new RuntimeSecurityConfiguration("no-auth", "--no-auth", null, _securityFlagsDetected, null);
        }

        return new RuntimeSecurityConfiguration(
            string.IsNullOrWhiteSpace(_configuredApiKey) ? "fallback host-only" : "fallback host-only (clé ignorée)",
            string.Empty,
            null,
            _securityFlagsDetected,
            string.IsNullOrWhiteSpace(_configuredApiKey)
                ? null
                : "Clé API configurée mais options d’authentification non supportées.",
            CanStart: true);
    }

    private static Task ValidateRuntimeCompatibilityAsync(CancellationToken ct)
        => Task.CompletedTask;

    private async Task LogRuntimeVersionAsync(CancellationToken ct)
    {
        var versionResult = await RunHelpProcessAsync("--version", ct).ConfigureAwait(false);
        var versionText = BuildHelpText(versionResult.Stdout, versionResult.Stderr);
        if (!string.IsNullOrWhiteSpace(versionText))
        {
            Log.Info($"Llama runtime version: {versionText.Trim()}");
        }
    }

    private async Task<HelpOutputResult> GetHelpOutputAsync(CancellationToken ct)
    {
        var primary = await RunHelpProcessAsync("--help", ct).ConfigureAwait(false);
        if (primary.ExitCode != 0 && string.IsNullOrWhiteSpace(primary.HelpText))
        {
            var fallback = await RunHelpProcessAsync("-h", ct).ConfigureAwait(false);
            return fallback;
        }

        return primary;
    }

    private async Task<HelpOutputResult> RunHelpProcessAsync(string arguments, CancellationToken ct)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _executablePath,
                Arguments = arguments,
                WorkingDirectory = Path.GetDirectoryName(_executablePath) ?? AppContext.BaseDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return new HelpOutputResult(string.Empty, string.Empty, -1, string.Empty);
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);
            var helpText = BuildHelpText(stdout, stderr);
            return new HelpOutputResult(stdout, stderr, process.ExitCode, helpText);
        }
        catch (Exception ex)
        {
            return new HelpOutputResult(string.Empty, ex.Message, -1, ex.Message);
        }
    }

    private static List<string> DetectFlags(string helpText)
    {
        var detected = new List<string>();
        if (ContainsFlag(helpText, "--no-auth"))
        {
            detected.Add("--no-auth");
        }

        if (ContainsFlag(helpText, "--api-key"))
        {
            detected.Add("--api-key");
        }

        if (ContainsFlag(helpText, "--api-key-file"))
        {
            detected.Add("--api-key-file");
        }

        if (ContainsFlag(helpText, "--auth-token"))
        {
            detected.Add("--auth-token");
        }

        if (ContainsFlag(helpText, "--token"))
        {
            detected.Add("--token");
        }

        if (ContainsFlag(helpText, "--require-api-key")
            || helpText.IndexOf("require-api-key", StringComparison.OrdinalIgnoreCase) >= 0
            || helpText.IndexOf("require_api_key", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            detected.Add("--require-api-key");
        }

        return detected;
    }

    private static bool ContainsFlag(string text, string flag)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var pattern = $@"(?<!\S){Regex.Escape(flag)}(?=\s|,|;|$)";
        return Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase);
    }

    private static string BuildHelpText(string stdout, string stderr)
    {
        if (string.IsNullOrWhiteSpace(stdout) && string.IsNullOrWhiteSpace(stderr))
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(stdout))
        {
            return stderr;
        }

        if (string.IsNullOrWhiteSpace(stderr))
        {
            return stdout;
        }

        return $"{stdout}{Environment.NewLine}{stderr}";
    }


    private static string ExtractHelpExcerpt(string helpText, int maxLength = 800)
    {
        if (string.IsNullOrWhiteSpace(helpText))
        {
            return string.Empty;
        }

        var trimmed = helpText.Trim();
        return trimmed.Length <= maxLength
            ? trimmed
            : $"{trimmed.Substring(0, maxLength)}…";
    }

    private string? CreateTempApiKeyFile(string apiKey)
    {
        CleanupTempApiKeyFile();

        try
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"virgil-llama-api-key-{Guid.NewGuid():N}.txt");
            File.WriteAllText(tempFile, apiKey);
            _tempApiKeyFilePath = tempFile;
            return tempFile;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void ConfigureHttpClientAuthHeaders(string? apiKey)
    {
        _httpClient.DefaultRequestHeaders.Authorization = null;
        _httpClient.DefaultRequestHeaders.Remove("X-API-Key");

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return;
        }

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        _httpClient.DefaultRequestHeaders.Add("X-API-Key", apiKey);
    }

    private static bool IsRuntimeWarningLine(string line)
        => line.Contains("untrusted environments", StringComparison.OrdinalIgnoreCase)
            || line.Contains("not recommended", StringComparison.OrdinalIgnoreCase)
            || line.Contains("note:", StringComparison.OrdinalIgnoreCase);

    private static bool IsMissingModelLine(string line)
        => line.Contains(MissingModelStderrMessage, StringComparison.OrdinalIgnoreCase);

    private static string? GetMissingModelErrorMessage(string stderr)
        => string.IsNullOrWhiteSpace(stderr) || !IsMissingModelLine(stderr)
            ? null
            : MissingModelErrorMessage;

    private static string TryExtractModelId(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return string.Empty;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("data", out var dataElement)
                && dataElement.ValueKind == JsonValueKind.Array
                && dataElement.GetArrayLength() > 0)
            {
                var first = dataElement[0];
                if (first.TryGetProperty("id", out var idElement))
                {
                    return idElement.GetString() ?? string.Empty;
                }
            }
        }
        catch (JsonException)
        {
        }

        return string.Empty;
    }

    private async Task<RuntimeAttemptResult> WaitForReadinessAsync(CancellationToken ct)
    {
        var start = DateTimeOffset.UtcNow;
        var delay = TimeSpan.FromMilliseconds(200);
        var maxDelay = TimeSpan.FromMilliseconds(1000);
        var lastStatusCode = (HttpStatusCode?)null;
        string? lastResponseExcerpt = null;
        string? lastEndpoint = null;
        var lastLog = DateTimeOffset.MinValue;
        var attempt = 0;
        var lastProbeException = string.Empty;
        string? lastModelsErrorMessageText = null;

        while (DateTimeOffset.UtcNow - start < _readinessTimeout)
        {
            if (_process is not null && _process.HasExited)
            {
                var exitCode = _process.ExitCode;
                var stderr = GetCapturedStderr();
                var missingModelError = GetMissingModelErrorMessage(stderr);
                var lastError = !string.IsNullOrWhiteSpace(missingModelError)
                    ? missingModelError
                    : string.IsNullOrWhiteSpace(stderr)
                        ? $"Runtime terminé avec le code {exitCode}."
                        : $"Runtime terminé avec le code {exitCode}: {GetLastNonWarningLine(stderr)}";
                return new RuntimeAttemptResult(false, exitCode, stderr, lastError, TimedOut: false);
            }

            attempt++;
            var readinessProbe = await ProbeModelsAsync(ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(readinessProbe.ExceptionMessage))
            {
                lastProbeException = readinessProbe.ExceptionMessage;
            }
            if (!string.IsNullOrWhiteSpace(readinessProbe.ErrorMessage))
            {
                lastModelsErrorMessageText = readinessProbe.ErrorMessage;
            }
            lastStatusCode = readinessProbe.StatusCode;
            lastResponseExcerpt = readinessProbe.ResponseExcerpt;
            lastEndpoint = ModelsEndpoint;
            LocalAiFileLog.Write(BuildReadinessLogEntry(attempt, readinessProbe.StatusCode, readinessProbe.ResponseExcerpt, readinessProbe.ExceptionMessage));
            if (readinessProbe.Incompatible)
            {
                var incompatibleMessage = readinessProbe.ErrorMessage
                    ?? $"Runtime IA incompatible: endpoint {ModelsEndpoint} indisponible.";
                UpdateDiagnostics(
                    processRunning: true,
                    portOpen: false,
                    exitCode: null,
                    lastErrorMessage: incompatibleMessage,
                    lastReadinessHttpStatus: readinessProbe.StatusCode.HasValue ? (int)readinessProbe.StatusCode.Value : null,
                    lastModelsResponseExcerpt: readinessProbe.ResponseExcerpt,
                    lastModelsErrorMessage: readinessProbe.ErrorMessage,
                    localStatus: LocalStatus.Failed,
                    failureCategory: "EndpointUnavailable");
                LocalAiFileLog.Write("FAILED Cause=EndpointUnavailable");
                return new RuntimeAttemptResult(false, null, GetCapturedStderr(), incompatibleMessage, TimedOut: false, ShouldStopProcess: true);
            }

            if (readinessProbe.Ready)
            {
                var warmupDuration = DateTimeOffset.UtcNow - start;
                var readyStatus = lastStatusCode.HasValue
                    ? ((int)lastStatusCode.Value).ToString()
                    : "200";
                Log.Info($"Llama runtime prêt après {warmupDuration.TotalSeconds:0.0}s via {ModelsEndpoint} (HTTP {readyStatus}).");
                UpdateDiagnostics(
                    processRunning: true,
                    portOpen: true,
                    exitCode: null,
                    lastErrorMessage: string.Empty,
                    lastReadinessHttpStatus: readinessProbe.StatusCode.HasValue ? (int)readinessProbe.StatusCode.Value : null,
                    lastModelsResponseExcerpt: readinessProbe.ResponseExcerpt,
                    lastModelsErrorMessage: null,
                    localStatus: LocalStatus.Ready,
                    localModelId: readinessProbe.ModelId,
                    failureCategory: FailureCategoryNone);
                return new RuntimeAttemptResult(true, null, string.Empty, null, TimedOut: false);
            }

            var elapsed = DateTimeOffset.UtcNow - start;
            var remaining = _readinessTimeout - elapsed;
            var remainingSeconds = Math.Max(0, (int)Math.Ceiling(remaining.TotalSeconds));
            var statusLabel = lastStatusCode.HasValue ? ((int)lastStatusCode.Value).ToString() : "aucun";
            var readinessMessage = $"Démarrage IA: chargement du modèle... ~{remainingSeconds}s restantes (dernier HTTP {statusLabel}).";
            UpdateDiagnostics(
                processRunning: true,
                portOpen: false,
                exitCode: null,
                lastErrorMessage: readinessMessage,
                lastReadinessHttpStatus: lastStatusCode.HasValue ? (int)lastStatusCode.Value : null,
                lastModelsResponseExcerpt: lastResponseExcerpt,
                lastModelsErrorMessage: readinessProbe.ErrorMessage,
                localStatus: LocalStatus.Starting);
            if (DateTimeOffset.UtcNow - lastLog >= TimeSpan.FromSeconds(2))
            {
                Log.Info(readinessMessage);
                lastLog = DateTimeOffset.UtcNow;
            }
            await Task.Delay(delay, ct).ConfigureAwait(false);
            delay = delay < TimeSpan.FromMilliseconds(500)
                ? TimeSpan.FromMilliseconds(500)
                : maxDelay;
        }

        var finalStderr = GetCapturedStderr();
        var finalMissingModelError = GetMissingModelErrorMessage(finalStderr);
        var finalWarmup = DateTimeOffset.UtcNow - start;
        var finalStatus = lastStatusCode.HasValue ? ((int)lastStatusCode.Value).ToString() : "aucun";
        var finalEndpoint = string.IsNullOrWhiteSpace(lastEndpoint) ? "aucun" : lastEndpoint;
        Log.Info($"Readiness runtime expiré après {finalWarmup.TotalSeconds:0.0}s (dernier HTTP {finalStatus} sur {finalEndpoint}).");
        if (!string.IsNullOrWhiteSpace(lastModelsErrorMessageText))
        {
            LocalAiFileLog.Write($"Readiness dernière erreur: {lastModelsErrorMessageText}");
        }
        var finalCause = ResolveReadinessFailureCause(lastStatusCode, lastProbeException, finalMissingModelError);
        UpdateDiagnostics(
            processRunning: true,
            portOpen: false,
            exitCode: null,
            lastErrorMessage: finalMissingModelError ?? "Readiness check échoué.",
            lastReadinessHttpStatus: lastStatusCode.HasValue ? (int)lastStatusCode.Value : null,
            lastModelsResponseExcerpt: lastResponseExcerpt,
            localStatus: LocalStatus.Failed,
            failureCategory: finalCause);
        LocalAiFileLog.Write($"FAILED Cause={finalCause}");
        return new RuntimeAttemptResult(false, null, finalStderr, finalMissingModelError ?? "Readiness check échoué.", TimedOut: true);
    }

    private sealed record RuntimeSecurityConfiguration(
        string StrategyLabel,
        string Arguments,
        string? ApiKey,
        string FlagsDetected,
        string? ErrorMessage,
        bool CanStart = true);

    private sealed record HelpOutputResult(string Stdout, string Stderr, int ExitCode, string HelpText = "");

    private sealed record RuntimeAttemptResult(bool Success, int? ExitCode, string Stderr, string? LastErrorMessage, bool TimedOut, bool ShouldStopProcess = false);

    private sealed record ModelsProbeResult(
        bool Ready,
        bool Incompatible,
        HttpStatusCode? StatusCode,
        string? ErrorMessage,
        string? ResponseExcerpt,
        string? ModelId,
        string? ExceptionMessage);

    private sealed record ResponseReadResult(string? Content, string? Excerpt);

    private static string SanitizeArgumentsForLog(string arguments)
    {
        var sanitized = arguments;
        sanitized = RedactArgumentValue(sanitized, "--api-key");
        sanitized = RedactArgumentValue(sanitized, "--api-key-file");
        sanitized = RedactArgumentValue(sanitized, "--auth-token");
        sanitized = RedactArgumentValue(sanitized, "--token");
        sanitized = RedactArgumentValue(sanitized, "--cert");
        sanitized = RedactArgumentValue(sanitized, "--key");
        return sanitized;
    }

    private static string RedactArgumentValue(string arguments, string flag)
    {
        if (string.IsNullOrWhiteSpace(arguments))
        {
            return string.Empty;
        }

        var pattern = $@"(?<!\S){Regex.Escape(flag)}(?:\s+|=)(\""[^\""]*\""|'[^']*'|\S+)";
        var updated = Regex.Replace(arguments, pattern, $"{flag} <redacted>", RegexOptions.IgnoreCase);
        return NormalizeWhitespace(updated);
    }

    private static string BuildReadinessLogEntry(int attempt, HttpStatusCode? statusCode, string? responseExcerpt, string? exceptionMessage)
    {
        var status = statusCode.HasValue ? ((int)statusCode.Value).ToString() : "aucun";
        var firstLine = GetFirstLine(responseExcerpt);
        if (!string.IsNullOrWhiteSpace(exceptionMessage))
        {
            return $"Readiness GET {ModelsEndpoint} attempt={attempt} exception={exceptionMessage}";
        }

        return string.IsNullOrWhiteSpace(firstLine)
            ? $"Readiness GET {ModelsEndpoint} attempt={attempt} status={status}"
            : $"Readiness GET {ModelsEndpoint} attempt={attempt} status={status} response=\"{firstLine}\"";
    }

    private static string? GetFirstLine(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var lines = value.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        return lines.Length == 0 ? value : lines[0];
    }

    private static bool TryDetectPortInUse(string? message, string stderr)
    {
        var combined = $"{message}{Environment.NewLine}{stderr}".Trim();
        if (string.IsNullOrWhiteSpace(combined))
        {
            return false;
        }

        return combined.Contains("address already in use", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("eaddrinuse", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("bind", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("port already", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveReadinessFailureCause(HttpStatusCode? statusCode, string? exceptionMessage, string? missingModelError)
    {
        if (!string.IsNullOrWhiteSpace(missingModelError))
        {
            return "ModelMissing";
        }

        if (!string.IsNullOrWhiteSpace(exceptionMessage))
        {
            return "EndpointUnavailable";
        }

        if (statusCode == HttpStatusCode.NotFound)
        {
            return "EndpointUnavailable";
        }

        return "TimeoutLoading";
    }
}
