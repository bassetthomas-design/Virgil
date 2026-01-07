using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Virgil.Core.Config;
using Virgil.Core.Services;
using Virgil.Services.Abstractions;

namespace Virgil.Services;

/// <summary>
/// Implémentation de l'action 4 « Scan système express ».
/// Ne fait qu'observer : aucun changement sur la machine.
/// </summary>
public sealed class DiagnosticService : IDiagnosticService
{
    private const double CpuUsageWarning = 90d;
    private const double RamUsageWarning = 90d;
    private const double DiskUsageWarning = 95d;
    private const double TemperatureWarningC = 85d;
    private const double GpuUsageWarning = 95d;

    private readonly IExpressScanCollector _collector;
    private readonly IScanHistoryStore _history;
    private readonly IClock _clock;
    private readonly IHardwareSnapshotCollector _hardware;

    public DiagnosticService()
        : this(new ExpressScanCollector(), new FileScanHistoryStore(), SystemClock.Instance, new HardwareQuickDiagnosticsCollector())
    {
    }

    internal DiagnosticService(
        IExpressScanCollector collector,
        IScanHistoryStore history,
        IClock clock,
        IHardwareSnapshotCollector? hardwareCollector = null)
    {
        _collector = collector ?? throw new ArgumentNullException(nameof(collector));
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _hardware = hardwareCollector ?? new HardwareQuickDiagnosticsCollector();
    }

    public Task<ActionExecutionResult> RunExpressAsync(CancellationToken ct = default)
        => ExecuteExpressScanAsync("Scan express terminé", ct, onCanceled: "Scan express annulé", onError: "Scan express indisponible");

    public async Task<ActionExecutionResult> RunHardwareQuickCheckAsync(CancellationToken ct = default)
    {
        try
        {
            var snapshot = await _hardware.CaptureAsync(ct).ConfigureAwait(false);
            var warnings = AnalyzeHardware(snapshot);
            var global = warnings.Count == 0 ? "OK" : "Attention";
            var summary = BuildHardwareChatMessage(global, snapshot, warnings);
            return ActionExecutionResult.Ok("Diagnostic matériel rapide terminé", summary);
        }
        catch (OperationCanceledException)
        {
            return ActionExecutionResult.Failure("Diagnostic matériel annulé");
        }
        catch (Exception ex)
        {
            return ActionExecutionResult.NotAvailable("Diagnostic matériel indisponible", ex.Message);
        }
    }

    public Task<ActionExecutionResult> DiskCheckAsync(CancellationToken ct = default)
        => Task.FromResult(ActionExecutionResult.NotImplemented("Vérification disque non implémentée"));

    public Task<ActionExecutionResult> SystemIntegrityCheckAsync(CancellationToken ct = default)
        => Task.FromResult(ActionExecutionResult.NotImplemented("Vérification intégrité système non implémentée"));

    public Task<ActionExecutionResult> RescanSystemAsync(CancellationToken ct = default)
        => ExecuteExpressScanAsync(
            "Re-scan terminé",
            ct,
            onCanceled: "Re-scan annulé",
            onError: "Re-scan indisponible");

    private async Task<ActionExecutionResult> ExecuteExpressScanAsync(
        string successMessage,
        CancellationToken ct,
        string? onCanceled = null,
        string? onError = null)
    {
        try
        {
            var snapshot = await _collector.CaptureAsync(ct).ConfigureAwait(false);
            var issues = DetectIssues(snapshot);
            var globalState = issues.Count == 0 ? "OK" : "Attention";

            var previous = await _history.LoadAsync(ct).ConfigureAwait(false);
            var resolved = previous is null
                ? new List<string>()
                : previous.Issues.Except(issues, StringComparer.OrdinalIgnoreCase).ToList();

            var persistent = issues
                .Select(i => previous?.Issues.Contains(i, StringComparer.OrdinalIgnoreCase) == true
                    ? i
                    : $"{i} (nouveau)")
                .ToList();

            var evolution = DescribeEvolution(previous, globalState, issues.Count);
            var recommendations = persistent.Count == 0 || persistent.All(i => string.Equals(i, "Aucun", StringComparison.OrdinalIgnoreCase))
                ? Array.Empty<string>()
                : BuildRecommendations(snapshot, persistent);

            var summary = BuildChatMessage(globalState, evolution, resolved, persistent, snapshot.MissingMetrics, recommendations);

            await _history.SaveAsync(new ScanHistoryEntry(_clock.Now, globalState, issues), ct).ConfigureAwait(false);

            return ActionExecutionResult.Ok(successMessage, summary);
        }
        catch (OperationCanceledException)
        {
            return ActionExecutionResult.Failure(onCanceled ?? "Scan annulé");
        }
        catch (Exception ex)
        {
            return ActionExecutionResult.NotAvailable(onError ?? "Scan indisponible", ex.Message);
        }
    }

