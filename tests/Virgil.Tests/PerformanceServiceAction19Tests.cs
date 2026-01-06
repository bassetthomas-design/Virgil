using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Virgil.Services;
using Virgil.Services.Network;
using Xunit;

namespace Virgil.Tests;

public class PerformanceServiceAction19Tests
{
    [Fact]
    public async Task CloseGamingSession_ShouldApplyPolicyAndActions()
    {
        var processes = new[]
        {
            new FakeProcessHandle(12, "discord", hasMainWindow: false, sessionId: 1),
            new FakeProcessHandle(99, "audiodg", hasMainWindow: false, sessionId: 0)
        };

        var controller = new FakeProcessController();
        var service = BuildService(
            processes,
            new FakePolicyProvider(
                whitelist: new[] { "discord" },
                blacklist: new[] { "audiodg" },
                games: new string[0],
                tags: new string[0]),
            confirmation: new FakeSessionConfirmation(true),
            controller: controller);

        var result = await service.CloseGamingSessionAsync();

        result.Success.Should().BeTrue();
        result.Message.Should().Contain("discord");
        controller.Closed.Should().ContainSingle(id => id == 12);
        controller.Prioritized.Should().ContainSingle(id => id == 12);
    }

    [Fact]
    public async Task CloseGamingSession_ShouldIgnoreForegroundGame()
    {
        var processes = new[]
        {
            new FakeProcessHandle(7, "valorant", hasMainWindow: true, sessionId: 1),
            new FakeProcessHandle(8, "discord", hasMainWindow: false, sessionId: 1)
        };

        var controller = new FakeProcessController();
        var service = BuildService(
            processes,
            new FakePolicyProvider(
                whitelist: new[] { "discord" },
                blacklist: new string[0],
                games: new[] { "valorant" },
                tags: new string[0]),
            confirmation: new FakeSessionConfirmation(true),
            controller: controller,
            foregroundPid: 7);

        var result = await service.CloseGamingSessionAsync();

        result.Success.Should().BeTrue();
        controller.Closed.Should().ContainSingle(id => id == 8);
        controller.Closed.Should().NotContain(7);
    }

    [Fact]
    public async Task CloseGamingSession_ShouldSkipSystemProcesses()
    {
        var processes = new[]
        {
            new FakeProcessHandle(1, "System", hasMainWindow: false, sessionId: 0),
            new FakeProcessHandle(2, "discord", hasMainWindow: false, sessionId: 1)
        };

        var controller = new FakeProcessController();
        var service = BuildService(
            processes,
            new FakePolicyProvider(
                whitelist: new[] { "discord" },
                blacklist: new string[0],
                games: new string[0],
                tags: new string[0]),
            confirmation: new FakeSessionConfirmation(true),
            controller: controller);

        var result = await service.CloseGamingSessionAsync();

        result.Success.Should().BeTrue();
        controller.Closed.Should().ContainSingle(id => id == 2);
        controller.Closed.Should().NotContain(1);
        result.TryGetDetails(out var details).Should().BeTrue();
        details!.Should().Contain("système");
    }

    [Fact]
    public async Task CloseGamingSession_ShouldRespectUserRefusal()
    {
        var processes = new[]
        {
            new FakeProcessHandle(10, "discord", hasMainWindow: false, sessionId: 1)
        };

        var controller = new FakeProcessController();
        var service = BuildService(
            processes,
            new FakePolicyProvider(
                whitelist: new[] { "discord" },
                blacklist: new string[0],
                games: new string[0],
                tags: new string[0]),
            confirmation: new FakeSessionConfirmation(false),
            controller: controller);

        var result = await service.CloseGamingSessionAsync();

        result.Success.Should().BeFalse();
        controller.Closed.Should().BeEmpty();
        result.Message.Should().Contain("aucune app fermée", System.StringComparison.OrdinalIgnoreCase);
    }

