using System.Threading;
using System.Threading.Tasks;
using Virgil.Services;
using Virgil.Services.Network;
using Xunit;

namespace Virgil.Tests;

public class NetworkServiceAction21Tests
{
    [Fact]
    public async Task RunInternetSpeedTest_FormatsOrderedOutput()
    {
        var probe = new StubSpeedProbe(new SpeedTestProbeResult(
            Success: true,
            ServerLabel: "TestLab",
            DownloadMbps: 72.5,
            UploadMbps: 18.2,
            LatencyMs: 23,
            StabilityVariationPercent: 4.3,
            UsedFallback: false));

        var service = new NetworkService(
            new NoopRunner(),
            new StubPrivilegeChecker(),
            new StubPlatformInfo(),
            new AlwaysUpPingClient(),
            new StubNetworkInfoProvider("192.168.0.1"),
            probe);

        var result = await service.RunInternetSpeedTestAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("Débit descendant: 72.5 Mbps", result.Message);
        Assert.Contains("Débit montant: 18.2 Mbps", result.Message);
        Assert.Contains("Latence mesurée: 23 ms", result.Message);
        Assert.Contains("Appréciation globale: Bon", result.Message);
        Assert.Contains("Usage conseillé: jeu en ligne", result.Message);
        Assert.Contains("Stabilité: variation 4.3%", result.Message);
    }

    [Fact]
    public async Task RunInternetSpeedTest_HandlesOfflineGracefully()
    {
        var service = new NetworkService(
            new NoopRunner(),
            new StubPrivilegeChecker(),
            new StubPlatformInfo(),
            new OfflinePingClient(),
            new StubNetworkInfoProvider("192.168.0.1"),
            new StubSpeedProbe(SpeedTestProbeResult.Fail("x", "Should not be used")));

        var result = await service.RunInternetSpeedTestAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Connexion indisponible", result.Message);
    }

    [Fact]
    public async Task RunInternetSpeedTest_ReportsTimeouts()
    {
        var probe = new StubSpeedProbe(SpeedTestProbeResult.Fail("Cloud", "Serveur de test indisponible (timeout)", timedOut: true));
        var service = new NetworkService(
            new NoopRunner(),
            new StubPrivilegeChecker(),
            new StubPlatformInfo(),
            new AlwaysUpPingClient(),
            new StubNetworkInfoProvider("192.168.0.1"),
            probe);

        var result = await service.RunInternetSpeedTestAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Serveur de test indisponible", result.Message);
    }

    private sealed class StubSpeedProbe : IInternetSpeedProbe
    {
        private readonly SpeedTestProbeResult _result;

        public StubSpeedProbe(SpeedTestProbeResult result) => _result = result;

        public Task<SpeedTestProbeResult> MeasureAsync(CancellationToken ct = default) => Task.FromResult(_result);
    }

    private sealed class NoopRunner : INetworkCommandRunner
    {
        public Task<NetworkCommandResult> RunAsync(string fileName, string arguments, System.TimeSpan timeout, CancellationToken ct = default)
            => Task.FromResult(new NetworkCommandResult(true));
    }

    private sealed class StubPrivilegeChecker : IPrivilegeChecker
    {
        private readonly bool _isAdmin;

        public StubPrivilegeChecker(bool isAdmin = true) => _isAdmin = isAdmin;

        public bool IsAdministrator() => _isAdmin;
    }

    private sealed class StubPlatformInfo : IPlatformInfo
    {
        public bool IsWindows() => true;
    }

    private sealed class StubNetworkInfoProvider : INetworkInfoProvider
    {
        private readonly string? _gateway;

        public StubNetworkInfoProvider(string? gateway) => _gateway = gateway;

        public string? GetDefaultGateway() => _gateway;
    }

    private sealed class AlwaysUpPingClient : IPingClient
    {
        public Task<PingAttemptResult> SendAsync(string host, int timeoutMs, CancellationToken ct = default)
            => Task.FromResult(new PingAttemptResult(PingAttemptStatus.Success, 10));
    }

    private sealed class OfflinePingClient : IPingClient
    {
        public Task<PingAttemptResult> SendAsync(string host, int timeoutMs, CancellationToken ct = default)
            => Task.FromResult(new PingAttemptResult(PingAttemptStatus.Timeout));
    }
}
