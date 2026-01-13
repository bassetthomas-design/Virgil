using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
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
    private readonly string? _arguments;
    private readonly HttpClient _httpClient;
    private readonly TimeSpan _healthTimeout;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _outputLock = new();
    private Process? _process;
    private StringBuilder _stdoutBuffer = new();
    private StringBuilder _stderrBuffer = new();
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
        _arguments = EnsureSecurityArguments(arguments, baseUrl);
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
            Log.Info($"Llama runtime args: {_arguments ?? string.Empty}");

            InitializeDiagnostics();

            var workingDirectory = Path.GetDirectoryName(_executablePath) ?? AppContext.BaseDirectory;
            var startInfo = new ProcessStartInfo
            {
                FileName = _executablePath,
                Arguments = _arguments ?? string.Empty,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            _process?.Dispose();
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
                throw new AssistantProviderUnavailableException($"Impossible de lancer le runtime: {ex.Message}", ex);
            }

            if (_process is null)
            {
                UpdateDiagnostics(
                    processLaunched: false,
                    portOpen: false,
                    exitCode: null,
                    lastErrorMessage: "Impossible de lancer le runtime.");
                throw new AssistantProviderUnavailableException("Impossible de lancer le runtime.");
            }

            _process.EnableRaisingEvents = true;
            _process.OutputDataReceived += OnOutputDataReceived;
            _process.ErrorDataReceived += OnErrorDataReceived;
            _process.Exited += OnProcessExited;
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            UpdateDiagnostics(processLaunched: true, portOpen: null, exitCode: null, lastErrorMessage: null);
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
            using var response = await _httpClient.GetAsync("/", ct).ConfigureAwait(false);
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

    private static string EnsureSecurityArguments(string? arguments, string baseUrl)
    {
        var sanitizedArguments = arguments ?? string.Empty;
        var hasHost = ContainsArgument(sanitizedArguments, "--host");
        var hasPort = ContainsArgument(sanitizedArguments, "--port");
        var hasAuthFlag = ContainsArgument(sanitizedArguments, "--no-auth")
            || ContainsArgument(sanitizedArguments, "--api-key");

        var port = ResolvePort(baseUrl);
        var builder = new StringBuilder(sanitizedArguments);

        AppendArgumentIfMissing(builder, hasHost, $"--host {LocalHostAddress}");
        AppendArgumentIfMissing(builder, hasPort, $"--port {port}");
        AppendArgumentIfMissing(builder, hasAuthFlag, "--api-key none");

        return builder.ToString().Trim();
    }

    private static int ResolvePort(string baseUrl)
    {
        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
        {
            return uri.Port;
        }

        return DefaultPort;
    }

    private static bool ContainsArgument(string arguments, string flag)
        => arguments.Contains(flag, StringComparison.OrdinalIgnoreCase);

    private static void AppendArgumentIfMissing(StringBuilder builder, bool hasArgument, string argument)
    {
        if (hasArgument)
        {
            return;
        }

        if (builder.Length > 0 && !char.IsWhiteSpace(builder[^1]))
        {
            builder.Append(' ');
        }

        builder.Append(argument);
    }

    private bool IsProcessRunning()
        => _process is not null && !_process.HasExited;

    private void InitializeDiagnostics()
    {
        lock (_outputLock)
        {
            _stdoutBuffer = new StringBuilder();
            _stderrBuffer = new StringBuilder();
        }

        var diagnostics = new LlamaRuntimeDiagnostics(
            _executablePath,
            _arguments ?? string.Empty,
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
                AppendCapped(_stderrBuffer, data);
            }
            else
            {
                AppendCapped(_stdoutBuffer, data);
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

    private static void AppendCapped(StringBuilder builder, string line)
    {
        const int maxChars = 8000;
        if (builder.Length > 0)
        {
            builder.AppendLine();
        }

        builder.Append(line);
        if (builder.Length <= maxChars)
        {
            return;
        }

        builder.Remove(0, builder.Length - maxChars);
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
        _httpClient.Dispose();
        _gate.Dispose();
    }
}
