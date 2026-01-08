using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Virgil.Core.Models;

namespace Virgil.Services;

/// <summary>
/// Normalized result returned by the action pipeline (orchestrator & services).
/// </summary>
public sealed record ActionExecutionResult
{
    public ActionExecutionResult(
        ActionResultStatus status,
        string title,
        string summary,
        IReadOnlyList<ActionStepResult>? steps = null,
        IReadOnlyList<string>? recommendations = null,
        string? debugInfo = null)
    {
        Status = status;
        Title = title;
        Summary = summary;
        Steps = steps ?? Array.Empty<ActionStepResult>();
        Recommendations = recommendations ?? Array.Empty<string>();
        DebugInfo = debugInfo;
    }

    public ActionResultStatus Status { get; }
    public string Title { get; }
    public string Summary { get; }
    public IReadOnlyList<ActionStepResult> Steps { get; }
    public IReadOnlyList<string> Recommendations { get; }
    public string? DebugInfo { get; }

    public bool Success => Status is ActionResultStatus.Success or ActionResultStatus.PartialSuccess;

    public string Message => string.IsNullOrWhiteSpace(Summary) ? Title : Summary;

    public string? Details => Summary;

    public bool TryGetDetails([NotNullWhen(true)] out string? details)
    {
        details = Details;
        return !string.IsNullOrWhiteSpace(details);
    }

    public static ActionExecutionResult Ok(string title, string? summary = null, IEnumerable<ActionStepResult>? steps = null, IEnumerable<string>? recommendations = null)
        => new(ActionResultStatus.Success, title, summary ?? string.Empty, steps?.ToList(), recommendations?.ToList());

    public static ActionExecutionResult Partial(string title, string? summary = null, IEnumerable<ActionStepResult>? steps = null, IEnumerable<string>? recommendations = null)
        => new(ActionResultStatus.PartialSuccess, title, summary ?? string.Empty, steps?.ToList(), recommendations?.ToList());

    public static ActionExecutionResult Failure(string title, string? summary = null, IEnumerable<ActionStepResult>? steps = null, IEnumerable<string>? recommendations = null)
        => new(ActionResultStatus.Failed, title, summary ?? string.Empty, steps?.ToList(), recommendations?.ToList());

    public static ActionExecutionResult NotAvailable(string title = "Non disponible", string? summary = null, IEnumerable<ActionStepResult>? steps = null)
        => new(ActionResultStatus.NotAvailable, title, summary ?? string.Empty, steps?.ToList());

    public static ActionExecutionResult NotImplemented(string title = "Non implémenté", string? summary = null, IEnumerable<ActionStepResult>? steps = null)
        => new(ActionResultStatus.NotImplemented, title, summary ?? string.Empty, steps?.ToList());

    public static ActionExecutionResult Skipped(string title = "Ignoré", string? summary = null, IEnumerable<ActionStepResult>? steps = null)
        => new(ActionResultStatus.Skipped, title, summary ?? string.Empty, steps?.ToList());
}
