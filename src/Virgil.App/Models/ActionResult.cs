using System.Collections.Generic;
using Virgil.Core.Models;

namespace Virgil.App.Models
{
    public sealed record ActionResult(
        ActionResultStatus Status,
        string Title,
        string Summary,
        IReadOnlyList<ActionStepResult>? Steps = null,
        IReadOnlyList<string>? Recommendations = null,
        string? DebugInfo = null)
    {
        public bool Success => Status is ActionResultStatus.Success or ActionResultStatus.PartialSuccess;

        public string Message => string.IsNullOrWhiteSpace(Summary) ? Title : Summary;

        public static ActionResult Completed(string message = "")
            => new(ActionResultStatus.Success, message, string.Empty);

        public static ActionResult PartialSuccess(string title, string? summary = null)
            => new(ActionResultStatus.PartialSuccess, title, summary ?? string.Empty);

        public static ActionResult Failure(string message)
            => new(ActionResultStatus.Failed, message, string.Empty);

        public static ActionResult NotAvailable(string title, string? summary = null)
            => new(ActionResultStatus.NotAvailable, title, summary ?? string.Empty);

        public static ActionResult NotImplemented(string title = "Action non implémentée", string? summary = null)
            => new(ActionResultStatus.NotImplemented, title, summary ?? string.Empty);

        public static ActionResult Skipped(string title, string? summary = null)
            => new(ActionResultStatus.Skipped, title, summary ?? string.Empty);
    }
}
