using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Virgil.Services;

public enum AutoUpdateToggle
{
    Enable,
    Disable
}

public sealed record AutoUpdateUserIntent(
    AutoUpdateToggle? Toggle = null,
    bool ForceRescan = true);

public sealed record AutomaticUpdateSnapshot(
    bool Supported,
    bool AutomaticUpdatesEnabled,
    bool AdminRequiredForChanges,
    bool HasAdministrativeAccess,
    bool ChangeApplied,
    IReadOnlyList<string> AvailableUpdates,
    string StatusDetails,
    string ScanDetails,
    string Recommendation,
    bool ConflictDetected)
{
    public static AutomaticUpdateSnapshot Unsupported(string reason)
        => new(false, false, true, false, false, Array.Empty<string>(), reason, reason, reason, true);
}

public interface IAutomaticUpdateDataSource
{
    Task<AutomaticUpdateSnapshot> CaptureAsync(AutoUpdateUserIntent intent, CancellationToken ct = default);
}