    private static PerformanceService BuildService(
        IReadOnlyList<PerformanceService.IProcessHandle> processes,
        PerformanceService.IBackgroundProcessPolicyProvider policy,
        PerformanceService.ICloseSessionConfirmation confirmation,
        PerformanceService.IProcessController controller,
        int? foregroundPid = null)
    {
        return new PerformanceService(
            processProvider: new FakeProcessProvider(processes, foregroundPid),
            memoryReader: new FakeMemoryReader(),
            whitelistProvider: new FakeWhitelistProvider(new string[0]),
            policyProvider: policy,
            sessionConfirmation: confirmation,
            processController: controller,
            platformInfo: new StubPlatformInfo());
    }

    private sealed class FakeProcessProvider : PerformanceService.IProcessProvider
    {
        private readonly IReadOnlyList<PerformanceService.IProcessHandle> _processes;
        private readonly int? _foregroundPid;

        public FakeProcessProvider(IReadOnlyList<PerformanceService.IProcessHandle> processes, int? foregroundPid)
        {
            _processes = processes;
            _foregroundPid = foregroundPid;
        }

        public IEnumerable<PerformanceService.IProcessHandle> EnumerateProcesses() => _processes;

        public int? TryGetForegroundProcessId() => _foregroundPid;
    }

    private sealed class FakeProcessHandle : PerformanceService.IProcessHandle
    {
        public FakeProcessHandle(int id, string name, bool hasMainWindow, int sessionId)
        {
            Id = id;
            ProcessName = name;
            HasMainWindow = hasMainWindow;
            SessionId = sessionId;
        }

        public int Id { get; }
        public string ProcessName { get; }
        public int SessionId { get; }
        public bool HasExited => false;
        public bool HasMainWindow { get; }
        public long WorkingSet => 0;
        public void Dispose() { }
        public bool TryTrimWorkingSet(out long reclaimedBytes)
        {
            reclaimedBytes = 0;
            return false;
        }
    }

    private sealed class FakePolicyProvider : PerformanceService.IBackgroundProcessPolicyProvider
    {
        private readonly PerformanceService.BackgroundProcessPolicy _policy;

        public FakePolicyProvider(IEnumerable<string> whitelist, IEnumerable<string> blacklist, IEnumerable<string> games, IEnumerable<string> tags)
        {
            _policy = new PerformanceService.BackgroundProcessPolicy(
                whitelist.Select(PerformanceService.ProcessNameHelper.Normalize).ToHashSet(),
                blacklist.Select(PerformanceService.ProcessNameHelper.Normalize).ToHashSet(),
                games.Select(PerformanceService.ProcessNameHelper.Normalize).ToHashSet(),
                tags.Select(t => t.Trim().ToLowerInvariant()).ToHashSet());
        }

        public PerformanceService.BackgroundProcessPolicy GetPolicy() => _policy;
    }

    private sealed class FakeSessionConfirmation : PerformanceService.ICloseSessionConfirmation
    {
        private readonly bool _result;

        public FakeSessionConfirmation(bool result)
        {
            _result = result;
        }

        public Task<bool> ConfirmAsync(string proposal, CancellationToken ct = default) => Task.FromResult(_result);
    }

    private sealed class FakeProcessController : PerformanceService.IProcessController
    {
        public List<int> Closed { get; } = new();
        public List<int> Prioritized { get; } = new();

        public bool TryRequestClose(int pid, out string? note)
        {
            Closed.Add(pid);
            note = "fermeture demandée";
            return true;
        }

        public bool TrySuspend(int pid, out string? note)
        {
            note = null;
            return false;
        }

        public bool TryLowerPriority(int pid, out string? note)
        {
            Prioritized.Add(pid);
            note = "priorité réduite";
            return true;
        }
    }

    private sealed class StubPlatformInfo : IPlatformInfo
    {
        public bool IsWindows() => true;
    }

    private sealed class FakeMemoryReader : PerformanceService.IMemoryReader
    {
        public bool IsSupportedPlatform => true;
        public PerformanceService.MemorySnapshot GetSnapshot() => new(0, 0);
    }

    private sealed class FakeWhitelistProvider : PerformanceService.IProcessWhitelistProvider
    {
        private readonly IReadOnlySet<string> _entries;
        public FakeWhitelistProvider(IEnumerable<string> entries)
        {
            _entries = entries.Select(PerformanceService.ProcessNameHelper.Normalize).ToHashSet();
        }

        public IReadOnlySet<string> GetNormalizedWhitelist() => _entries;
    }
}
