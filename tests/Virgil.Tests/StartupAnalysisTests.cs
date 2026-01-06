using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Virgil.Services;
using Virgil.Services.Network;
using Virgil.Services.Startup;
using IBackgroundProcessPolicyProvider = Virgil.Services.PerformanceService.IBackgroundProcessPolicyProvider;
using IAppMemoryTrimmer = Virgil.Services.PerformanceService.IAppMemoryTrimmer;
using IMemoryReader = Virgil.Services.PerformanceService.IMemoryReader;
using IProcessHandle = Virgil.Services.PerformanceService.IProcessHandle;
using IProcessProvider = Virgil.Services.PerformanceService.IProcessProvider;
using IProcessWhitelistProvider = Virgil.Services.PerformanceService.IProcessWhitelistProvider;
using IStandbyMemoryReleaser = Virgil.Services.PerformanceService.IStandbyMemoryReleaser;
using MemorySnapshot = Virgil.Services.PerformanceService.MemorySnapshot;
using Xunit;

namespace Virgil.Tests;

public class StartupAnalysisTests
{
    [Fact]
    public async Task AnalyzeStartupAsync_ShouldReportOrderedSectionsAndNoSideEffects()
    {
        var analyzer = new FakeStartupAnalyzer();
        var service = new PerformanceService(
            new EmptyProcessProvider(),
            new SupportedMemoryReader(),
            new NoopStandbyReleaser(),
            new EmptyWhitelistProvider(),
            new NoopPolicyProvider(),
            new NoopAppMemoryTrimmer(),
            new FakePlatformInfo(),
            new FakePrivilegeChecker(),
            new NoopSystemCommandRunner(),
            new InMemoryPerformanceStateStore(),
            analyzer);

        var result = await service.AnalyzeStartupAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("1) Liste des éléments startup", result.Message, StringComparison.Ordinal);
        Assert.Contains("2) Classement par impact", result.Message, StringComparison.Ordinal);
        Assert.Contains("3) Temps de démarrage estimé", result.Message, StringComparison.Ordinal);
        Assert.Contains("4) Recommandations (peut être désactivé)", result.Message, StringComparison.Ordinal);
        Assert.Contains("Optimiser le démarrage", result.Message, StringComparison.Ordinal);

        var first = result.Message.IndexOf("1) Liste", StringComparison.Ordinal);
        var second = result.Message.IndexOf("2) Classement", StringComparison.Ordinal);
        var third = result.Message.IndexOf("3) Temps", StringComparison.Ordinal);
        var fourth = result.Message.IndexOf("4) Recommandations", StringComparison.Ordinal);

        Assert.True(first >= 0 && second > first && third > second && fourth > third, "Sections should be ordered and present");
        Assert.DoesNotContain("désactivé automatiquement", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakeStartupAnalyzer : IStartupAnalyzer
    {
        public StartupAnalysisReport Analyze(CancellationToken ct = default)
        {
            var items = new List<StartupAnalysisItem>
            {
                new("Updater", "Acme", "C:/updater.exe", "Registre Run (HKCU)", "Utilisateur courant", true, StartupImpactLevel.Moyen, true, "C:/updater.exe"),
                new("ServiceCore", "OSVendor", "C:/Windows/system32/core.exe", "Service (auto)", "Tous les utilisateurs", true, StartupImpactLevel.Faible, false, "core.exe")
            };

            return new StartupAnalysisReport(items, TimeSpan.FromSeconds(25), TimeSpan.FromMinutes(5), Array.Empty<string>());
        }
    }

    private sealed class FakePlatformInfo : IPlatformInfo
    {
        public bool IsWindows() => true;
    }

    private sealed class EmptyProcessProvider : IProcessProvider
    {
        public IEnumerable<IProcessHandle> EnumerateProcesses() => Enumerable.Empty<IProcessHandle>();
        public int? TryGetForegroundProcessId() => null;
    }

    private sealed class SupportedMemoryReader : IMemoryReader
    {
        public bool IsSupportedPlatform => true;
        public MemorySnapshot GetSnapshot() => new(0, 0);
    }

    private sealed class NoopStandbyReleaser : IStandbyMemoryReleaser
    {
        public bool TryRelease(out string message)
        {
            message = string.Empty;
            return false;
        }
    }

    private sealed class EmptyWhitelistProvider : IProcessWhitelistProvider
    {
        public IReadOnlySet<string> GetNormalizedWhitelist() => new HashSet<string>();
    }

    private sealed class NoopAppMemoryTrimmer : IAppMemoryTrimmer
    {
        public void Trim()
        {
        }
    }

    private sealed class NoopPolicyProvider : IBackgroundProcessPolicyProvider
    {
        public PerformanceService.BackgroundProcessPolicy LoadPolicy() => new(
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>());
    }

    private sealed class FakePrivilegeChecker : IPrivilegeChecker
    {
        public bool IsAdministrator() => false;
    }

    private sealed class NoopSystemCommandRunner : ISystemCommandRunner
    {
        public Task<CommandResult> RunAsync(string fileName, string arguments, TimeSpan timeout, CancellationToken ct = default)
            => Task.FromResult(new CommandResult(true, Output: "noop"));
    }

    private sealed class InMemoryPerformanceStateStore : IPerformanceStateStore
    {
        private PerformanceModeState _state = new();

        public void Clear() => _state = new PerformanceModeState();
        public PerformanceModeState Load() => _state;
        public void Save(PerformanceModeState state) => _state = state;
    }
}
