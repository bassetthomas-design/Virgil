using System;
using System.Collections.Generic;
using System.Linq;

namespace Virgil.Services.Assistant;

internal static class IntentRouter
{
    public static IReadOnlyList<ProposedAction> SuggestActions(string userMessage, IReadOnlyList<AssistantActionCatalogItem> catalog)
    {
        if (string.IsNullOrWhiteSpace(userMessage) || catalog.Count == 0)
        {
            return Array.Empty<ProposedAction>();
        }

        var lower = userMessage.ToLowerInvariant();
        var suggestions = new List<ProposedAction>();

        if (ContainsAny(lower, "scan", "analyse", "diagnostic", "statut"))
        {
            AddIfAvailable(suggestions, catalog, "status");
            AddIfAvailable(suggestions, catalog, "quick_scan");
        }

        if (ContainsAny(lower, "nettoyer", "nettoyage", "clean"))
        {
            AddIfAvailable(suggestions, catalog, "quick_clean");
            AddIfAvailable(suggestions, catalog, "system_temp_clean");
            AddIfAvailable(suggestions, catalog, "browser_soft_clean");
        }

        if (ContainsAny(lower, "réseau", "reseau", "wifi", "internet", "latence", "débit", "debit"))
        {
            AddIfAvailable(suggestions, catalog, "network_diag");
            AddIfAvailable(suggestions, catalog, "network_speed_test");
            AddIfAvailable(suggestions, catalog, "network_latency_test");
        }

        if (ContainsAny(lower, "mise à jour", "mise a jour", "update", "maj"))
        {
            AddIfAvailable(suggestions, catalog, "apps_update_all");
            AddIfAvailable(suggestions, catalog, "windows_update");
            AddIfAvailable(suggestions, catalog, "auto_updates_manage");
        }

        if (ContainsAny(lower, "thanos"))
        {
            AddIfAvailable(suggestions, catalog, "chat_thanos");
        }

        if (ContainsAny(lower, "driver", "drivers", "pilote", "pilotes", "gpu"))
        {
            AddIfAvailable(suggestions, catalog, "gpu_driver_check");
        }

        if (ContainsAny(lower, "ram", "mémoire", "memoire"))
        {
            AddIfAvailable(suggestions, catalog, "ram_soft_free");
        }

        return suggestions.Count == 0
            ? Array.Empty<ProposedAction>()
            : suggestions.Take(3).ToArray();
    }

    private static void AddIfAvailable(ICollection<ProposedAction> list, IReadOnlyList<AssistantActionCatalogItem> catalog, string id)
    {
        if (list.Any(item => item.ActionId.Equals(id, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

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

    private static bool ContainsAny(string text, params string[] keywords)
        => keywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
}