    private static string BuildChatMessage(
        string globalState,
        string evolution,
        IReadOnlyCollection<string> resolved,
        IReadOnlyCollection<string> persistent,
        IReadOnlyCollection<string> missing,
        IReadOnlyCollection<string> recommendations)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"État global: {globalState} {(globalState == "OK" ? "(miracle)" : "(ça couine un peu)")}");
        sb.AppendLine($"Évolution: {evolution}");

        sb.AppendLine("Problèmes résolus:");
        AppendBullets(sb, resolved);

        sb.AppendLine("Problèmes persistants:");
        AppendBullets(sb, persistent);

        if (missing.Count > 0)
        {
            sb.AppendLine("Mesures indisponibles:");
            AppendBullets(sb, missing);
        }

        if (recommendations.Count > 0)
        {
            sb.AppendLine("Recommandations:");
            AppendBullets(sb, recommendations);
        }

        return sb.ToString();
    }

    private static void AppendBullets(StringBuilder sb, IReadOnlyCollection<string> items)
    {
        if (items.Count == 0)
        {
            sb.AppendLine("- Aucun");
            return;
        }

        foreach (var item in items)
        {
            sb.AppendLine($"- {item}");
        }
    }

    private static List<string> AnalyzeHardware(HardwareQuickSnapshot snapshot)
    {
        var warnings = new List<string>();

        if (snapshot.CpuUsagePercent is { } cpu && cpu > CpuUsageWarning)
        {
            warnings.Add($"CPU surchargé ({cpu.ToString("0", CultureInfo.InvariantCulture)} %)");
        }

        if (snapshot.CpuThrottlingSuspected == true)
        {
            warnings.Add("CPU: throttling détecté");
        }

        if (snapshot.RamUsagePercent is { } ram && ram > RamUsageWarning)
        {
            warnings.Add($"RAM sous tension ({ram.ToString("0", CultureInfo.InvariantCulture)} %)");
        }

        foreach (var disk in snapshot.Disks)
        {
            if (disk.UsagePercent is { } usage && usage > DiskUsageWarning)
            {
                warnings.Add($"{disk.Label} saturé ({usage.ToString("0", CultureInfo.InvariantCulture)} %)");
            }
        }

        if (snapshot.Gpu.UsagePercent is { } gpuUsage && gpuUsage > GpuUsageWarning)
        {
            warnings.Add($"GPU occupé ({gpuUsage.ToString("0", CultureInfo.InvariantCulture)} %)");
        }

        if (snapshot.Temperatures.CpuTempC is { } cpuTemp && cpuTemp > TemperatureWarningC)
        {
            warnings.Add($"Température CPU élevée ({cpuTemp.ToString("0", CultureInfo.InvariantCulture)} °C)");
        }

        if (snapshot.Temperatures.GpuTempC is { } gpuTemp && gpuTemp > TemperatureWarningC)
        {
            warnings.Add($"Température GPU élevée ({gpuTemp.ToString("0", CultureInfo.InvariantCulture)} °C)");
        }

        if (snapshot.Temperatures.DiskTempC is { } diskTemp && diskTemp > TemperatureWarningC)
        {
            warnings.Add($"Température disque élevée ({diskTemp.ToString("0", CultureInfo.InvariantCulture)} °C)");
        }

        return warnings;
    }

    private static string BuildHardwareChatMessage(string globalState, HardwareQuickSnapshot snapshot, IReadOnlyCollection<string> warnings)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Résumé global matériel: {globalState} {(globalState == "OK" ? "(rien ne crie à l'aide. Pour l'instant.)" : "(à surveiller sans dramatiser.)")}");
        sb.AppendLine($"CPU: charge {FormatPercent(snapshot.CpuUsagePercent)}, fréquence {FormatFrequency(snapshot.CpuFrequencyMHz)}, throttling: {FormatThrottling(snapshot.CpuThrottlingSuspected)}");
        sb.AppendLine($"RAM: {FormatRam(snapshot)}");
        sb.AppendLine($"Disque(s): {FormatDisks(snapshot.Disks)}");
        sb.AppendLine($"GPU: {FormatGpu(snapshot.Gpu)}");
        sb.AppendLine($"Températures: {FormatTemps(snapshot.Temperatures)}");
        sb.AppendLine($"Avertissements éventuels: {FormatWarnings(warnings)}");

        if (snapshot.MissingMetrics.Count > 0)
        {
            sb.AppendLine($"(Mesures manquantes: {string.Join(", ", snapshot.MissingMetrics)})");
        }

        return sb.ToString().TrimEnd();
    }

    private static string FormatPercent(double? value, string fallback = "non disponible")
        => value.HasValue ? $"{value.Value.ToString("0", CultureInfo.InvariantCulture)} %" : fallback;

    private static string FormatFrequency(double? value)
        => value.HasValue ? $"{value.Value.ToString("0", CultureInfo.InvariantCulture)} MHz" : "non disponible";

    private static string FormatThrottling(bool? throttling)
        => throttling switch
        {
            true => "détecté",
            false => "non détecté",
            _ => "non disponible"
        };

    private static string FormatRam(HardwareQuickSnapshot snapshot)
    {
        if (snapshot.RamTotalGb is { } total && snapshot.RamUsedGb is { } used && snapshot.RamUsagePercent is { } percent)
        {
            return $"{used.ToString("0.0", CultureInfo.InvariantCulture)} / {total.ToString("0.0", CultureInfo.InvariantCulture)} Go ({percent.ToString("0", CultureInfo.InvariantCulture)} % utilisé)";
        }

        return "non disponible";
    }

    private static string FormatDisks(IReadOnlyCollection<DiskStatus> disks)
    {
        if (disks.Count == 0)
        {
            return "non disponible";
        }

        return string.Join(" | ", disks.Select(disk =>
        {
            var usage = FormatPercent(disk.UsagePercent, "usage non disponible");
            var free = disk.FreeSpaceGb.HasValue
                ? $"{disk.FreeSpaceGb.Value.ToString("0.0", CultureInfo.InvariantCulture)} Go libres"
                : "espace libre non disponible";
            var smart = string.IsNullOrWhiteSpace(disk.SmartStatus)
                ? "SMART: non disponible"
                : $"SMART: {disk.SmartStatus}";
            return $"{disk.Label}: {usage} ({free}), {smart}";
        }));
    }

    private static string FormatGpu(GpuStatus gpu)
    {
        if (!gpu.Present)
        {
            return "non détecté (non disponible)";
        }

        var usage = gpu.UsagePercent.HasValue
            ? $"{gpu.UsagePercent.Value.ToString("0", CultureInfo.InvariantCulture)} %"
            : "charge non disponible";
        var temp = gpu.TemperatureC.HasValue
            ? $"{gpu.TemperatureC.Value.ToString("0", CultureInfo.InvariantCulture)} °C"
            : "température non disponible";

        return $"{gpu.Name}: {usage}, {temp}";
    }

    private static string FormatTemps(TemperatureSnapshot temps)
    {
        var cpu = temps.CpuTempC.HasValue
            ? $"CPU {temps.CpuTempC.Value.ToString("0", CultureInfo.InvariantCulture)} °C"
            : "CPU: non disponible";
        var gpu = temps.GpuTempC.HasValue
            ? $"GPU {temps.GpuTempC.Value.ToString("0", CultureInfo.InvariantCulture)} °C"
            : "GPU: non disponible";
        var disk = temps.DiskTempC.HasValue
            ? $"Disque {temps.DiskTempC.Value.ToString("0", CultureInfo.InvariantCulture)} °C"
            : "Disque: non disponible";

        return $"{cpu}; {gpu}; {disk}";
    }

    private static string FormatWarnings(IReadOnlyCollection<string> warnings)
    {
        if (warnings.Count == 0)
        {
            return "Aucun (profitons-en)";
        }

        return string.Join(" | ", warnings);
    }

    private static string DescribeEvolution(ScanHistoryEntry? previous, string currentState, int issueCount)
    {
        if (previous is null)
        {
            return "premier scan";
        }

        if (previous.GlobalState.Equals("Attention", StringComparison.OrdinalIgnoreCase) && currentState.Equals("OK", StringComparison.OrdinalIgnoreCase))
        {
            return "amélioré";
        }

        if (previous.GlobalState.Equals("OK", StringComparison.OrdinalIgnoreCase) && currentState.Equals("Attention", StringComparison.OrdinalIgnoreCase))
        {
            return "pire";
        }

        if (issueCount < previous.Issues.Count)
        {
            return "amélioré";
        }

        if (issueCount > previous.Issues.Count)
        {
            return "pire";
        }

        return "identique";
    }

    private static List<string> DetectIssues(ExpressScanSnapshot snapshot)
    {
        var issues = new List<string>();

        if (snapshot.CpuUsagePercent is { } cpu && cpu > 85)
        {
            issues.Add($"CPU surmenée ({cpu.ToString("0", CultureInfo.InvariantCulture)} %)" +
                       (snapshot.CpuPerformancePercent is { } perf && perf < 80 ? " + possible throttling" : string.Empty));
        }

        if (snapshot.MemoryUsagePercent is { } ram && ram > 85)
        {
            issues.Add($"Mémoire pressurisée ({ram.ToString("0", CultureInfo.InvariantCulture)} %)");
        }

        if (snapshot.DiskUsagePercent is { } disk && disk > 90)
        {
            issues.Add($"Disque {snapshot.DiskLabel} saturé ({disk.ToString("0", CultureInfo.InvariantCulture)} % utilisé)");
        }

        if (snapshot.DiskActivityPercent is { } diskAct && diskAct > 80)
        {
            issues.Add($"Disque occupé ({diskAct.ToString("0", CultureInfo.InvariantCulture)} % d'activité)");
        }

        if (snapshot.CpuTempC is { } cpuTemp && cpuTemp > 85)
        {
            issues.Add($"Température CPU haute ({cpuTemp.ToString("0", CultureInfo.InvariantCulture)} °C)");
        }

        if (snapshot.GpuTempC is { } gpuTemp && gpuTemp > 85)
        {
            issues.Add($"Température GPU haute ({gpuTemp.ToString("0", CultureInfo.InvariantCulture)} °C)");
        }

        if (snapshot.HeavyServices.Count > 0)
        {
            issues.Add($"Services Windows gourmands : {string.Join(", ", snapshot.HeavyServices)}");
        }

        if (snapshot.SuspiciousProcesses.Count > 0)
        {
            issues.Add($"Processus bruyants : {string.Join(", ", snapshot.SuspiciousProcesses)}");
        }

        if (snapshot.StartupAppsCount.HasValue && snapshot.StartupAppsCount.Value > 8)
        {
            issues.Add($"Démarrage encombré ({snapshot.StartupAppsCount.Value} entrées)");
        }

        if (snapshot.RecentErrors.Count > 0)
        {
            issues.Add($"Erreurs système récentes : {string.Join(" | ", snapshot.RecentErrors.Take(3))}");
        }

        if (snapshot.NetworkStatus == NetworkState.Limited)
        {
            issues.Add("Réseau grognon (connectivité limitée)");
        }
        else if (snapshot.NetworkStatus == NetworkState.DnsBroken)
        {
            issues.Add("DNS aux fraises (résolution impossible)");
        }

        return issues;
    }

    private static IReadOnlyCollection<string> BuildRecommendations(ExpressScanSnapshot snapshot, IReadOnlyCollection<string> persistentIssues)
    {
        var list = new List<string>();

        if (persistentIssues.Any(i => i.Contains("CPU", StringComparison.OrdinalIgnoreCase)))
        {
            list.Add("Fermer les applis qui chauffent et laisser la machine respirer.");
        }

        if (persistentIssues.Any(i => i.Contains("Mémoire", StringComparison.OrdinalIgnoreCase)))
        {
            list.Add("Alléger les onglets/applications ; un redémarrage ne ferait pas de mal.");
        }

        if (persistentIssues.Any(i => i.Contains("Disque", StringComparison.OrdinalIgnoreCase)))
        {
            list.Add("Prévoir un ménage sur le disque ou déplacer des fichiers trop gourmands.");
        }

        if (persistentIssues.Any(i => i.Contains("Température", StringComparison.OrdinalIgnoreCase)))
        {
            list.Add("Vérifier la ventilation (poussière, support aéré) et éviter les charges prolongées.");
        }

        if (persistentIssues.Any(i => i.Contains("Services Windows", StringComparison.OrdinalIgnoreCase)))
        {
            list.Add("Garder un œil sur les services listés ; si anormal, envisager un redémarrage plus tard.");
        }

        if (persistentIssues.Any(i => i.Contains("Processus", StringComparison.OrdinalIgnoreCase)))
        {
            list.Add("Observer les processus signalés via le Gestionnaire des tâches avant qu'ils ne se prennent pour des divas.");
        }

        if (persistentIssues.Any(i => i.Contains("Démarrage encombré", StringComparison.OrdinalIgnoreCase)))
        {
            list.Add("Désactiver les apps inutiles au démarrage (sans tout casser, promis).");
        }

        if (persistentIssues.Any(i => i.Contains("Erreurs système", StringComparison.OrdinalIgnoreCase)))
        {
            list.Add("Consulter l'observateur d'événements pour voir qui râle en coulisse.");
        }

        if (persistentIssues.Any(i => i.Contains("Réseau", StringComparison.OrdinalIgnoreCase) || i.Contains("DNS", StringComparison.OrdinalIgnoreCase)))
        {
            list.Add("Vérifier la connexion/DNS, éventuellement relancer la box (sans me demander de le faire à ta place).");
        }

        return list;
    }
}

