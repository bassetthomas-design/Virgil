using System;
using System.Linq;
using System.Text;
using Virgil.Core.Models;

namespace Virgil.Services;

public sealed record FormattedActionResult(ChatSeverity Severity, string PrimaryMessage, string? Details);

public enum ChatSeverity
{
    Info,
    Warning,
    Error
}

public sealed class ActionResultToChatFormatter
{
    public FormattedActionResult Format(ActionExecutionResult result)
    {
        var label = string.IsNullOrWhiteSpace(result.Title) ? result.Summary : result.Title;
        if (string.IsNullOrWhiteSpace(label))
        {
            label = "Action";
        }

        var primary = result.Status switch
        {
            ActionResultStatus.Success => $"Terminé: {label}",
            ActionResultStatus.PartialSuccess => $"Terminé (partiel): {label}",
            ActionResultStatus.Failed => $"Échec: {label}",
            ActionResultStatus.NotAvailable => $"Non disponible: {label}",
            ActionResultStatus.NotImplemented => $"Non implémenté: {label}",
            ActionResultStatus.Skipped => $"Ignoré: {label}",
            _ => label
        };

        var details = BuildDetails(result, label);
        return new FormattedActionResult(ToSeverity(result.Status), primary, details);
    }

    private static ChatSeverity ToSeverity(ActionResultStatus status)
        => status switch
        {
            ActionResultStatus.Success => ChatSeverity.Info,
            ActionResultStatus.PartialSuccess => ChatSeverity.Warning,
            ActionResultStatus.Failed => ChatSeverity.Error,
            ActionResultStatus.NotAvailable => ChatSeverity.Warning,
            ActionResultStatus.NotImplemented => ChatSeverity.Warning,
            ActionResultStatus.Skipped => ChatSeverity.Warning,
            _ => ChatSeverity.Info
        };

    private static string? BuildDetails(ActionExecutionResult result, string label)
    {
        var sb = new StringBuilder();

        var summary = result.Summary?.Trim();
        if (!string.IsNullOrWhiteSpace(summary) && !string.Equals(summary, label, StringComparison.OrdinalIgnoreCase))
        {
            sb.AppendLine(summary);
        }

        if (result.Steps.Count > 0)
        {
            if (sb.Length > 0)
            {
                sb.AppendLine();
            }

            sb.AppendLine("Étapes:");
            foreach (var step in result.Steps)
            {
                sb.AppendLine($"- {step.Title}: {StepStatusLabel(step.Status)} — {step.Summary}");
            }
        }

        if (result.Recommendations.Count > 0)
        {
            if (sb.Length > 0)
            {
                sb.AppendLine();
            }

            sb.AppendLine("Recommandations:");
            foreach (var recommendation in result.Recommendations.Where(r => !string.IsNullOrWhiteSpace(r)))
            {
                sb.AppendLine($"- {recommendation}");
            }
        }

        return sb.Length == 0 ? null : sb.ToString().TrimEnd();
    }

    private static string StepStatusLabel(ActionResultStatus status)
        => status switch
        {
            ActionResultStatus.Success => "OK",
            ActionResultStatus.PartialSuccess => "Partiel",
            ActionResultStatus.Failed => "Échec",
            ActionResultStatus.NotAvailable => "Non dispo",
            ActionResultStatus.NotImplemented => "Non implémenté",
            ActionResultStatus.Skipped => "Ignoré",
            _ => "Inconnu"
        };
}
