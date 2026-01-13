using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
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
            var attempts = BuildSecurityArgumentAttempts();
            RuntimeAttemptResult? lastFailure = null;

            foreach (var attempt in attempts)
            {
                ct.ThrowIfCancellationRequested();

                var arguments = BuildArguments(_baseArguments, port, attempt.Arguments);
                var commandLine = BuildCommandLine(_executablePath, arguments);

                Log.Info($"Llama runtime args attempt ({attempt.Label}): {arguments}");
                Log.Info($"Llama runtime command line: {commandLine}");

                var result = await TryStartProcessAsync(arguments, commandLine, ct).ConfigureAwait(false);
                if (result.Success)
                {
                    Log.Info($"Llama runtime security config selected: {attempt.Label}");
                    return;
                }

                if (!string.IsNullOrWhiteSpace(result.Stderr))
                {
                    Log.Warn($"Llama runtime config rejected ({attempt.Label}) stderr: {result.Stderr}");
                }

                lastFailure = result;
            }

            CleanupTempApiKeyFile();
            var lastError = lastFailure?.LastErrorMessage ?? "Aucune configuration de sécurité valide trouvée.";
            throw new AssistantProviderUnavailableException(lastError);
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

    private IEnumerable<RuntimeSecurityAttempt> BuildSecurityArgumentAttempts()
    {
        yield return new RuntimeSecurityAttempt("no-auth", "--no-auth");
        yield return new RuntimeSecurityAttempt("api-key none", "--api-key none");
        yield return new RuntimeSecurityAttempt("api-key empty", "--api-key \"\"");

        var tempFile = GetOrCreateTempApiKeyFile();
        if (!string.IsNullOrWhiteSpace(tempFile))
        {
            yield return new RuntimeSecurityAttempt("api-key-file empty", $"--api-key-file {QuoteIfNeeded(tempFile)}");
        }
        else
        {
            Log.Warn("Impossible de créer le fichier temporaire pour --api-key-file.");
        }
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

    private string? GetOrCreateTempApiKeyFile()
    {
        if (!string.IsNullOrWhiteSpace(_tempApiKeyFilePath))
        {
            return _tempApiKeyFilePath;
        }

        try
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"virgil-llama-api-key-{Guid.NewGuid():N}.txt");
            File.WriteAllText(tempFile, string.Empty);
            _tempApiKeyFilePath = tempFile;
            return tempFile;
        }
        catch (Exception)
        {
            return null;
        }
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

    private sealed record RuntimeSecurityAttempt(string Label, string Arguments);

    private sealed record RuntimeAttemptResult(bool Success, int? ExitCode, string Stderr, string? LastErrorMessage);
}