internal interface IHardwareSnapshotCollector
{
    Task<HardwareQuickSnapshot> CaptureAsync(CancellationToken ct);
}

internal sealed record HardwareQuickSnapshot
{
    public double? CpuUsagePercent { get; init; }
    public double? CpuFrequencyMHz { get; init; }
    public bool? CpuThrottlingSuspected { get; init; }
    public double? RamTotalGb { get; init; }
    public double? RamUsedGb { get; init; }
    public double? RamUsagePercent { get; init; }
    public IReadOnlyCollection<DiskStatus> Disks { get; init; } = Array.Empty<DiskStatus>();
    public GpuStatus Gpu { get; init; } = GpuStatus.NotPresent();
    public TemperatureSnapshot Temperatures { get; init; } = new();
    public IReadOnlyCollection<string> MissingMetrics { get; init; } = Array.Empty<string>();
}

internal sealed record DiskStatus(string Label, double? UsagePercent, double? FreeSpaceGb, string SmartStatus);

internal sealed record GpuStatus(string? Name, double? UsagePercent, double? TemperatureC)
{
    public bool Present => !string.IsNullOrWhiteSpace(Name);

    public static GpuStatus NotPresent() => new(null, null, null);
}

internal sealed record TemperatureSnapshot(double? CpuTempC = null, double? GpuTempC = null, double? DiskTempC = null);

