using System;
using System.Threading;
using System.Threading.Tasks;
using Virgil.Services;
using Xunit;

namespace Virgil.Tests;

public class HardwareDiagnosticAction20Tests
{
    [Fact]
    public async Task RunHardwareQuickCheckAsync_ShouldReturnSummaryAndWarnings()
    {
        var snapshot = new HardwareQuickSnapshot
        {
            CpuUsagePercent = 95,
            CpuFrequencyMHz = 3200,
            CpuThrottlingSuspected = true,
            RamTotalGb = 16,
            RamUsedGb = 15.5,
            RamUsagePercent = 97,
            Disks = new[] { new DiskStatus("Disque C", 96, 1.2, "non disponible") },
            Gpu = new GpuStatus("Test GPU", null, 70),
            Temperatures = new TemperatureSnapshot(70, 70, null),
            MissingMetrics = Array.Empty<string>()
        };

        var service = new DiagnosticService(
            new DummyExpressCollector(),
            new InMemoryHistoryStore(),
            new FixedClock(DateTimeOffset.UtcNow),
            new FakeHardwareCollector(snapshot));

        var result = await service.RunHardwareQuickCheckAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Details);
        Assert.Contains("Résumé global matériel", result.Details!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Attention", result.Details!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Avertissements éventuels", result.Details!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunHardwareQuickCheckAsync_ShouldSurfaceUnavailableMetrics()
    {
        var snapshot = new HardwareQuickSnapshot
        {
            MissingMetrics = new[] { "RAM: non disponible", "Températures matérielles: non disponible" }
        };

        var collector = new FakeHardwareCollector(snapshot);
        var service = new DiagnosticService(
            new DummyExpressCollector(),
            new InMemoryHistoryStore(),
            new FixedClock(DateTimeOffset.UtcNow),
            collector);

        var result = await service.RunHardwareQuickCheckAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Details);
        Assert.Contains("non disponible", result.Details!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Mesures manquantes", result.Details!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunHardwareQuickCheckAsync_ShouldStayReadOnly()
    {
        var collector = new FakeHardwareCollector(new HardwareQuickSnapshot());
        var service = new DiagnosticService(
            new DummyExpressCollector(),
            new InMemoryHistoryStore(),
            new FixedClock(DateTimeOffset.UtcNow),
            collector);

        await service.RunHardwareQuickCheckAsync(CancellationToken.None);

        Assert.Equal(1, collector.CallCount);
    }

    private sealed class FakeHardwareCollector : IHardwareSnapshotCollector
    {
        private readonly HardwareQuickSnapshot _snapshot;

        public FakeHardwareCollector(HardwareQuickSnapshot snapshot) => _snapshot = snapshot;

        public int CallCount { get; private set; }

        public Task<HardwareQuickSnapshot> CaptureAsync(CancellationToken ct)
        {
            CallCount++;
            return Task.FromResult(_snapshot);
        }
    }

    private sealed class DummyExpressCollector : IExpressScanCollector
    {
        public Task<ExpressScanSnapshot> CaptureAsync(CancellationToken ct) => Task.FromResult(new ExpressScanSnapshot());
    }

    private sealed class InMemoryHistoryStore : IScanHistoryStore
    {
        public Task<ScanHistoryEntry?> LoadAsync(CancellationToken ct) => Task.FromResult<ScanHistoryEntry?>(null);

        public Task SaveAsync(ScanHistoryEntry entry, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTimeOffset now) => Now = now;

        public DateTimeOffset Now { get; }
    }
}

