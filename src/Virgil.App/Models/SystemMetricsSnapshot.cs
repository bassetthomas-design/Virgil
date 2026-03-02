using System;

namespace Virgil.App.Models;

public sealed class SystemMetricsSnapshot
{
    public DateTimeOffset Timestamp { get; init; }
    public double? CpuPercent { get; init; }
    public double? RamPercent { get; init; }
    public ulong? RamUsedBytes { get; init; }
    public ulong? RamTotalBytes { get; init; }
    public double? DiskActivePercent { get; init; }
    public double? DiskReadBps { get; init; }
    public double? DiskWriteBps { get; init; }
    public double? GpuPercent { get; init; }
    public double? CpuTempC { get; init; }
    public double? GpuTempC { get; init; }
    public string SourceFlags { get; init; } = string.Empty;
    public string ProviderName { get; init; } = string.Empty;
    public double SampleAgeMs { get; init; }
}
