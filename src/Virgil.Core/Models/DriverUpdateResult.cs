using System.Collections.Generic;

namespace Virgil.Core.Models;

public sealed record DriverUpdateResult(
    bool Succeeded,
    int Found,
    int Installed,
    bool RebootRequired,
    List<DriverUpdateItem> Items,
    string Summary,
    string? FailureReason);
