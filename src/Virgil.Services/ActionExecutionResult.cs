using System.Diagnostics.CodeAnalysis;

namespace Virgil.Services;

/// <summary>
/// Normalized result returned by the action pipeline (orchestrator & services).
/// </summary>
public sealed record ActionExecutionResult(bool Success, string Message, string? Details = null)
{
    public static ActionExecutionResult Ok(string message, string? details = null) => new(true, message, details);

    public static ActionExecutionResult Failure(string message, string? details = null) => new(false, message, details);

    public static ActionExecutionResult NotAvailable(string message = "Non disponible", string? details = null)
        => new(false, message, details);

    public bool TryGetDetails([NotNullWhen(true)] out string? details)
    {
        details = Details;
        return !string.IsNullOrWhiteSpace(details);
    }
}
