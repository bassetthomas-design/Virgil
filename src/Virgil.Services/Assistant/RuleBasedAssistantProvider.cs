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
        var actions = IntentRouter.SuggestActions(userMessage, ctx.ActionCatalog);
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

    private static string FormatStale(bool isStale) => isStale ? " (stale)" : string.Empty;
}
