using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Virgil.Services.Assistant;
using Xunit;

namespace Virgil.Tests;

public class LlamaRuntimeReadinessTests
{
    [Fact]
    public async Task StartAsync_WaitsForReadinessWith503Retries()
    {
        var handler = new SequencedHandler();
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:12345"),
            Timeout = TimeSpan.FromMilliseconds(100)
        };

        var runtimePath = Path.GetTempFileName();
        var modelPath = Path.GetTempFileName();
        try
        {
            var runtime = new LlamaRuntimeManager(
                "http://localhost:12345",
                runtimePath,
                apiKey: null,
                healthTimeout: TimeSpan.FromMilliseconds(50),
                readinessTimeout: TimeSpan.FromSeconds(2),
                httpClient: httpClient,
                processRunner: new FakeProcessRunner(),
                skipCompatibilityCheck: true,
                startupDelay: TimeSpan.FromMilliseconds(10));

            runtime.SetModelPath(modelPath);

            await runtime.StartAsync();

            var healthy = await runtime.HealthCheckAsync();

            Assert.True(healthy);
            Assert.True(handler.ModelsCallCount >= 2);
        }
        finally
        {
            File.Delete(runtimePath);
            File.Delete(modelPath);
        }
    }

    private sealed class SequencedHandler : HttpMessageHandler
    {
        private int _modelsCallCount;

        public int ModelsCallCount => _modelsCallCount;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (path.EndsWith("/health", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith("/v1/health", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            if (path.EndsWith("/v1/models", StringComparison.OrdinalIgnoreCase))
            {
                var count = Interlocked.Increment(ref _modelsCallCount);
                return Task.FromResult(new HttpResponseMessage(count < 2 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    private sealed class FakeProcessRunner : IRuntimeProcessRunner
    {
        public IRuntimeProcess? Start(ProcessStartInfo startInfo) => new FakeProcess();
    }

    private sealed class FakeProcess : IRuntimeProcess
    {
#pragma warning disable CS0067
        private bool _hasExited;
        private int _exitCode;

        public bool HasExited => _hasExited;

        public int ExitCode => _exitCode;

        public bool EnableRaisingEvents { get; set; }

        public event DataReceivedEventHandler? OutputDataReceived;

        public event DataReceivedEventHandler? ErrorDataReceived;

        public event EventHandler? Exited;
#pragma warning restore CS0067

        public void BeginOutputReadLine()
        {
        }

        public void BeginErrorReadLine()
        {
        }

        public void CloseMainWindow()
        {
            Exit(0);
        }

        public Task WaitForExitAsync(CancellationToken ct)
        {
            Exit(_exitCode);
            return Task.CompletedTask;
        }

        public void Kill(bool entireProcessTree)
        {
            Exit(1);
        }

        public void Dispose()
        {
        }

        private void Exit(int exitCode)
        {
            if (_hasExited)
            {
                return;
            }

            _exitCode = exitCode;
            _hasExited = true;
            Exited?.Invoke(this, EventArgs.Empty);
        }
    }
}
