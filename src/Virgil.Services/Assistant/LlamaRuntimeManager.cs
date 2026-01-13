using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
    private static readonly TimeSpan DefaultShutdownTimeout = TimeSpan.FromSeconds(5);
    private const string LocalHostAddress = "127.0.0.1";
    private const int DefaultPort = 8080;
    private const string RuntimeExecutableName = "llama-server.exe";
    private const string DefaultApiKey = "virgil";
    private readonly string _baseUrl;
    private readonly string _executablePath;
    private readonly string _baseArguments;
    private readonly HttpClient _httpClient;
    private readonly TimeSpan _healthTimeout;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _outputLock = new();
    private Process? _process;
    private StringBuilder _stdoutBuffer = new();
    private StringBuilder _stderrBuffer = new();
    private string? _tempApiKeyFilePath;
    private string? _apiKey;
    private string _securityFlagsDetected = string.Empty;
    private string _securityStrategy = string.Empty;
    private bool _disposed;

    public LlamaRuntimeManager(
        string baseUrl,
        string? executablePath = null,
        string? arguments = null,
        TimeSpan? healthTimeout = null,
        HttpClient? httpClient = null)
    {
        _baseUrl = baseUrl;
        _executablePath = string.IsNullOrWhiteSpace(executablePath)
            ? DefaultRuntimePath
            : executablePath;
        _baseArguments = SanitizeRuntimeArguments(arguments);
        _healthTimeout = healthTimeout ?? DefaultHealthTimeout;

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
                lastErrorMessage: securityConfig.ErrorMessage);

            if (!securityConfig.CanStart)
            {
                throw new AssistantProviderUnavailableException(securityConfig.ErrorMessage ?? "Aucune configuration de sécurité valide trouvée.");
            }

            var arguments = BuildArguments(_baseArguments, port, securityConfig.Arguments);
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
            lastErrorMessage: healthy ? null : "Port fermé ou runtime non prêt.");
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
                lastErrorMessage: healthy ? null : "Port fermé ou runtime non prêt.");
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
        try
        {
            using var response = await _httpClient.GetAsync("/health", ct).ConfigureAwait(false);
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

        try
        {
            using var response = await _httpClient.GetAsync("/v1/models", ct).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
        }

        return false;
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

        var diagnostics = new LlamaRuntimeDiagnostics(
            _executablePath,
            arguments,
            commandLine,
            _securityFlagsDetected,
            _securityStrategy,
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

        string stdout;
        string stderr;
        lock (_outputLock)
        {
            if (isError)
            {
                AppendOutputLine(_stderrBuffer, data);
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
        if (exitCode != 0 || !string.IsNullOrWhiteSpace(stderr))
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
            LastErrorMessage = string.IsNullOrWhiteSpace(lastErrorMessage)
                ? existing.LastErrorMessage
                : lastErrorMessage
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
            return new RuntimeAttemptResult(false, null, string.Empty, $"Impossible de lancer le runtime: {ex.Message}");
        }

        if (_process is null)
        {
            UpdateDiagnostics(
                processLaunched: false,
                portOpen: false,
                exitCode: null,
                lastErrorMessage: "Impossible de lancer le runtime.");
            return new RuntimeAttemptResult(false, null, string.Empty, "Impossible de lancer le runtime.");
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
            UpdateDiagnostics(processLaunched: false, portOpen: false, exitCode: exitCode, lastErrorMessage: null);
            await StopProcessAsync(ct).ConfigureAwait(false);
            var lastError = string.IsNullOrWhiteSpace(stderr)
                ? $"Runtime terminé avec le code {exitCode}."
                : $"Runtime terminé avec le code {exitCode}: {GetLastLine(stderr)}";
            return new RuntimeAttemptResult(false, exitCode, stderr, lastError);
        }

        var healthy = await ProbeHealthAsync(ct).ConfigureAwait(false);
        UpdateDiagnostics(processLaunched: true, portOpen: healthy, exitCode: null, lastErrorMessage: healthy ? null : "Port fermé ou runtime non prêt.");
        if (!healthy)
        {
            var stderr = GetCapturedStderr();
            await StopProcessAsync(ct).ConfigureAwait(false);
            return new RuntimeAttemptResult(false, null, stderr, "Port fermé ou runtime non prêt.");
        }

        return new RuntimeAttemptResult(true, null, string.Empty, null);
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

    private static string BuildArguments(string baseArguments, int port, string securityArguments)
    {
        var builder = new StringBuilder(baseArguments);
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

        if (detectedFlags.Contains("--no-auth"))
        {
            return new RuntimeSecurityConfiguration("no-auth", "--no-auth", null, _securityFlagsDetected, null);
        }

        if (detectedFlags.Contains("--api-key"))
        {
            return new RuntimeSecurityConfiguration(
                "api-key",
                $"--api-key {QuoteIfNeeded(DefaultApiKey)}",
                DefaultApiKey,
                _securityFlagsDetected,
                null);
        }

        if (detectedFlags.Contains("--api-key-file"))
        {
            var apiKey = GenerateApiKey();
            var tempFile = CreateTempApiKeyFile(apiKey);
            if (string.IsNullOrWhiteSpace(tempFile))
            {
                return new RuntimeSecurityConfiguration(
                    "api-key-file",
                    string.Empty,
                    apiKey,
                    _securityFlagsDetected,
                    "Impossible de créer le fichier temporaire pour --api-key-file.",
                    CanStart: false);
            }

            return new RuntimeSecurityConfiguration(
                "api-key-file",
                $"--api-key-file {QuoteIfNeeded(tempFile)}",
                apiKey,
                _securityFlagsDetected,
                null);
        }

        if (detectedFlags.Contains("--auth-token"))
        {
            var apiKey = GenerateApiKey();
            return new RuntimeSecurityConfiguration(
                "auth-token",
                $"--auth-token {QuoteIfNeeded(apiKey)}",
                apiKey,
                _securityFlagsDetected,
                null);
        }

        if (detectedFlags.Contains("--token"))
        {
            var apiKey = GenerateApiKey();
            return new RuntimeSecurityConfiguration(
                "token",
                $"--token {QuoteIfNeeded(apiKey)}",
                apiKey,
                _securityFlagsDetected,
                null);
        }

        if (detectedFlags.Contains("--require-api-key"))
        {
            var apiKey = GenerateApiKey();
            return new RuntimeSecurityConfiguration(
                "require-api-key",
                "--require-api-key",
                apiKey,
                _securityFlagsDetected,
                null);
        }

        return new RuntimeSecurityConfiguration(
            "fallback host-only",
            string.Empty,
            null,
            _securityFlagsDetected,
            "Impossible d’activer un mode sécurisé: flags non trouvés",
            CanStart: true);
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

    private string GenerateApiKey()
        => $"virgil-{Guid.NewGuid():N}";

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

    private sealed record RuntimeSecurityConfiguration(
        string StrategyLabel,
        string Arguments,
        string? ApiKey,
        string FlagsDetected,
        string? ErrorMessage,
        bool CanStart = true);

    private sealed record HelpOutputResult(string Stdout, string Stderr, int ExitCode, string HelpText = "");

    private sealed record RuntimeAttemptResult(bool Success, int? ExitCode, string Stderr, string? LastErrorMessage);
}
