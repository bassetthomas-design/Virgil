using System.Linq;
using System.Text;

namespace Virgil.Services.Assistant;

internal static class AssistantPromptBuilder
{
    public static string BuildSystemPrompt(AssistantContext ctx)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Tu es l'assistant système Virgil.");
        builder.AppendLine("You are Virgil, a conversational assistant.");
        builder.AppendLine("You do NOT execute system commands unless explicitly asked to in a dedicated 'command mode'.");
        builder.AppendLine("User messages should be interpreted as natural language conversation by default.");
        builder.AppendLine("Réponds uniquement en JSON strict, sans texte additionnel.");
        builder.AppendLine("Format attendu:");
        builder.AppendLine("{ \"text\": \"...\", \"proposedActions\": [ { \"actionId\": \"...\", \"title\": \"...\", \"parameters\": { ... } } ] }");
        builder.AppendLine("Règles:");
        builder.AppendLine("- Ne propose QUE des actionId présents dans le catalogue.");
        builder.AppendLine("- Maximum 3 actions proposées.");
        builder.AppendLine("- Si aucune action pertinente, proposedActions doit être [].");
        builder.AppendLine("- Par défaut: mode conversationnel.");
        builder.AppendLine("- Mode commande UNIQUEMENT si l'utilisateur tape un préfixe explicite (ex: /cmd, !cmd, etc.).");
        builder.AppendLine();
        builder.AppendLine("Catalogue d'actions disponibles:");

        foreach (var item in ctx.ActionCatalog)
        {
            builder.AppendLine($"- Id: {item.Id} | Label: {item.Label} | Description: {item.Description} | Admin: {(item.RequiresAdmin ? "oui" : "non")} | Destructif: {(item.DestructiveFlag ? "oui" : "non")}");
        }

        builder.AppendLine();
        builder.AppendLine("Contexte système:");
        builder.AppendLine($"CPU: {ctx.Telemetry.Cpu} (stale: {ctx.Telemetry.CpuStale})");
        builder.AppendLine($"RAM: {ctx.Telemetry.Ram} (stale: {ctx.Telemetry.RamStale})");
        builder.AppendLine($"Température: {ctx.Telemetry.Temperature} (stale: {ctx.Telemetry.TemperatureStale})");
        builder.AppendLine($"Disque: {ctx.Telemetry.Disk} (stale: {ctx.Telemetry.DiskStale})");

        if (ctx.LastActionResult is not null)
        {
            builder.AppendLine($"Dernière action: {ctx.LastActionResult.Title} ({ctx.LastActionResult.Status})");
            if (ctx.LastActionResult.Lines is not null && ctx.LastActionResult.Lines.Count > 0)
            {
                builder.AppendLine("Résumé: " + string.Join(" | ", ctx.LastActionResult.Lines.Take(3)));
            }
        }

        return builder.ToString();
    }
}
