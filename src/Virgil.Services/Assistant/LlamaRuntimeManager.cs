using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
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
    private const string LocalHostAddress = "127.0.0.1";
    private const int DefaultPort = 8080;
    private const string RuntimeExecutableName = "llama-server.exe";
    private const string MissingModelStderrMessage = "no model will be loaded in this process";
    private const string MissingModelErrorMessage = "Modèle non chargé: argument --model manquant.";
    private static readonly string[] ReadinessEndpoints = { "/health", "/v1/health", "/v1/models" };
    private static readonly string[] CompatibilityMarkers =
    {
        "/v1/chat/completions",
        "OpenAI",
        "chat/completions",
        "v1/models"
    };
    private readonly string _baseUrl;
    private readonly string _executablePath;
    private readonly string _baseArguments;
    private readonly string? _configuredApiKey;
    private readonly HttpClient _httpClient;
    private readonly TimeSpan _healthTimeout;
    private readonly TimeSpan _readinessTimeout;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _outputLock = new();
    private Process? _process;
    private StringBuilder _stdoutBuffer = new();
    private StringBuilder _stderrBuffer = new();
    private string? _tempApiKeyFilePath;
    private string? _apiKey;
    private string? _modelPath;
    private string _securityFlagsDetected = string.Empty;
    private string _securityStrategy = string.Empty;
    private string _warningMessage = string.Empty;
    private bool _disposed;

    public LlamaRuntimeManager(
        string baseUrl,
        string? executablePath = null,
        string? arguments = null,
        string? apiKey = null,
        TimeSpan? healthTimeout = null,
        TimeSpan? readinessTimeout = null,
        HttpClient? httpClient = null)
    {
        _baseUrl = baseUrl;
        _executablePath = string.IsNullOrWhiteSpace(executablePath)
            ? DefaultRuntimePath
            : executablePath;
        _baseArguments = SanitizeRuntimeArguments(arguments);
        _configuredApiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim();
        _healthTimeout = healthTimeout ?? DefaultHealthTimeout;
        _readinessTimeout = readinessTimeout ?? DefaultReadinessTimeout;

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

            if (!IsRuntimeAvailable())
            {
                throw new AssistantProviderUnavailableException($"Llama runtime not found at '{_executablePath}'.");
            }

            await ValidateRuntimeCompatibilityAsync(ct).ConfigureAwait(false);
            await LogRuntimeVersionAsync(ct).ConfigureAwait(false);

            Log.Info($"Llama runtime path: {_executablePath}");
            var port = ResolvePort(_baseUrl);
            var securityConfig = await DetectSecurityConfigurationAsync(ct).ConfigureAwait(false);
            _securityFlagsDetected = securityConfig.FlagsDetected;
            _securityStrategy = securityConfig.StrategyLabel;
            _apiKey = securityConfig.ApiKey;
            ConfigureHttpClientAuthHeaders(_apiKey);

            UpdateDiagnostics(
                processLaunched: null,
                portOpen: null,
                exitCode: null,
                lastErrorMessage: securityConfig.CanStart ? string.Empty : securityConfig.ErrorMessage);

            if (!securityConfig.CanStart)
            {
                throw new AssistantProviderUnavailableException(securityConfig.ErrorMessage ?? "Aucune configuration de sécurité valide trouvée.");
            }

            var modelPath = _modelPath;
            if (string.IsNullOrWhiteSpace(modelPath))
            {
                UpdateDiagnostics(processLaunched: false, portOpen: false, exitCode: null, lastErrorMessage: MissingModelErrorMessage);
                throw new AssistantProviderUnavailableException(MissingModelErrorMessage);
            }

            var arguments = BuildArguments(_baseArguments, port, securityConfig.Arguments, modelPath);
            var commandLine = BuildCommandLine(_executablePath, arguments);

            Log.Info($"Llama runtime security config selected: {securityConfig.StrategyLabel}");
            Log.Info($"Llama runtime args: {arguments}");
            Log.Info($"Llama runtime command line: {commandLine}");

            var result = await TryStartProcessAsync(arguments, commandLine, ct).ConfigureAwait(false);
            if (!result.Success)
            {
                CleanupTempApiKeyFile();
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
            _process?.Dispose();
            _process = null;
            _gate.Release();
        }
    }

    public async Task<bool> HealthCheckAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();

        await EnsureProcessRunningAsync(ct).ConfigureAwait(false);

        var healthy = await ProbeHealthAsync(ct).ConfigureAwait(false);
        UpdateDiagnostics(
            processLaunched: null,
            portOpen: healthy,
            exitCode: null,
            lastErrorMessage: healthy ? string.Empty : "Port fermé ou runtime non prêt.");
        if (!healthy)
        {
            if (IsProcessRunning())
            {
                return false;
            }

            await EnsureProcessRunningAsync(ct).ConfigureAwait(false);
            healthy = await ProbeHealthAsync(ct).ConfigureAwait(false);
            UpdateDiagnostics(
                processLaunched: null,
                portOpen: healthy,
                exitCode: null,
                lastErrorMessage: healthy ? string.Empty : "Port fermé ou runtime non prêt.");
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

    private async Task<ReadinessProbeResult> ProbeReadinessAsync(CancellationToken ct)
    {
        HttpStatusCode? lastStatus = null;
        string? lastEndpoint = null;

        foreach (var endpoint in ReadinessEndpoints)
        {
            lastEndpoint = endpoint;
            try
            {
                using var response = await _httpClient.GetAsync(endpoint, ct).ConfigureAwait(false);
                lastStatus = response.StatusCode;
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return new ReadinessProbeResult(true, endpoint, response.StatusCode);
                }

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    continue;
                }

                if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
                {
                    continue;
                }
            }
            catch (HttpRequestException)
            {
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
            }
        }

        return new ReadinessProbeResult(false, lastEndpoint, lastStatus);
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
            _securityFlagsDetected,
            _securityStrategy,
            _warningMessage,
            string.Empty,
            string.Empty,
            null,
            false,
            false,
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

        if (isError && IsMissingModelLine(data))
        {
            UpdateDiagnostics(
                processLaunched: null,
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
                : $"Runtime terminé avec le code {exitCode}: {GetLastLine(stderr)}";
        }

        UpdateDiagnostics(
            processLaunched: false,
            portOpen: false,
            exitCode: exitCode,
            lastErrorMessage: errorMessage);
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

    private void UpdateDiagnostics(bool? processLaunched, bool? portOpen, int? exitCode, string? lastErrorMessage)
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

        LlamaRuntimeDiagnosticsStore.Update(existing => existing with
        {
            ProcessLaunched = processLaunched ?? existing.ProcessLaunched,
            PortOpen = portOpen ?? existing.PortOpen,
            ExitCode = exitCode ?? existing.ExitCode,
            SecurityFlagsDetected = string.IsNullOrWhiteSpace(_securityFlagsDetected)
                ? existing.SecurityFlagsDetected
                : _securityFlagsDetected,
            SecurityStrategy = string.IsNullOrWhiteSpace(_securityStrategy)
                ? existing.SecurityStrategy
                : _securityStrategy,
            LastErrorMessage = resolvedLastError
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
            _process = Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            UpdateDiagnostics(
                processLaunched: false,
                portOpen: false,
                exitCode: null,
                lastErrorMessage: $"Impossible de lancer le runtime: {ex.Message}");
            return new RuntimeAttemptResult(false, null, string.Empty, $"Impossible de lancer le runtime: {ex.Message}", TimedOut: false);
        }

        if (_process is null)
        {
            UpdateDiagnostics(
                processLaunched: false,
                portOpen: false,
                exitCode: null,
                lastErrorMessage: "Impossible de lancer le runtime.");
            return new RuntimeAttemptResult(false, null, string.Empty, "Impossible de lancer le runtime.", TimedOut: false);
        }

        _process.EnableRaisingEvents = true;
        _process.OutputDataReceived += OnOutputDataReceived;
        _process.ErrorDataReceived += OnErrorDataReceived;
        _process.Exited += OnProcessExited;
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        UpdateDiagnostics(processLaunched: true, portOpen: null, exitCode: null, lastErrorMessage: null);

        await Task.Delay(TimeSpan.FromMilliseconds(1500), ct).ConfigureAwait(false);

        if (_process.HasExited)
        {
            var exitCode = _process.ExitCode;
            var stderr = GetCapturedStderr();
            var missingModelError = GetMissingModelErrorMessage(stderr);
            UpdateDiagnostics(
                processLaunched: false,
                portOpen: false,
                exitCode: exitCode,
                lastErrorMessage: missingModelError);
            await StopProcessAsync(ct).ConfigureAwait(false);
            var lastError = !string.IsNullOrWhiteSpace(missingModelError)
                ? missingModelError
                : string.IsNullOrWhiteSpace(stderr)
                    ? $"Runtime terminé avec le code {exitCode}."
                    : $"Runtime terminé avec le code {exitCode}: {GetLastLine(stderr)}";
            return new RuntimeAttemptResult(false, exitCode, stderr, lastError, TimedOut: false);
        }

        var healthy = await ProbeHealthAsync(ct).ConfigureAwait(false);
        UpdateDiagnostics(processLaunched: true, portOpen: healthy, exitCode: null, lastErrorMessage: healthy ? string.Empty : "Port fermé ou runtime non prêt.");
        if (!healthy)
        {
            var readinessResult = await WaitForReadinessAsync(ct).ConfigureAwait(false);
            if (!readinessResult.Success)
            {
                var stderr = readinessResult.Stderr;
                var exitCode = readinessResult.ExitCode;
                UpdateDiagnostics(
                    processLaunched: false,
                    portOpen: false,
                    exitCode: exitCode,
                    lastErrorMessage: readinessResult.LastErrorMessage);
                if (exitCode.HasValue || readinessResult.TimedOut)
                {
                    await StopProcessAsync(ct).ConfigureAwait(false);
                }
                return readinessResult;
            }
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
        }
    }

    private string GetCapturedStderr()
    {
        lock (_outputLock)
        {
            return _stderrBuffer.ToString();
        }
    }

    private static string BuildArguments(string baseArguments, int port, string securityArguments, string modelPath)
    {
        var builder = new StringBuilder(baseArguments);
        AppendArgument(builder, $"--model {QuoteIfNeeded(modelPath)}");
        AppendArgument(builder, $"--host {LocalHostAddress}");
        AppendArgument(builder, $"--port {port}");
        AppendArgument(builder, securityArguments);
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

    private async Task ValidateRuntimeCompatibilityAsync(CancellationToken ct)
    {
        var helpResult = await GetHelpOutputAsync(ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(helpResult.HelpText))
        {
            var message = "Runtime IA incompatible: impossible de lire l’aide du binaire (llama-server.exe).";
            UpdateDiagnostics(processLaunched: false, portOpen: false, exitCode: helpResult.ExitCode, lastErrorMessage: message);
            throw new AssistantProviderUnavailableException(message);
        }

        if (!ContainsCompatibilityMarker(helpResult.HelpText))
        {
            var message = "Runtime IA incompatible: l’aide ne mentionne pas l’API OpenAI (/v1/chat/completions, v1/models, etc.).";
            UpdateDiagnostics(processLaunched: false, portOpen: false, exitCode: helpResult.ExitCode, lastErrorMessage: message);
            throw new AssistantProviderUnavailableException(message);
        }
    }

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

    private static bool ContainsCompatibilityMarker(string helpText)
    {
        foreach (var marker in CompatibilityMarkers)
        {
            if (helpText.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
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

    private async Task<RuntimeAttemptResult> WaitForReadinessAsync(CancellationToken ct)
    {
        var start = DateTimeOffset.UtcNow;
        var delay = TimeSpan.FromMilliseconds(200);
        var maxDelay = TimeSpan.FromMilliseconds(1000);
        var lastStatusCode = (HttpStatusCode?)null;
        string? lastEndpoint = null;
        var lastLog = DateTimeOffset.MinValue;

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
                        : $"Runtime terminé avec le code {exitCode}: {GetLastLine(stderr)}";
                return new RuntimeAttemptResult(false, exitCode, stderr, lastError, TimedOut: false);
            }

            var readinessProbe = await ProbeReadinessAsync(ct).ConfigureAwait(false);
            lastStatusCode = readinessProbe.StatusCode;
            lastEndpoint = readinessProbe.Endpoint;
            if (readinessProbe.Ready)
            {
                var warmupDuration = DateTimeOffset.UtcNow - start;
                var readyStatus = readinessProbe.StatusCode.HasValue
                    ? ((int)readinessProbe.StatusCode.Value).ToString()
                    : "200";
                Log.Info($"Llama runtime prêt après {warmupDuration.TotalSeconds:0.0}s via {readinessProbe.Endpoint} (HTTP {readyStatus}).");
                UpdateDiagnostics(processLaunched: true, portOpen: true, exitCode: null, lastErrorMessage: string.Empty);
                return new RuntimeAttemptResult(true, null, string.Empty, null, TimedOut: false);
            }

            var elapsed = DateTimeOffset.UtcNow - start;
            var remaining = _readinessTimeout - elapsed;
            var remainingSeconds = Math.Max(0, (int)Math.Ceiling(remaining.TotalSeconds));
            var statusLabel = lastStatusCode.HasValue ? ((int)lastStatusCode.Value).ToString() : "aucun";
            var readinessMessage = $"Démarrage IA: chargement du modèle... ~{remainingSeconds}s restantes (dernier HTTP {statusLabel}).";
            UpdateDiagnostics(processLaunched: true, portOpen: false, exitCode: null, lastErrorMessage: readinessMessage);
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

    private sealed record RuntimeAttemptResult(bool Success, int? ExitCode, string Stderr, string? LastErrorMessage, bool TimedOut);

    private sealed record ReadinessProbeResult(bool Ready, string? Endpoint, HttpStatusCode? StatusCode);
}
