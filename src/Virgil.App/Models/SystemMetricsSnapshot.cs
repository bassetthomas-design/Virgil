using System;
using System.Collections.Generic;

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
    public IReadOnlyList<GpuAdapterMetric> GpuAdapters { get; init; } = Array.Empty<GpuAdapterMetric>();
    public string? ActiveGpuName { get; init; }
    public double? ActiveGpuPercent { get; init; }
    public double? ActiveGpuTempC { get; init; }
    public double? CpuTempC { get; init; }
    public double? GpuTempC { get; init; }
    public string TempProviderName { get; init; } = string.Empty;
    public string? CpuTempSensorName { get; init; }
    public string? GpuTempSensorName { get; init; }
    public double? CpuTempRawC { get; init; }
    public double? GpuTempRawC { get; init; }
    public double? CpuTempSmoothedC { get; init; }
    public double? GpuTempSmoothedC { get; init; }
    public string SourceFlags { get; init; } = string.Empty;
    public string ProviderName { get; init; } = string.Empty;
    public double SampleAgeMs { get; init; }
}
