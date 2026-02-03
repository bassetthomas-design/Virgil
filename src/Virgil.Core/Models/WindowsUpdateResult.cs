namespace Virgil.Core.Models;

public sealed record WindowsUpdateResult(
    bool Succeeded,
    int UpdatesFound,
    int UpdatesInstalled,
    bool RebootRequired,
    string Summary,
    string? FailureReason);
