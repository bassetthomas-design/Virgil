using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Virgil.Services.Abstractions;

namespace Virgil.Services;

/// <summary>
/// Stub de IDiagnosticService – scan express, vérifs disque et intégrité seront branchés ensuite.
/// </summary>
public sealed class DiagnosticService : IDiagnosticService
{
    public async Task<ActionExecutionResult> RunExpressAsync(CancellationToken ct = default)
    {
        try
        {
            var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
            var cpuUsage = await SampleProcessCpuAsync(ct).ConfigureAwait(false);

            var memoryInfo = GC.GetGCMemoryInfo();
            var memoryLoad = BuildMemoryLoad(memoryInfo);

            var (diskLabel, diskUsage) = BuildDiskUsage();
            var networkStatus = BuildNetworkStatus();

            var summary =
                $"Uptime: {FormatDuration(uptime)} · CPU (app) {cpuUsage.ToString("0.0", CultureInfo.InvariantCulture)}% · " +
                $"Mémoire {memoryLoad} · {diskLabel} {diskUsage} · Réseau {networkStatus}";

            var details = new StringBuilder()
                .AppendLine("Synthèse express :")
                .AppendLine($"- Uptime système : {FormatDuration(uptime)}")
                .AppendLine($"- Charge CPU de Virgil : {cpuUsage.ToString("0.0", CultureInfo.InvariantCulture)}%")
                .AppendLine($"- Pression mémoire : {memoryLoad}")
                .AppendLine($"- {diskLabel} : {diskUsage}")
                .AppendLine($"- Réseau : {networkStatus}")
                .AppendLine()
                .AppendLine("Recommandations :")
                .AppendLine(memoryInfo.MemoryLoadBytes > memoryInfo.TotalAvailableMemoryBytes * 0.8
                    ? "- Mémoire sous tension : envisager de fermer des applications lourdes."
                    : "- RAS mémoire : utilisation stable.")
                .AppendLine(cpuUsage > 60
                    ? "- CPU sollicitée : laisser Virgil finir ou fermer les apps en pic."
                    : "- CPU calme : aucune action urgente.")
                .AppendLine(string.Equals(networkStatus, "limité", StringComparison.OrdinalIgnoreCase)
                    ? "- Réseau limité : vérifier la connexion ou le Wi‑Fi."
                    : "- Réseau OK : latence non mesurée dans ce scan.")
                .ToString();

            return ActionExecutionResult.Ok("Scan express terminé", details);
        }
        catch (OperationCanceledException)
        {
            return ActionExecutionResult.Failure("Scan express annulé");
        }
        catch (Exception ex)
        {
            return ActionExecutionResult.NotAvailable("Scan express indisponible", ex.Message);
        }
    }

    public Task<ActionExecutionResult> DiskCheckAsync(CancellationToken ct = default)
        => Task.FromResult(ActionExecutionResult.NotAvailable("Vérification disque non implémentée"));

    public Task<ActionExecutionResult> SystemIntegrityCheckAsync(CancellationToken ct = default)
        => Task.FromResult(ActionExecutionResult.NotAvailable("Vérification intégrité système non implémentée"));

    public Task<ActionExecutionResult> RescanSystemAsync(CancellationToken ct = default)
        => Task.FromResult(ActionExecutionResult.NotAvailable("Re-scan système non implémenté"));

    private static async Task<double> SampleProcessCpuAsync(CancellationToken ct)
    {
        var process = Process.GetCurrentProcess();
        var startCpu = process.TotalProcessorTime;
        var startTimestamp = Stopwatch.GetTimestamp();

        await Task.Delay(500, ct).ConfigureAwait(false);

        process.Refresh();
        var endCpu = process.TotalProcessorTime;
        var endTimestamp = Stopwatch.GetTimestamp();

        var elapsedSeconds = (endTimestamp - startTimestamp) / (double)Stopwatch.Frequency;
        if (elapsedSeconds <= 0)
        {
            return 0d;
        }

        var cpuSeconds = (endCpu - startCpu).TotalSeconds;
        var normalized = cpuSeconds / elapsedSeconds / Environment.ProcessorCount * 100d;
        return Math.Clamp(normalized, 0d, 100d);
    }

    private static string BuildMemoryLoad(GCMemoryInfo memoryInfo)
    {
        if (memoryInfo.TotalAvailableMemoryBytes <= 0 || memoryInfo.MemoryLoadBytes <= 0)
        {
            return "indisponible";
        }

        var pressure = memoryInfo.MemoryLoadBytes / (double)memoryInfo.TotalAvailableMemoryBytes * 100d;
        return pressure.ToString("0.0%", CultureInfo.InvariantCulture);
    }

    private static (string Label, string Usage) BuildDiskUsage()
    {
        try
        {
            var systemRoot = Path.GetPathRoot(Environment.SystemDirectory) ?? string.Empty;
            var drive = DriveInfo.GetDrives()
                .Where(d => d.IsReady)
                .OrderByDescending(d => systemRoot.StartsWith(d.RootDirectory.FullName, StringComparison.OrdinalIgnoreCase))
                .ThenBy(d => d.Name)
                .FirstOrDefault();

            if (drive == null)
            {
                return ("Disque", "indisponible");
            }

            var usedBytes = drive.TotalSize - drive.TotalFreeSpace;
            var usage = usedBytes / (double)drive.TotalSize;
            return ($"Disque {drive.Name.TrimEnd(Path.DirectorySeparatorChar)}",
                usage.ToString("0.0%", CultureInfo.InvariantCulture));
        }
        catch
        {
            return ("Disque", "indisponible");
        }
    }

    private static string BuildNetworkStatus()
    {
        try
        {
            return NetworkInterface.GetIsNetworkAvailable() ? "OK" : "limité";
        }
        catch
        {
            return "indisponible";
        }
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalDays >= 1)
        {
            return $"{(int)duration.TotalDays}j {duration.Hours}h";
        }

        if (duration.TotalHours >= 1)
        {
            return $"{(int)duration.TotalHours}h {duration.Minutes}m";
        }

        return $"{duration.Minutes}m";
    }
}
