using System.Collections.Generic;
using System.Linq;

namespace Virgil.Services.Startup;

public sealed record StartupItem(
    string Id,
    string Name,
    string Location,
    string Command,
    string Type,
    bool IsEssential,
    bool IsRecommended,
    bool IsSelected);

public sealed record StartupAnalysis(IReadOnlyList<StartupItem> Items)
{
    public bool HasRecommendations => Items.Any(item => item.IsRecommended && !item.IsEssential);
}

public sealed record StartupOptimizeResult
{
    public int ItemsDisabled { get; init; }
    public int ServicesSetToManual { get; init; }
    public int TasksDisabled { get; init; }
    public bool Succeeded { get; init; }
    public string Summary { get; init; } = string.Empty;
    public List<string> ActionsPerformed { get; init; } = new();
    public string? FailureReason { get; init; }
}

public sealed record StartupRestoreResult
{
    public int ItemsRestored { get; init; }
    public bool Succeeded { get; init; }
    public string Summary { get; init; } = string.Empty;
    public List<string> ActionsPerformed { get; init; } = new();
    public string? FailureReason { get; init; }
}
