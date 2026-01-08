using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Virgil.Domain.Actions;
using Virgil.Services.Abstractions;

namespace Virgil.Services.SelfTest;

public enum ActionSelfTestStatus
{
    Ok,
    NonCablee,
    Erreur,
}

public sealed record ActionSelfTestItem(int ActionNumber, VirgilActionId ActionId, string ActionKey, ActionSelfTestStatus Status, string? Reason);

public sealed record ActionWiringReport(IReadOnlyCollection<ActionSelfTestItem> Items, int OkCount, int Total, DryRunServiceProbes Probes);

/// <summary>
/// Executes the “Tester le câblage des actions” dry-run verification. The tester
/// walks through the action catalog, ensures that each action between 4 and 25
/// is registered in the UI routes, and simulates an execution through a
/// sandboxed orchestrator to validate the wiring.
/// </summary>
public sealed class ActionWiringTester
{
    private readonly Func<DryRunOrchestratorBundle> _orchestratorFactory;
    private readonly HashSet<string> _registeredRoutes;

    public ActionWiringTester(IEnumerable<string> registeredRoutes, Func<DryRunOrchestratorBundle>? orchestratorFactory = null)
    {
        _registeredRoutes = registeredRoutes is null
            ? throw new ArgumentNullException(nameof(registeredRoutes))
            : new HashSet<string>(registeredRoutes, StringComparer.OrdinalIgnoreCase);

        _orchestratorFactory = orchestratorFactory ?? DryRunOrchestratorBundle.Create;
    }

    public async Task<ActionWiringReport> RunAsync(IEnumerable<ActionDescriptor> descriptors, CancellationToken ct = default)
    {
        if (descriptors is null) throw new ArgumentNullException(nameof(descriptors));

        var orchestratorBundle = _orchestratorFactory();
        var descriptorMap = descriptors
            .GroupBy(d => d.VirgilActionId)
            .ToDictionary(g => g.Key, g => g.First());

        var items = new List<ActionSelfTestItem>();
        var okCount = 0;

        for (var actionNumber = 4; actionNumber <= 25; actionNumber++)
        {
            var actionId = (VirgilActionId)(actionNumber - 1);
            var status = await InspectActionAsync(actionNumber, actionId, descriptorMap, orchestratorBundle.Orchestrator, ct)
                .ConfigureAwait(false);

            if (status.Status == ActionSelfTestStatus.Ok)
            {
                okCount++;
            }

            items.Add(status);
        }

        return new ActionWiringReport(items, okCount, 25, orchestratorBundle.Probes);
    }

    private async Task<ActionSelfTestItem> InspectActionAsync(
        int actionNumber,
        VirgilActionId actionId,
        IReadOnlyDictionary<VirgilActionId, ActionDescriptor> descriptors,
        IActionOrchestrator orchestrator,
        CancellationToken ct)
    {
        if (!descriptors.TryGetValue(actionId, out var descriptor))
        {
            return new ActionSelfTestItem(actionNumber, actionId, $"action_{actionNumber:00}", ActionSelfTestStatus.NonCablee, "Action absente du catalogue");
        }

        if (!_registeredRoutes.Contains(descriptor.ActionKey))
        {
            return new ActionSelfTestItem(actionNumber, actionId, descriptor.ActionKey, ActionSelfTestStatus.NonCablee, "Route UI absente");
        }

        try
        {
            var result = await orchestrator.RunAsync(descriptor.VirgilActionId, ct).ConfigureAwait(false);
            if (result.Success)
            {
                return new ActionSelfTestItem(actionNumber, actionId, descriptor.ActionKey, ActionSelfTestStatus.Ok, null);
            }

            var reason = string.IsNullOrWhiteSpace(result.Summary) ? result.Title : result.Summary;
            if (string.IsNullOrWhiteSpace(reason))
            {
                reason = "Echec dry-run";
            }
            var status = reason.Contains("non gérée", StringComparison.OrdinalIgnoreCase)
                ? ActionSelfTestStatus.NonCablee
                : ActionSelfTestStatus.Erreur;
            return new ActionSelfTestItem(actionNumber, actionId, descriptor.ActionKey, status, reason);
        }
        catch (Exception ex)
        {
            return new ActionSelfTestItem(actionNumber, actionId, descriptor.ActionKey, ActionSelfTestStatus.Erreur, ex.Message);
        }
    }
}
