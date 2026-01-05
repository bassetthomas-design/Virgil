using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Virgil.Services;
using Virgil.Services.Network;
using Xunit;

namespace Virgil.Tests;

public class NetworkServiceAction13Tests
{
    [Fact]
    public async Task RunLatencyTest_FormatsMetrics_ForGatewayAndExternal()
    {
        var ping = new StubPingClient(new Dictionary<string, IEnumerable<PingAttemptResult>>
        {
            ["192.168.0.1"] = Enumerable.Repeat(new PingAttemptResult(PingAttemptStatus.Success, 4), 10),
            ["1.1.1.1"] = new[]
            {
                new PingAttemptResult(PingAttemptStatus.Success, 12),
                new PingAttemptResult(PingAttemptStatus.Success, 14),
                new PingAttemptResult(PingAttemptStatus.Success, 15),
                new PingAttemptResult(PingAttemptStatus.Success, 13),
                new PingAttemptResult(PingAttemptStatus.Success, 12),
                new PingAttemptResult(PingAttemptStatus.Success, 12),
                new PingAttemptResult(PingAttemptStatus.Success, 14),
                new PingAttemptResult(PingAttemptStatus.Success, 11),
                new PingAttemptResult(PingAttemptStatus.Success, 12),
                new PingAttemptResult(PingAttemptStatus.Success, 12),
            }
        });

        var service = new NetworkService(new NoopRunner(), new StubPrivilegeChecker(), new StubPlatformInfo(), ping, new StubNetworkInfoProvider("192.168.0.1"));

        var result = await service.RunLatencyTestAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("Passerelle locale: OK", result.Message);
        Assert.Contains("Serveur externe stable: OK", result.Message);
        Assert.Contains("Résumé global: OK", result.Message);
        Assert.Contains("jitter", result.Message);
    }

    [Fact]
    public async Task RunLatencyTest_HandlesMissingGateway()
    {
        var ping = new StubPingClient(new Dictionary<string, IEnumerable<PingAttemptResult>>
        {
            ["1.1.1.1"] = Enumerable.Repeat(new PingAttemptResult(PingAttemptStatus.Success, 25), 10)
        });

        var service = new NetworkService(new NoopRunner(), new StubPrivilegeChecker(), new StubPlatformInfo(), ping, new StubNetworkInfoProvider(null));

        var result = await service.RunLatencyTestAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("Passerelle locale: Échec (passerelle non détectée)", result.Message);
        Assert.Contains("Résumé global: Échec", result.Message);
    }

    [Fact]
    public async Task RunLatencyTest_HandlesDnsFailureOnExternal()
    {
        var ping = new StubPingClient(new Dictionary<string, IEnumerable<PingAttemptResult>>
        {
            ["192.168.0.1"] = Enumerable.Repeat(new PingAttemptResult(PingAttemptStatus.Success, 3), 10),
            ["1.1.1.1"] = new[]
            {
                new PingAttemptResult(PingAttemptStatus.DnsError),
                new PingAttemptResult(PingAttemptStatus.DnsError),
                new PingAttemptResult(PingAttemptStatus.DnsError),
            }
        });

        var service = new NetworkService(new NoopRunner(), new StubPrivilegeChecker(), new StubPlatformInfo(), ping, new StubNetworkInfoProvider("192.168.0.1"));

        var result = await service.RunLatencyTestAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("Serveur externe stable: Échec (DNS/resolve)", result.Message);
        Assert.Contains("Résumé global: Échec", result.Message);
    }

    private sealed class StubNetworkInfoProvider : INetworkInfoProvider
    {
        private readonly string? _gateway;

        public StubNetworkInfoProvider(string? gateway) => _gateway = gateway;

        public string? GetDefaultGateway() => _gateway;
    }

    private sealed class StubPingClient : IPingClient
    {
        private readonly ConcurrentDictionary<string, ConcurrentQueue<PingAttemptResult>> _map;
        private readonly PingAttemptResult _default;

        public StubPingClient(Dictionary<string, IEnumerable<PingAttemptResult>>? map = null, PingAttemptResult? fallback = null)
        {
            _map = new ConcurrentDictionary<string, ConcurrentQueue<PingAttemptResult>>(
                (map ?? new Dictionary<string, IEnumerable<PingAttemptResult>>())
                    .ToDictionary(kvp => kvp.Key, kvp => new ConcurrentQueue<PingAttemptResult>(kvp.Value)));
            _default = fallback ?? new PingAttemptResult(PingAttemptStatus.Success, 10);
        }

        public Task<PingAttemptResult> SendAsync(string host, int timeoutMs, CancellationToken ct = default)
        {
            if (_map.TryGetValue(host, out var queue) && queue.TryDequeue(out var result))
            {
                return Task.FromResult(result);
            }

            return Task.FromResult(_default);
        }
    }

    private sealed class NoopRunner : INetworkCommandRunner
    {
        public Task<NetworkCommandResult> RunAsync(string fileName, string arguments, TimeSpan timeout, CancellationToken ct = default)
            => Task.FromResult(new NetworkCommandResult(true));
    }

    private sealed class StubPrivilegeChecker : IPrivilegeChecker
    {
        public bool IsAdministrator() => true;
    }

    private sealed class StubPlatformInfo : IPlatformInfo
    {
        public bool IsWindows() => true;
    }
}