internal sealed class HardwareQuickDiagnosticsCollector : IHardwareSnapshotCollector
{
    public async Task<HardwareQuickSnapshot> CaptureAsync(CancellationToken ct)
    {
        var missing = new List<string>();

        var cpuUsage = await SampleCounterAsync("Processor", "% Processor Time", "_Total", ct).ConfigureAwait(false);
        var cpuPerf = await SampleCounterAsync("Processor Information", "% Processor Performance", "_Total", ct).ConfigureAwait(false);
        var cpuFreq = await SampleCounterAsync("Processor Information", "Processor Frequency", "_Total", ct).ConfigureAwait(false);

        bool? throttling = null;
        if (cpuUsage.HasValue && cpuPerf.HasValue)
        {
            throttling = cpuUsage.Value > 70 && cpuPerf.Value < 75;
        }

        var memory = GetMemoryStatus();
        if (memory is null)
        {
            missing.Add("RAM: non disponible");
        }

        var disks = GetDisks(missing);
        var temperatures = ReadTemperatures(missing);
        var gpu = DetectGpu(temperatures, missing);

        return new HardwareQuickSnapshot
        {
            CpuUsagePercent = cpuUsage,
            CpuFrequencyMHz = cpuFreq,
            CpuThrottlingSuspected = throttling,
            RamTotalGb = memory?.TotalGb,
            RamUsedGb = memory?.UsedGb,
            RamUsagePercent = memory?.UsagePercent,
            Disks = disks,
            Gpu = gpu,
            Temperatures = temperatures,
            MissingMetrics = missing
        };
    }

