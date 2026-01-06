using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Virgil.Domain.Actions;
using Virgil.Services.Abstractions;
using Virgil.Services;
using Virgil.Services.SelfTest;
using Xunit;

namespace Virgil.Tests;

public class ActionWiringTesterTests
{
    [Fact]
    public async Task ShouldGenerateCompleteReport()
    {
        var tester = new ActionWiringTester(ActionCatalog.All.Values.Select(v => v.ActionKey));

        var report = await tester.RunAsync(ActionCatalog.All.Values);

        report.Items.Should().HaveCount(22);
        report.Total.Should().Be(25);
    }

    [Fact]
    public async Task MissingRoute_ShouldBeMarkedNonCablee()
    {
        var registeredRoutes = ActionCatalog.All.Values.Select(v => v.ActionKey).Where(key => key != "ram_soft_free");
        var tester = new ActionWiringTester(registeredRoutes);

        var report = await tester.RunAsync(ActionCatalog.All.Values);

        var softRam = report.Items.Single(i => i.ActionNumber == 4);
        softRam.Status.Should().Be(ActionSelfTestStatus.NonCablee);
        softRam.Reason.Should().Contain("Route UI absente");
    }

    [Fact]
    public async Task OrchestratorFailure_ShouldBeSurfacedAsError()
    {
        var registeredRoutes = ActionCatalog.All.Values.Select(v => v.ActionKey);
        var tester = new ActionWiringTester(
            registeredRoutes,
            () => new DryRunOrchestratorBundle(new ThrowingOrchestrator(VirgilActionId.SoftRamFlush), new DryRunServiceProbes()));

        var report = await tester.RunAsync(ActionCatalog.All.Values);

        var softRam = report.Items.Single(i => i.ActionNumber == 4);
        softRam.Status.Should().Be(ActionSelfTestStatus.Erreur);
        softRam.Reason.Should().Contain("boom");
    }

    [Fact]
    public async Task DryRun_ShouldAvoidSideEffects()
    {
        var tester = new ActionWiringTester(ActionCatalog.All.Values.Select(v => v.ActionKey));

        var report = await tester.RunAsync(ActionCatalog.All.Values);

        report.Probes.HasSideEffects.Should().BeFalse();
        report.Probes.TotalInvocations.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Catalog_ShouldExposeDiskAndIntegrityChecks()
    {
        ActionCatalog.All.Values.Should().Contain(d => d.VirgilActionId == VirgilActionId.DiskCheck
            && d.ActionKey == "disk_check"
            && d.DisplayName.Contains("disque", StringComparison.OrdinalIgnoreCase));

        ActionCatalog.All.Values.Should().Contain(d => d.VirgilActionId == VirgilActionId.SystemIntegrityCheck
            && d.ActionKey == "system_integrity_check"
            && d.DisplayName.Contains("intégrité", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class ThrowingOrchestrator : IActionOrchestrator
    {
        private readonly VirgilActionId _target;

        public ThrowingOrchestrator(VirgilActionId target)
        {
            _target = target;
        }

        public Task<ActionExecutionResult> RunAsync(VirgilActionId actionId, CancellationToken cancellationToken = default)
        {
            if (actionId == _target)
            {
                throw new InvalidOperationException("boom");
            }

            return Task.FromResult(ActionExecutionResult.Ok("dry run ok"));
        }
    }
}
