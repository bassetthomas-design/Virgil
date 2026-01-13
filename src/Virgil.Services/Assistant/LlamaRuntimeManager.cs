using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Virgil.Core.Logging;

namespace Virgil.Services.Assistant;

public sealed class LlamaRuntimeManager : IAsyncDisposable, ILocalLlmRuntime
{
    private static readonly TimeSpan DefaultHealthTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DefaultShutdownTimeout = TimeSpan.FromSeconds(5);
    private const string RuntimeExecutableName = "llama-server.exe";
    private readonly string _baseUrl;
    private readonly string _executablePath;
    private readonly string? _arguments;
    private readonly HttpClient _httpClient;
    private readonly TimeSpan _healthTimeout;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Process? _process;
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
        _arguments = arguments;
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

            var workingDirectory = Path.GetDirectoryName(_executablePath) ?? AppContext.BaseDirectory;
            var startInfo = new ProcessStartInfo
            {
                FileName = _executablePath,
                Arguments = _arguments ?? string.Empty,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            _process?.Dispose();
            _process = Process.Start(startInfo);
            if (_process is null)
            {
                throw new AssistantProviderUnavailableException("Unable to start Llama runtime.");
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

        if (!await ProbeHealthAsync(ct).ConfigureAwait(false))
        {
            if (IsProcessRunning())
            {
                return false;
            }

            await EnsureProcessRunningAsync(ct).ConfigureAwait(false);
            return await ProbeHealthAsync(ct).ConfigureAwait(false);
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

    private bool IsProcessRunning()
        => _process is not null && !_process.HasExited;

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