    private static async Task<double?> SampleCounterAsync(string category, string counter, string instance, CancellationToken ct)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return null;
        }

        try
        {
            using var pc = new PerformanceCounter(category, counter, instance, true);
            pc.NextValue();
            await Task.Delay(TimeSpan.FromMilliseconds(250), ct).ConfigureAwait(false);
            return Math.Clamp(pc.NextValue(), 0, 10_000);
        }
        catch
        {
            return null;
        }
    }

    private static MemoryStatus? GetMemoryStatus()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return null;
        }

        MEMORYSTATUSEX status = new();
        try
        {
            if (GlobalMemoryStatusEx(ref status))
            {
                var totalGb = status.ullTotalPhys / (1024d * 1024d * 1024d);
                var freeGb = status.ullAvailPhys / (1024d * 1024d * 1024d);
                var usedGb = totalGb - freeGb;
                var usagePercent = Math.Clamp(usedGb / totalGb * 100d, 0d, 100d);
                return new MemoryStatus(totalGb, usedGb, usagePercent);
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }

    private static IReadOnlyCollection<DiskStatus> GetDisks(List<string> missing)
    {
        try
        {
            var ready = DriveInfo.GetDrives().Where(d => d.IsReady).ToList();
            if (ready.Count == 0)
            {
                missing.Add("Disques: non disponible");
                return Array.Empty<DiskStatus>();
            }

            var disks = new List<DiskStatus>();
            foreach (var drive in ready)
            {
                double? usage = null;
                double? freeGb = null;
                try
                {
                    usage = Math.Clamp((double)(drive.TotalSize - drive.TotalFreeSpace) / drive.TotalSize * 100d, 0d, 100d);
                    freeGb = drive.TotalFreeSpace / (1024d * 1024d * 1024d);
                }
                catch
                {
                    missing.Add($"Disque {drive.Name.TrimEnd(Path.DirectorySeparatorChar)}: impossible de lire l'espace");
                }

                disks.Add(new DiskStatus($"Disque {drive.Name.TrimEnd(Path.DirectorySeparatorChar)}", usage, freeGb, "non disponible"));
            }

            missing.Add("SMART: non disponible");
            return disks;
        }
        catch
        {
            missing.Add("Disques: non disponible");
            return Array.Empty<DiskStatus>();
        }
    }

    private static TemperatureSnapshot ReadTemperatures(List<string> missing)
    {
        try
        {
            using var reader = new AdvancedMonitoringService();
            var temps = reader.Read();
            if (!temps.CpuTempC.HasValue) missing.Add("Température CPU: non disponible");
            if (!temps.GpuTempC.HasValue) missing.Add("Température GPU: non disponible");
            if (!temps.DiskTempC.HasValue) missing.Add("Température disque: non disponible");
            return new TemperatureSnapshot(temps.CpuTempC, temps.GpuTempC, temps.DiskTempC);
        }
        catch
        {
            missing.Add("Températures matérielles: non disponible");
            return new TemperatureSnapshot();
        }
    }

    private static GpuStatus DetectGpu(TemperatureSnapshot temps, List<string> missing)
    {
        var name = TryGetGpuName();
        double? usage = null;

        if (usage is null)
        {
            missing.Add("Charge GPU: non disponible");
        }

        if (string.IsNullOrWhiteSpace(name) && temps.GpuTempC is null && usage is null)
        {
            return GpuStatus.NotPresent();
        }

        return new GpuStatus(string.IsNullOrWhiteSpace(name) ? "GPU principal" : name, usage, temps.GpuTempC);
    }

    private static string? TryGetGpuName()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return null;
        }

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
            foreach (var obj in searcher.Get())
            {
                var name = obj["Name"]?.ToString();
                if (!string.IsNullOrWhiteSpace(name))
                {
                    return name;
                }
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }

    private sealed record MemoryStatus(double TotalGb, double UsedGb, double UsagePercent);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
        public MEMORYSTATUSEX()
        {
            dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            dwMemoryLoad = 0;
            ullTotalPhys = ullAvailPhys = ullTotalPageFile = ullAvailPageFile = 0;
            ullTotalVirtual = ullAvailVirtual = ullAvailExtendedVirtual = 0;
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
}

