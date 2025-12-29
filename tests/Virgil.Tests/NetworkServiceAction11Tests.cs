using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Virgil.Services;
using Virgil.Services.Network;
using Xunit;

namespace Virgil.Tests;

public class NetworkServiceAction11Tests
{
    [Fact]
    public async Task SoftReset_BuildsFullSummary_WhenAdmin()
    {
        var runner = new StubRunner();
        var service = new NetworkService(runner, new StubPrivilegeChecker(isAdmin: true), new StubPlatformInfo());

        var result = await service.SoftResetAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("Reset réseau (soft): Résultat global: OK", result.Message);
        Assert.Contains("Flush DNS: OK", result.Message);
        Assert.Contains("Renew IP: OK", result.Message);
        Assert.Contains("Reset Winsock léger: OK", result.Message);
        Assert.Contains("Réinitialiser DNS custom", result.Message);
        Assert.Contains("Prochaines options: Diagnostic réseau | Reset réseau (complet)", result.Message);
    }

    [Fact]
    public async Task SoftReset_GracefullyDegrades_WhenNotAdmin()
    {
        var runner = new StubRunner();
        var service = new NetworkService(runner, new StubPrivilegeChecker(isAdmin: false), new StubPlatformInfo());

        var result = await service.SoftResetAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("Résultat global: Attention", result.Message);
        Assert.Contains("Reset Winsock léger: Ignoré", result.Message);
        Assert.Contains("Réinitialiser DNS custom: Ignoré", result.Message);
        Assert.Contains("Réinitialiser adaptateurs (soft): Ignoré", result.Message);
        Assert.Contains("Prochaines options: Diagnostic réseau | Reset réseau (complet)", result.Message);
    }

    private sealed class StubRunner : INetworkCommandRunner
    {
        public ConcurrentQueue<(string FileName, string Args)> Calls { get; } = new();

        public Task<NetworkCommandResult> RunAsync(string fileName, string arguments, TimeSpan timeout, CancellationToken ct = default)
        {
            Calls.Enqueue((fileName, arguments));
            return Task.FromResult(new NetworkCommandResult(true));
        }
    }

    private sealed class StubPrivilegeChecker : IPrivilegeChecker
    {
        private readonly bool _isAdmin;

        public StubPrivilegeChecker(bool isAdmin) => _isAdmin = isAdmin;

        public bool IsAdministrator() => _isAdmin;
    }

    private sealed class StubPlatformInfo : IPlatformInfo
    {
        public bool IsWindows() => true;
    }
}
