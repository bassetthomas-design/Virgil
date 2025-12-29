using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Virgil.Services;
using Xunit;

namespace Virgil.Tests;

public class PerformanceServiceAction7Tests
{
    [Fact]
    public async Task SoftRamFlush_ShouldReturnChatFriendlySummary()
    {
        var memoryReader = new FakeMemoryReader(
            new PerformanceService.MemorySnapshot(16000, 8000),
            new PerformanceService.MemorySnapshot(16000, 8100));

        var process = new FakeProcessHandle("chrome", workingSet: 120_000_000, sessionId: 1, hasMainWindow: false);

        var service = new PerformanceService(
            processProvider: new FakeProcessProvider(new[] { process }),
            memoryReader: memoryReader,
            standbyMemoryReleaser: new FakeStandbyMemoryReleaser(false, "Libération du cache standby non disponible sans droits admin."),
            whitelistProvider: new FakeWhitelistProvider(new[] { "chrome" }),
            appMemoryTrimmer: new FakeAppMemoryTrimmer());

        var result = await service.SoftRamFlushAsync();

        result.Success.Should().BeTrue();
        result.Message.Should().Contain("avant");
        result.Message.Should().Contain("après");
        result.Message.Should().Contain("Mo");
        result.Message.Should().Contain("temporaire");
    }

    [Fact]
    public async Task SoftRamFlush_ShouldRespectWhitelist()
    {
        var memoryReader = new FakeMemoryReader(
            new PerformanceService.MemorySnapshot(12000, 6000),
            new PerformanceService.MemorySnapshot(12000, 6050));

        var allowed = new FakeProcessHandle("edge", workingSet: 90_000_000, sessionId: 1, hasMainWindow: false);
        var blocked = new FakeProcessHandle("notepad", workingSet: 90_000_000, sessionId: 1, hasMainWindow: false);

        var service = new PerformanceService(
            processProvider: new FakeProcessProvider(new[] { blocked, allowed }),
            memoryReader: memoryReader,
            standbyMemoryReleaser: new FakeStandbyMemoryReleaser(false, string.Empty),
            whitelistProvider: new FakeWhitelistProvider(new[] { "edge" }),
            appMemoryTrimmer: new FakeAppMemoryTrimmer());

        _ = await service.SoftRamFlushAsync();

        allowed.TrimAttempts.Should().Be(1);
        blocked.TrimAttempts.Should().Be(0);
    }

    [Fact]
    public async Task SoftRamFlush_ShouldStayOkWhenStandbyUnavailable()
    {
        var memoryReader = new FakeMemoryReader(
            new PerformanceService.MemorySnapshot(8000, 4000),
            new PerformanceService.MemorySnapshot(8000, 4050));

        var process = new FakeProcessHandle("vlc", workingSet: 70_000_000, sessionId: 1, hasMainWindow: false);

        var service = new PerformanceService(
            processProvider: new FakeProcessProvider(new[] { process }),
            memoryReader: memoryReader,
            standbyMemoryReleaser: new FakeStandbyMemoryReleaser(false, "Standby/cache non disponible sans droits"),
            whitelistProvider: new FakeWhitelistProvider(new[] { "vlc" }),
            appMemoryTrimmer: new FakeAppMemoryTrimmer());

        var result = await service.SoftRamFlushAsync();

        result.Success.Should().BeTrue();
        result.TryGetDetails(out var details).Should().BeTrue();
        details!.Should().Contain("Standby/cache non disponible sans droits");
    }

    private sealed class FakeProcessProvider : PerformanceService.IProcessProvider
    {
        private readonly IReadOnlyList<PerformanceService.IProcessHandle> _processes;

        public FakeProcessProvider(IReadOnlyList<PerformanceService.IProcessHandle> processes)
        {
            _processes = processes;
        }

        public IEnumerable<PerformanceService.IProcessHandle> EnumerateProcesses() => _processes;

        public int? TryGetForegroundProcessId() => null;
    }

    private sealed class FakeProcessHandle : PerformanceService.IProcessHandle
    {
        private long _workingSet;

        public FakeProcessHandle(string name, long workingSet, int sessionId, bool hasMainWindow)
        {
            ProcessName = name;
            _workingSet = workingSet;
            SessionId = sessionId;
            HasMainWindow = hasMainWindow;
        }

        public int Id => 4242;
        public string ProcessName { get; }
        public int SessionId { get; }
        public bool HasExited => false;
        public bool HasMainWindow { get; }
        public long WorkingSet => _workingSet;
        public int TrimAttempts { get; private set; }

        public bool TryTrimWorkingSet(out long reclaimedBytes)
        {
            TrimAttempts++;
            reclaimedBytes = _workingSet / 2;
            _workingSet -= reclaimedBytes;
            return true;
        }

        public void Dispose()
        {
        }
    }

    private sealed class FakeMemoryReader : PerformanceService.IMemoryReader
    {
        private readonly Queue<PerformanceService.MemorySnapshot> _snapshots;

        public FakeMemoryReader(params PerformanceService.MemorySnapshot[] snapshots)
        {
            _snapshots = new Queue<PerformanceService.MemorySnapshot>(snapshots);
        }

        public bool IsSupportedPlatform { get; set; } = true;

        public PerformanceService.MemorySnapshot GetSnapshot()
        {
            return _snapshots.Count > 0
                ? _snapshots.Dequeue()
                : new PerformanceService.MemorySnapshot(0, 0);
        }
    }

    private sealed class FakeStandbyMemoryReleaser : PerformanceService.IStandbyMemoryReleaser
    {
        private readonly bool _succeed;
        private readonly string _message;

        public FakeStandbyMemoryReleaser(bool succeed, string message)
        {
            _succeed = succeed;
            _message = message;
        }

        public bool TryRelease(out string message)
        {
            message = _message;
            return _succeed;
        }
    }

    private sealed class FakeWhitelistProvider : PerformanceService.IProcessWhitelistProvider
    {
        private readonly HashSet<string> _names;

        public FakeWhitelistProvider(IEnumerable<string> names)
        {
            _names = new HashSet<string>();
            foreach (var name in names)
            {
                _names.Add(PerformanceService.ProcessNameHelper.Normalize(name));
            }
        }

        public IReadOnlySet<string> GetNormalizedWhitelist() => _names;
    }

    private sealed class FakeAppMemoryTrimmer : PerformanceService.IAppMemoryTrimmer
    {
        public void Trim()
        {
        }
    }
}