internal interface IExpressScanCollector
{
    Task<ExpressScanSnapshot> CaptureAsync(CancellationToken ct);
}

internal interface IScanHistoryStore
{
    Task<ScanHistoryEntry?> LoadAsync(CancellationToken ct);
    Task SaveAsync(ScanHistoryEntry entry, CancellationToken ct);
}

internal interface IClock
{
    DateTimeOffset Now { get; }
}

internal sealed class SystemClock : IClock
{
    public static readonly SystemClock Instance = new();
    private SystemClock() { }
    public DateTimeOffset Now => DateTimeOffset.UtcNow;
}

internal sealed record ScanHistoryEntry(DateTimeOffset Timestamp, string GlobalState, List<string> Issues);

internal sealed class ExpressScanSnapshot
{
    public double? CpuUsagePercent { get; init; }
    public double? CpuPerformancePercent { get; init; }
    public double? CpuFrequencyMHz { get; init; }
    public double? MemoryUsagePercent { get; init; }
    public double? DiskUsagePercent { get; init; }
    public double? DiskActivityPercent { get; init; }
    public double? CpuTempC { get; init; }
    public double? GpuTempC { get; init; }
    public double? DiskTempC { get; init; }
    public string DiskLabel { get; init; } = "Disque";
    public NetworkState NetworkStatus { get; init; } = NetworkState.Unknown;
    public IReadOnlyCollection<string> HeavyServices { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<string> SuspiciousProcesses { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<string> RecentErrors { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<string> MissingMetrics { get; init; } = Array.Empty<string>();
    public int? StartupAppsCount { get; init; }
}

internal enum NetworkState
{
    Unknown,
    Ok,
    Limited,
    DnsBroken
}

internal sealed record ProcessSnapshot(int Id, string Name, double CpuPercent, double MemoryMb, string? Path);

internal sealed class ExpressScanCollector : IExpressScanCollector
{
    private const double DefaultDelayMs = 300;

    public async Task<ExpressScanSnapshot> CaptureAsync(CancellationToken ct)
    {
        var missing = new List<string>();

        var cpuUsage = await SampleCounterAsync("Processor", "% Processor Time", "_Total", ct).ConfigureAwait(false);
        var cpuPerf = await SampleCounterAsync("Processor Information", "% Processor Performance", "_Total", ct).ConfigureAwait(false);
        var cpuFreq = await SampleCounterAsync("Processor Information", "Processor Frequency", "_Total", ct).ConfigureAwait(false);
        var memUsage = GetMemoryUsagePercent();
        var (diskLabel, diskUsage) = GetDiskUsage();
        var diskActivity = await SampleCounterAsync("PhysicalDisk", "% Disk Time", "_Total", ct).ConfigureAwait(false);

        float? cpuTemp = null, gpuTemp = null, diskTemp = null;
        try
        {
            using var tempReader = new AdvancedMonitoringService();
            var temps = tempReader.Read();
            cpuTemp = temps.CpuTempC;
            gpuTemp = temps.GpuTempC;
            diskTemp = temps.DiskTempC;
            if (!temps.CpuTempC.HasValue) missing.Add("Température CPU: non dispo ici");
            if (!temps.GpuTempC.HasValue) missing.Add("Température GPU: non dispo ici");
            if (!temps.DiskTempC.HasValue) missing.Add("Température disque: non dispo ici");
        }
        catch
        {
            missing.Add("Températures matérielles: non dispo ici");
        }

        var processes = await SampleProcessesAsync(ct).ConfigureAwait(false);
        var heavyServices = TryGetHeavyServices(processes, missing);
        var suspicious = DetectSuspiciousProcesses(processes);
        var startupCount = TryGetStartupAppsCount(missing);
        var recentErrors = TryReadRecentErrors(missing);
        var network = CheckNetworkState();

        return new ExpressScanSnapshot
        {
            CpuUsagePercent = cpuUsage,
            CpuPerformancePercent = cpuPerf,
            CpuFrequencyMHz = cpuFreq,
            MemoryUsagePercent = memUsage,
            DiskUsagePercent = diskUsage,
            DiskActivityPercent = diskActivity,
            CpuTempC = cpuTemp,
            GpuTempC = gpuTemp,
            DiskTempC = diskTemp,
            DiskLabel = diskLabel,
            HeavyServices = heavyServices,
            SuspiciousProcesses = suspicious,
            StartupAppsCount = startupCount,
            RecentErrors = recentErrors,
            NetworkStatus = network,
            MissingMetrics = missing
        };
    }

    private static async Task<double?> SampleCounterAsync(string category, string counter, string instance, CancellationToken ct)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return null;
        }

        try
        {
            using var pc = new PerformanceCounter(category, counter, instance, true);
            pc.NextValue();
            await Task.Delay(TimeSpan.FromMilliseconds(DefaultDelayMs), ct).ConfigureAwait(false);
            return Math.Clamp(pc.NextValue(), 0, 1000);
        }
        catch
        {
            return null;
        }
    }

    private static double? GetMemoryUsagePercent()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return null;
        }

