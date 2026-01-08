using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Virgil.Services.Assistant;

public sealed record ProposedAction(
    string ActionId,
    string Title,
    IReadOnlyDictionary<string, string>? Parameters = null,
    bool RequiresConfirmation = true,
    string? Warning = null);

public sealed record AssistantReply(string Text, IReadOnlyList<ProposedAction> ProposedActions)
{
    public static AssistantReply Empty { get; } = new(string.Empty, Array.Empty<ProposedAction>());
}

public sealed record AssistantTelemetrySummary(
    string Cpu,
    bool CpuStale,
    string Ram,
    bool RamStale,
    string Temperature,
    bool TemperatureStale,
    string Disk,
    bool DiskStale);

public sealed record AssistantActionSummary(
    string Status,
    string Title,
    IReadOnlyList<string> Lines);

public sealed record AssistantActionCatalogItem(
    string Id,
    string Label,
    string Description,
    bool RequiresAdmin,
    bool DestructiveFlag);

public sealed record AssistantContext(
    AssistantTelemetrySummary Telemetry,
    AssistantActionSummary? LastActionResult,
    IReadOnlyList<AssistantActionCatalogItem> ActionCatalog)
{
    public static AssistantContext Empty { get; } = new(
        new AssistantTelemetrySummary(string.Empty, false, string.Empty, false, string.Empty, false, string.Empty, false),
        null,
        Array.Empty<AssistantActionCatalogItem>());
}

public interface IAssistantProvider
{
    Task<AssistantReply> AskAsync(string userMessage, AssistantContext ctx, CancellationToken ct = default);
}

public interface IAssistantService
{
    Task<AssistantReply> AskAsync(string userMessage, AssistantContext ctx, CancellationToken ct = default);
}
