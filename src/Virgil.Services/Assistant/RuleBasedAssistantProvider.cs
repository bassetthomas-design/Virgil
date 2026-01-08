using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Virgil.Services.Assistant;

public sealed class RuleBasedAssistantProvider : IAssistantProvider
{
    public Task<AssistantReply> AskAsync(string userMessage, AssistantContext ctx, CancellationToken ct = default)
    {
        var responseText = BuildResponseText(ctx);
        var actions = SuggestActions(userMessage, ctx.ActionCatalog);
        return Task.FromResult(new AssistantReply(responseText, actions));
    }

    private static string BuildResponseText(AssistantContext ctx)
    {
        var parts = new List<string>();

        if (ctx.Telemetry is not null)
        {
            var telemetry = ctx.Telemetry;
            parts.Add($"CPU: {telemetry.Cpu}{FormatStale(telemetry.CpuStale)} · RAM: {telemetry.Ram}{FormatStale(telemetry.RamStale)}");
            parts.Add($"Temp: {telemetry.Temperature}{FormatStale(telemetry.TemperatureStale)} · Disque: {telemetry.Disk}{FormatStale(telemetry.DiskStale)}");
        }

        if (ctx.LastActionResult is not null)
        {
            var last = ctx.LastActionResult;
            var lines = last.Lines?.Take(3).ToArray() ?? Array.Empty<string>();
            var summary = lines.Length == 0
                ? $"{last.Title} ({last.Status})"
                : $"{last.Title} ({last.Status}) : {string.Join(" | ", lines)}";
            parts.Add($"Dernière action: {summary}");
        }

        if (parts.Count == 0)
        {
            parts.Add("Je suis prêt. Que souhaites-tu faire ?");
        }

        return string.Join(Environment.NewLine, parts);
    }

    private static IReadOnlyList<ProposedAction> SuggestActions(string userMessage, IReadOnlyList<AssistantActionCatalogItem> catalog)
    {
        if (string.IsNullOrWhiteSpace(userMessage) || catalog.Count == 0)
        {
            return Array.Empty<ProposedAction>();
        }

        var lower = userMessage.ToLowerInvariant();
        var suggestions = new List<ProposedAction>();

        if (ContainsAny(lower, "nettoyage", "clean"))
        {
            AddIfAvailable(suggestions, catalog, "quick_clean");
            AddIfAvailable(suggestions, catalog, "system_temp_clean");
        }

        if (ContainsAny(lower, "navigateur", "browser"))
        {
            AddIfAvailable(suggestions, catalog, "browser_soft_clean");
        }

        if (ContainsAny(lower, "analyse", "scan", "diagnostic", "statut"))
        {
            AddIfAvailable(suggestions, catalog, "status");
            AddIfAvailable(suggestions, catalog, "quick_scan");
        }

        if (ContainsAny(lower, "mise à jour", "update"))
        {
            AddIfAvailable(suggestions, catalog, "apps_update_all");
            AddIfAvailable(suggestions, catalog, "windows_update");
        }

        return suggestions.Count == 0
            ? Array.Empty<ProposedAction>()
            : suggestions;
    }

    private static void AddIfAvailable(ICollection<ProposedAction> list, IReadOnlyList<AssistantActionCatalogItem> catalog, string id)
    {
        var item = catalog.FirstOrDefault(c => c.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (item is null)
        {
            return;
        }

        var warning = item.RequiresAdmin
            ? "Admin requis"
            : item.DestructiveFlag ? "Destructif" : null;

        list.Add(new ProposedAction(item.Id, item.Label, null, true, warning));
    }

    private static string FormatStale(bool isStale) => isStale ? " (stale)" : string.Empty;

    private static bool ContainsAny(string text, params string[] keywords)
        => keywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
}
