using System.Collections.Generic;
using System.Linq;

namespace Virgil.Services.Startup;

public sealed record StartupAnalysis(StartupAnalysisReport Report)
{
    public IReadOnlyList<StartupAnalysisItem> Items => Report.Items;
    public bool HasRecommendations => Report.Items.Any(item => item.RecommendedForDisable);
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
