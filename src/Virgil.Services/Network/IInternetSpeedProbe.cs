using System;
using System.Threading;
using System.Threading.Tasks;

namespace Virgil.Services.Network;

public interface IInternetSpeedProbe
{
    Task<SpeedTestProbeResult> MeasureAsync(CancellationToken ct = default);
}

public sealed record SpeedTestProbeResult(
    bool Success,
    string ServerLabel,
    double DownloadMbps,
    double UploadMbps,
    double LatencyMs,
    double? StabilityVariationPercent,
    bool UsedFallback,
    string? FailureReason = null,
    bool TimedOut = false)
{
    public static SpeedTestProbeResult Fail(string serverLabel, string reason, bool timedOut = false)
        => new(false, serverLabel, 0, 0, 0, null, false, reason, timedOut);
}