        MEMORYSTATUSEX status = new MEMORYSTATUSEX();
        try
        {
            if (GlobalMemoryStatusEx(ref status))
            {
                ulong used = status.ullTotalPhys - status.ullAvailPhys;
                return Math.Clamp((double)used / status.ullTotalPhys * 100.0, 0d, 100d);
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }

    private static (string Label, double?) GetDiskUsage()
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
                return ("Disque", null);
            }

            var usedBytes = drive.TotalSize - drive.TotalFreeSpace;
            var usage = usedBytes / (double)drive.TotalSize * 100d;
            return ($"Disque {drive.Name.TrimEnd(Path.DirectorySeparatorChar)}", Math.Clamp(usage, 0, 100));
        }
        catch
        {
            return ("Disque", null);
        }
    }

    private static async Task<IReadOnlyCollection<ProcessSnapshot>> SampleProcessesAsync(CancellationToken ct)
    {
        var startTimes = new Dictionary<int, TimeSpan>();
        var snapshots = new List<ProcessSnapshot>();
        try
        {
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    startTimes[p.Id] = p.TotalProcessorTime;
                }
                catch { }
            }
        }
        catch { }

        var sw = Stopwatch.StartNew();
        await Task.Delay(TimeSpan.FromMilliseconds(DefaultDelayMs), ct).ConfigureAwait(false);
        sw.Stop();
        var elapsedSeconds = Math.Max(sw.Elapsed.TotalSeconds, 0.1);

        try
        {
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    if (!startTimes.TryGetValue(p.Id, out var startCpu))
                    {
                        continue;
                    }

                    var cpuDelta = (p.TotalProcessorTime - startCpu).TotalSeconds;
                    var cpuPercent = Math.Clamp(cpuDelta / elapsedSeconds / Environment.ProcessorCount * 100d, 0d, 100d);
                    var memMb = p.WorkingSet64 / 1024d / 1024d;
                    var path = TryGetPath(p);
                    snapshots.Add(new ProcessSnapshot(p.Id, p.ProcessName, cpuPercent, memMb, path));
                }
                catch
                {
                    // ignore processes that vanish or block access
                }
            }
        }
        catch { }

        return snapshots;
    }

    private static string? TryGetPath(Process p)
    {
        try
        {
            return p.MainModule?.FileName;
        }
        catch
        {
            return null;
        }
    }

    private static IReadOnlyCollection<string> TryGetHeavyServices(IReadOnlyCollection<ProcessSnapshot> processes, List<string> missing)
    {
        var heavy = new List<string>();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, DisplayName, ProcessId FROM Win32_Service WHERE State='Running'");
            foreach (ManagementObject svc in searcher.Get())
            {
                var pid = svc["ProcessId"] as uint?;
                var name = Convert.ToString(svc["DisplayName"], CultureInfo.InvariantCulture) ?? Convert.ToString(svc["Name"], CultureInfo.InvariantCulture) ?? "Service";
                if (pid is null)
                {
                    continue;
                }

                var procInfo = processes.FirstOrDefault(p => p.Id == (int)pid);
                if (procInfo is null)
                {
                    continue;
                }

                if (procInfo.CpuPercent > 15 || procInfo.MemoryMb > 300)
                {
                    heavy.Add($"{name} ({procInfo.CpuPercent.ToString("0", CultureInfo.InvariantCulture)}% CPU, {procInfo.MemoryMb.ToString("0", CultureInfo.InvariantCulture)} Mo)");
                }
            }
        }
        catch
        {
            missing.Add("Services Windows: non accessibles ici");
        }

        return heavy;
    }

    private static IReadOnlyCollection<string> DetectSuspiciousProcesses(IReadOnlyCollection<ProcessSnapshot> processes)
    {
        var list = new List<string>();
        var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        var candidates = processes
            .Where(p => !IsSystemProcess(p, systemRoot) && (p.CpuPercent > 20 || p.MemoryMb > 700))
            .OrderByDescending(p => p.CpuPercent + p.MemoryMb / 10)
            .Take(3);

        foreach (var p in candidates)
        {
            list.Add($"{p.Name} ({p.CpuPercent.ToString("0", CultureInfo.InvariantCulture)}% CPU, {p.MemoryMb.ToString("0", CultureInfo.InvariantCulture)} Mo)");
        }

        return list;
    }

    private static bool IsSystemProcess(ProcessSnapshot snapshot, string systemRoot)
    {
        if (string.IsNullOrWhiteSpace(systemRoot))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(snapshot.Path))
        {
            return snapshot.Name.Equals("System", StringComparison.OrdinalIgnoreCase)
                   || snapshot.Name.Equals("Idle", StringComparison.OrdinalIgnoreCase)
                   || snapshot.Name.StartsWith("svchost", StringComparison.OrdinalIgnoreCase);
        }

        return snapshot.Path.StartsWith(systemRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static int? TryGetStartupAppsCount(List<string> missing)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Command FROM Win32_StartupCommand");
            var count = searcher.Get()?.Count;
            return count;
        }
        catch
        {
            missing.Add("Applications au démarrage: non accessibles ici");
            return null;
        }
    }

    private static IReadOnlyCollection<string> TryReadRecentErrors(List<string> missing)
    {
        var errors = new List<string>();
        try
        {
            var now = DateTime.Now;
            foreach (var logName in new[] { "System", "Application" })
            {
                using var log = new EventLog(logName);
                var recent = log.Entries.Cast<EventLogEntry>()
                    .Where(e => e.EntryType == EventLogEntryType.Error && (now - e.TimeGenerated).TotalHours <= 6)
                    .OrderByDescending(e => e.TimeGenerated)
                    .Take(3);
                errors.AddRange(recent.Select(e => $"{logName}: {e.Source} ({e.InstanceId})"));
            }
        }
        catch
        {
            missing.Add("Journal d'événements: lecture impossible ici");
        }

        return errors;
    }

    private static NetworkState CheckNetworkState()
    {
        try
        {
            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                return NetworkState.Limited;
            }

            try
            {
                var ping = new Ping();
                var reply = ping.Send("1.1.1.1", 500);
                if (reply?.Status != IPStatus.Success)
                {
                    return NetworkState.Limited;
                }
            }
            catch
            {
                // ignore ping failures, keep OK unless DNS fails
            }

            try
            {
                Dns.GetHostEntry("example.com");
            }
            catch
            {
                return NetworkState.DnsBroken;
            }

            return NetworkState.Ok;
        }
        catch
        {
            return NetworkState.Unknown;
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
        public MEMORYSTATUSEX()
        {
            dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            dwMemoryLoad = 0;
            ullTotalPhys = ullAvailPhys = ullTotalPageFile = ullAvailPageFile = 0;
            ullTotalVirtual = ullAvailVirtual = ullAvailExtendedVirtual = 0;
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
}

internal sealed class FileScanHistoryStore : IScanHistoryStore
{
    private static readonly string HistoryPath = Path.Combine(AppPaths.UserDataRoot, "state", "scan-express.json");

    public async Task<ScanHistoryEntry?> LoadAsync(CancellationToken ct)
    {
        try
        {
            if (!File.Exists(HistoryPath))
            {
                return null;
            }

            await using var stream = File.OpenRead(HistoryPath);
            return await JsonSerializer.DeserializeAsync<ScanHistoryEntry>(stream, cancellationToken: ct).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    public async Task SaveAsync(ScanHistoryEntry entry, CancellationToken ct)
    {
        try
        {
            var dir = Path.GetDirectoryName(HistoryPath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            await using var stream = File.Open(HistoryPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(stream, entry, cancellationToken: ct).ConfigureAwait(false);
        }
        catch
        {
            // Best effort: history is optional.
        }
    }
}
