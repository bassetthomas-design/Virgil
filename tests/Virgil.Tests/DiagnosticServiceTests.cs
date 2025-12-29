using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Virgil.Services;
using Xunit;

namespace Virgil.Tests;

public class DiagnosticServiceTests
{
    [Fact]
    public async Task RunExpressAsync_ShouldHandleFirstScan()
    {
        var snapshot = new ExpressScanSnapshot
        {
            CpuUsagePercent = 20,
            MemoryUsagePercent = 30,
            DiskUsagePercent = 40,
            DiskLabel = "C:",
            MissingMetrics = Array.Empty<string>(),
            HeavyServices = Array.Empty<string>(),
            SuspiciousProcesses = Array.Empty<string>(),
            RecentErrors = Array.Empty<string>(),
            StartupAppsCount = 2,
            NetworkStatus = NetworkState.Ok
        };

        var service = new DiagnosticService(
            new FakeCollector(snapshot),
            new InMemoryHistoryStore(),
            new FixedClock(DateTimeOffset.UtcNow));

        var result = await service.RunExpressAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Details);
        Assert.Contains("État global", result.Details!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Évolution: premier scan", result.Details!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Problèmes résolus", result.Details!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Problèmes persistants", result.Details!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Recommandations", result.Details!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunExpressAsync_ShouldReportEvolutionAndRecommendations()
    {
        var previousIssues = new List<string> { "CPU surmenée (95 %)" };
        var history = new InMemoryHistoryStore(new ScanHistoryEntry(DateTimeOffset.UtcNow.AddMinutes(-30), "Attention", previousIssues));

        var snapshot = new ExpressScanSnapshot
        {
            CpuUsagePercent = 95,
            MemoryUsagePercent = 20,
            DiskUsagePercent = 30,
            DiskLabel = "C:",
            MissingMetrics = Array.Empty<string>(),
            HeavyServices = Array.Empty<string>(),
            SuspiciousProcesses = Array.Empty<string>(),
            RecentErrors = Array.Empty<string>(),
            StartupAppsCount = 1,
            NetworkStatus = NetworkState.Ok
        };

        var service = new DiagnosticService(
            new FakeCollector(snapshot),
            history,
            new FixedClock(DateTimeOffset.UtcNow));

        var result = await service.RunExpressAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Details);
        Assert.Contains("Évolution: identique", result.Details!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Problèmes persistants", result.Details!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Recommandations", result.Details!, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakeCollector : IExpressScanCollector
    {
        private readonly ExpressScanSnapshot _snapshot;

        public FakeCollector(ExpressScanSnapshot snapshot) => _snapshot = snapshot;

        public Task<ExpressScanSnapshot> CaptureAsync(CancellationToken ct) => Task.FromResult(_snapshot);
    }

    private sealed class InMemoryHistoryStore : IScanHistoryStore
    {
        private ScanHistoryEntry? _entry;

        public InMemoryHistoryStore(ScanHistoryEntry? entry = null)
        {
            _entry = entry;
        }

        public Task<ScanHistoryEntry?> LoadAsync(CancellationToken ct) => Task.FromResult(_entry);

        public Task SaveAsync(ScanHistoryEntry entry, CancellationToken ct)
        {
            _entry = entry;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTimeOffset now) => Now = now;

        public DateTimeOffset Now { get; }
    }
}
