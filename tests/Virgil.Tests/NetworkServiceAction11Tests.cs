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

    [Fact]
    public async Task AdvancedReset_RefusesWithoutAdmin()
    {
        var runner = new StubRunner();
        var service = new NetworkService(runner, new StubPrivilegeChecker(isAdmin: false), new StubPlatformInfo());

        var result = await service.AdvancedResetAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("droits administrateur", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public async Task AdvancedReset_BuildsFullSummary_WhenAdmin()
    {
        var runner = new StubRunner((file, args) =>
        {
            if (args.Contains("winsock reset", StringComparison.OrdinalIgnoreCase))
            {
                return new NetworkCommandResult(true, Output: "Please restart to complete reset");
            }

            return new NetworkCommandResult(true);
        });

        var service = new NetworkService(runner, new StubPrivilegeChecker(isAdmin: true), new StubPlatformInfo());

        var result = await service.AdvancedResetAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("Reset réseau (complet): Résultat global: OK", result.Message);
        Assert.Contains("Reset complet Winsock", result.Message);
        Assert.Contains("Reset pile TCP/IP", result.Message);
        Assert.Contains("Réinitialisation adaptateurs réseau", result.Message);
        Assert.Contains("Suppression configs IP custom", result.Message);
        Assert.Contains("Suppression profils Wi-Fi", result.Message);
        Assert.Contains("Suppression réseaux Ethernet mémorisés", result.Message);
        Assert.Contains("Redémarrage services réseau", result.Message);
        Assert.Contains("Redémarrage: requis", result.Message);
        Assert.Contains("reconfiguration Wi-Fi / VPN", result.Message);
        Assert.EndsWith("Prochaines options: Diagnostic réseau", result.Message.Trim());
    }

    [Fact]
    public async Task AdvancedReset_DoesNotTouchVpnSoftware()
    {
        var runner = new StubRunner();
        var service = new NetworkService(runner, new StubPrivilegeChecker(isAdmin: true), new StubPlatformInfo());

        var result = await service.AdvancedResetAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.DoesNotContain(runner.Calls, c => c.FileName.Contains("vpn", StringComparison.OrdinalIgnoreCase) || c.Args.Contains("vpn", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class StubRunner : INetworkCommandRunner
    {
        private readonly Func<string, string, NetworkCommandResult>? _handler;

        public StubRunner(Func<string, string, NetworkCommandResult>? handler = null)
            => _handler = handler;

        public ConcurrentQueue<(string FileName, string Args)> Calls { get; } = new();

        public Task<NetworkCommandResult> RunAsync(string fileName, string arguments, TimeSpan timeout, CancellationToken ct = default)
        {
            Calls.Enqueue((fileName, arguments));
            var result = _handler?.Invoke(fileName, arguments) ?? new NetworkCommandResult(true);
            return Task.FromResult(result);
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
