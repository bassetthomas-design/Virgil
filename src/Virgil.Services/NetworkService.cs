using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Virgil.Services.Abstractions;
using Virgil.Services.Network;

namespace Virgil.Services;

public sealed class NetworkService : INetworkService
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(20);
    private const int PingCount = 10; // Aligné avec l'ancienne implémentation UI (10 paquets)
    private const int PingTimeoutMs = 800;
    private const int LatencyWarningThresholdMs = 80; // Cf. docs/ARCHITECTURE.md
    private const int PacketLossWarningThresholdPercent = 5; // Seuil conservateur en l'absence de spec dédiée
    private const int JitterWarningThresholdMs = 25; // Seuil conservateur documenté en code
    private const string ExternalStableHost = "1.1.1.1"; // Référence historique du projet (Cloudflare)

    private readonly INetworkCommandRunner _runner;
    private readonly IPrivilegeChecker _privilegeChecker;
    private readonly IPlatformInfo _platform;
    private readonly IPingClient _ping;
    private readonly INetworkInfoProvider _networkInfo;

    public NetworkService(
        INetworkCommandRunner? runner = null,
        IPrivilegeChecker? privilegeChecker = null,
        IPlatformInfo? platformInfo = null,
        IPingClient? pingClient = null,
        INetworkInfoProvider? networkInfoProvider = null)
    {
        _runner = runner ?? new NetworkCommandRunner();
        _privilegeChecker = privilegeChecker ?? new WindowsPrivilegeChecker();
        _platform = platformInfo ?? new RuntimePlatformInfo();
        _ping = pingClient ?? new RuntimePingClient();
        _networkInfo = networkInfoProvider ?? new RuntimeNetworkInfoProvider();
    }

    public Task<ActionExecutionResult> RunQuickDiagnosticAsync(CancellationToken ct = default)
        => Task.FromResult(ActionExecutionResult.NotAvailable("Diagnostic réseau non implémenté"));

    public async Task<ActionExecutionResult> SoftResetAsync(CancellationToken ct = default)
    {
        if (!_platform.IsWindows())
        {
            return ActionExecutionResult.NotAvailable("Reset réseau (soft) uniquement disponible sur Windows");
        }

        var isAdmin = _privilegeChecker.IsAdministrator();
        var steps = new List<StepResult>();

        steps.Add(await RunCommandStepAsync("Flush DNS", "ipconfig", "/flushdns", requiresAdmin: false, ct));
        steps.Add(await RunCommandStepAsync("Release IP", "ipconfig", "/release", requiresAdmin: false, ct));
        steps.Add(await RunCommandStepAsync("Renew IP", "ipconfig", "/renew", requiresAdmin: false, ct));
        steps.Add(await RunCommandStepAsync("Reset Winsock léger", "netsh", "winsock reset", requiresAdmin: true, ct));
        steps.Add(await RunCommandStepAsync("Reset cache réseau Windows", "netsh", "interface ip delete arpcache", requiresAdmin: true, ct));
        steps.Add(await ResetCustomDnsAsync(isAdmin, ct));
        steps.Add(await RefreshAdaptersAsync(isAdmin, ct));

        var globalStatus = ComputeGlobalStatus(steps);
        var summary = BuildSummary(globalStatus, steps, isAdmin);

        return globalStatus == StepStatus.Failed
            ? ActionExecutionResult.Failure(summary)
            : ActionExecutionResult.Ok(summary);
    }

    public async Task<ActionExecutionResult> AdvancedResetAsync(CancellationToken ct = default)
    {
        if (!_platform.IsWindows())
        {
            return ActionExecutionResult.NotAvailable("Reset réseau (complet) uniquement disponible sur Windows");
        }

        if (!_privilegeChecker.IsAdministrator())
        {
            const string message = "Reset réseau (complet) nécessite les droits administrateur. Aucun changement effectué.";
            const string details = "Relancez en mode administrateur si vous voulez vraiment tout remettre d'équerre.";
            return ActionExecutionResult.NotAvailable(message, details);
        }

        var steps = new List<StepResult>();

        steps.Add(await RunCommandStepAsync("Reset complet Winsock", "netsh", "winsock reset", requiresAdmin: true, ct, result => DetectRebootSignal(result, RebootAdvice.Recommended)));
        steps.Add(await RunCommandStepAsync("Reset pile TCP/IP", "netsh", "int ip reset", requiresAdmin: true, ct, result => DetectRebootSignal(result, RebootAdvice.Recommended)));
        steps.Add(await RefreshAdaptersAsync(isAdmin: true, ct, hardReset: true));
        steps.Add(await ResetCustomIpAsync(ct));
        steps.Add(await ResetCustomDnsAsync(isAdmin: true, ct));
        steps.Add(await RemoveWifiProfilesAsync(ct));
        steps.Add(await RemoveEthernetProfilesAsync(ct));
        steps.Add(await RestartNetworkServicesAsync(ct));

        var globalStatus = ComputeGlobalStatus(steps);
        var summary = BuildAdvancedSummary(globalStatus, steps);

        return globalStatus == StepStatus.Failed
            ? ActionExecutionResult.Failure(summary)
            : ActionExecutionResult.Ok(summary);
    }

    public async Task<ActionExecutionResult> RunLatencyTestAsync(CancellationToken ct = default)
    {
        var gateway = _networkInfo.GetDefaultGateway();
        var gatewayResult = string.IsNullOrWhiteSpace(gateway)
            ? LatencyProbeResult.MissingGateway()
            : await ProbeAsync("Passerelle locale", gateway!, ct).ConfigureAwait(false);

        var externalResult = await ProbeAsync($"Serveur externe stable ({ExternalStableHost})", ExternalStableHost, ct).ConfigureAwait(false);

        var summary = BuildLatencySummary(gatewayResult, externalResult);

        return ActionExecutionResult.Ok(summary);
    }

    private async Task<StepResult> RunCommandStepAsync(string label, string fileName, string args, bool requiresAdmin, CancellationToken ct, Func<NetworkCommandResult, RebootAdvice>? rebootDetector = null)
    {
        if (requiresAdmin && !_privilegeChecker.IsAdministrator())
        {
            return StepResult.Ignored(label, "Droits admin requis");
        }

        var result = await _runner.RunAsync(fileName, args, CommandTimeout, ct).ConfigureAwait(false);
        if (result.Success)
        {
            var reboot = rebootDetector?.Invoke(result) ?? RebootAdvice.None;
            return StepResult.Ok(label, "Terminé", reboot);
        }

        var error = string.IsNullOrWhiteSpace(result.Error) ? "Erreur inconnue" : result.Error!;
        return StepResult.Failed(label, error);
    }

    private async Task<StepResult> ResetCustomDnsAsync(bool isAdmin, CancellationToken ct)
    {
        const string label = "Réinitialiser DNS custom";
        if (!isAdmin)
        {
            return StepResult.Ignored(label, "Droits admin requis");
        }

        var adapters = EnumerateTargetAdapters().ToList();
        if (adapters.Count == 0)
        {
            return StepResult.Ignored(label, "Aucun adaptateur réseau éligible");
        }

        var failures = 0;
        foreach (var adapter in adapters)
        {
            var name = adapter.Name;
            var res4 = await _runner.RunAsync("netsh", $"interface ip set dns name=\"{name}\" source=dhcp", CommandTimeout, ct).ConfigureAwait(false);
            var res6 = await _runner.RunAsync("netsh", $"interface ipv6 set dnsservers \"{name}\" source=dhcp", CommandTimeout, ct).ConfigureAwait(false);

            if (!res4.Success || !res6.Success)
            {
                failures++;
            }
        }

        if (failures == adapters.Count)
        {
            return StepResult.Failed(label, "Impossible de remettre le DNS en automatique");
        }

        return failures > 0
            ? StepResult.Ok(label, $"Appliqué avec avertissements sur {failures} adaptateur(s)")
            : StepResult.Ok(label, "DNS remis en automatique");
    }

    private async Task<StepResult> ResetCustomIpAsync(CancellationToken ct)
    {
        const string label = "Suppression configs IP custom";
        if (!_privilegeChecker.IsAdministrator())
        {
            return StepResult.Ignored(label, "Droits admin requis");
        }

        var adapters = EnumerateTargetAdapters().ToList();
        if (adapters.Count == 0)
        {
            return StepResult.Ignored(label, "Aucun adaptateur réseau éligible");
        }

        var failures = 0;
        foreach (var adapter in adapters)
        {
            var name = adapter.Name;
            var res4 = await _runner.RunAsync("netsh", $"interface ip set address name=\"{name}\" source=dhcp", CommandTimeout, ct).ConfigureAwait(false);
            var res6 = await _runner.RunAsync("netsh", $"interface ipv6 set address name=\"{name}\" source=dhcp", CommandTimeout, ct).ConfigureAwait(false);

            if (!res4.Success || !res6.Success)
            {
                failures++;
            }
        }

        if (failures == adapters.Count)
        {
            return StepResult.Failed(label, "Impossible de remettre les IP en automatique");
        }

        return failures > 0
            ? StepResult.Ok(label, $"Partiel ({failures} adaptateur(s) en échec)")
            : StepResult.Ok(label, "IP repassées en automatique");
    }

    private async Task<StepResult> RefreshAdaptersAsync(bool isAdmin, CancellationToken ct, bool hardReset = false)
    {
        var label = hardReset ? "Réinitialisation adaptateurs réseau" : "Réinitialiser adaptateurs (soft)";
        if (!isAdmin)
        {
            return StepResult.Ignored(label, "Droits admin requis");
        }

        var adapterQuery = EnumerateTargetAdapters();
        if (!hardReset)
        {
            adapterQuery = adapterQuery.Where(a => a.OperationalStatus == OperationalStatus.Up);
        }

        var adapters = adapterQuery.ToList();
        if (adapters.Count == 0)
        {
            return StepResult.Ignored(label, hardReset ? "Aucun adaptateur réseau détecté" : "Aucun adaptateur actif");
        }

        var failures = 0;
        foreach (var adapter in adapters)
        {
            var name = adapter.Name;
            var disable = await _runner.RunAsync("netsh", $"interface set interface name=\"{name}\" admin=disable", CommandTimeout, ct).ConfigureAwait(false);
            var enable = await _runner.RunAsync("netsh", $"interface set interface name=\"{name}\" admin=enable", CommandTimeout, ct).ConfigureAwait(false);

            if (!disable.Success || !enable.Success)
            {
                failures++;
            }
        }

        if (failures == adapters.Count)
        {
            return StepResult.Failed(label, "Impossible de rafraîchir les adaptateurs actifs");
        }

        return failures > 0
            ? StepResult.Ok(label, $"Rafraîchi avec avertissements sur {failures} adaptateur(s)")
            : StepResult.Ok(label, "Adaptateurs rafraîchis");
    }

    private static IEnumerable<NetworkInterface> EnumerateTargetAdapters()
    {
        return NetworkInterface
            .GetAllNetworkInterfaces()
            .Where(a => a.NetworkInterfaceType != NetworkInterfaceType.Loopback && a.NetworkInterfaceType != NetworkInterfaceType.Tunnel);
    }

    private static StepStatus ComputeGlobalStatus(IReadOnlyCollection<StepResult> steps)
    {
        var failed = steps.Any(s => s.Status == StepStatus.Failed);
        if (failed)
        {
            return StepStatus.Failed;
        }

        var warnings = steps.Any(s => s.Status == StepStatus.Ignored);
        return warnings ? StepStatus.Warning : StepStatus.Ok;
    }

    private static string BuildSummary(StepStatus globalStatus, IReadOnlyCollection<StepResult> steps, bool isAdmin)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Reset réseau (soft): Résultat global: {globalStatus}");
        sb.AppendLine($"Droits admin: {(isAdmin ? "Oui" : "Non")}");
        foreach (var step in steps)
        {
            sb.AppendLine($"- {step.Label}: {step.Status} ({step.Details})");
        }

        sb.Append("Prochaines options: Diagnostic réseau | Reset réseau (complet)");
        return sb.ToString();
    }

    private async Task<LatencyProbeResult> ProbeAsync(string label, string target, CancellationToken ct)
    {
        var rtts = new List<long>();
        var failures = 0;
        var dnsFailures = 0;

        for (var i = 0; i < PingCount; i++)
        {
            ct.ThrowIfCancellationRequested();

            var attempt = await _ping.SendAsync(target, PingTimeoutMs, ct).ConfigureAwait(false);
            switch (attempt.Status)
            {
                case PingAttemptStatus.Success:
                    rtts.Add(attempt.RoundtripTimeMs);
                    break;
                case PingAttemptStatus.DnsError:
                    dnsFailures++;
                    failures++;
                    break;
                default:
                    failures++;
                    break;
            }
        }

        var packetLossPercent = (double)failures / PingCount * 100;
        var hasSuccess = rtts.Count > 0;
        var min = hasSuccess ? rtts.Min() : (long?)null;
        var max = hasSuccess ? rtts.Max() : (long?)null;
        var avg = hasSuccess ? rtts.Average() : (double?)null;
        var jitter = CalculateJitter(rtts);

        var status = DetermineStatus(hasSuccess, dnsFailures, packetLossPercent, avg, jitter);
        return new LatencyProbeResult(label, target, status, min, avg, max, packetLossPercent, jitter);
    }

    private static LatencyStatus DetermineStatus(bool hasSuccess, int dnsFailures, double packetLossPercent, double? avg, double? jitter)
    {
        if (dnsFailures > 0 && !hasSuccess)
        {
            return LatencyStatus.DnsFailure;
        }

        if (!hasSuccess)
        {
            return LatencyStatus.Failure;
        }

        var warning = false;
        if (avg.HasValue && avg.Value > LatencyWarningThresholdMs)
        {
            warning = true;
        }

        if (packetLossPercent >= PacketLossWarningThresholdPercent)
        {
            warning = true;
        }

        if (jitter.HasValue && jitter.Value > JitterWarningThresholdMs)
        {
            warning = true;
        }

        return warning ? LatencyStatus.Warning : LatencyStatus.Ok;
    }

    private static double? CalculateJitter(IReadOnlyList<long> rtts)
    {
        if (rtts.Count < 2)
        {
            return null;
        }

        var deltas = new List<double>();
        for (var i = 1; i < rtts.Count; i++)
        {
            deltas.Add(Math.Abs(rtts[i] - rtts[i - 1]));
        }

        // Jitter = moyenne des variations absolues entre RTT consécutifs (définition projet par défaut)
        return deltas.Average();
    }

    private string BuildLatencySummary(LatencyProbeResult gateway, LatencyProbeResult external)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Passerelle locale: {FormatProbe(gateway)}");
        sb.AppendLine($"Serveur externe stable: {FormatProbe(external)}");

        var global = ComputeGlobalStatus(gateway, external);
        sb.AppendLine($"Résumé global: {global}");
        sb.Append("Ton réseau respire… parfois.");

        return sb.ToString();
    }

    private static string ComputeGlobalStatus(LatencyProbeResult gateway, LatencyProbeResult external)
    {
        if (gateway.Status is LatencyStatus.DnsFailure or LatencyStatus.MissingGateway || external.Status == LatencyStatus.DnsFailure)
        {
            return "Échec";
        }

        if (gateway.Status == LatencyStatus.Failure || external.Status == LatencyStatus.Failure)
        {
            return "Échec";
        }

        if (gateway.Status == LatencyStatus.Warning || external.Status == LatencyStatus.Warning)
        {
            return "Attention";
        }

        return "OK";
    }

    private static string FormatProbe(LatencyProbeResult probe)
    {
        return probe.Status switch
        {
            LatencyStatus.MissingGateway => "Échec (passerelle non détectée)",
            LatencyStatus.DnsFailure => "Échec (DNS/resolve)",
            LatencyStatus.Failure => "Échec (aucune réponse)",
            _ => FormatMetrics(probe)
        };
    }

    private static string FormatMetrics(LatencyProbeResult probe)
    {
        var jitter = probe.JitterMs.HasValue ? $"{probe.JitterMs.Value:0.0} ms" : "N/A";
        var min = probe.MinMs?.ToString() ?? "-";
        var avg = probe.AverageMs.HasValue ? probe.AverageMs.Value.ToString("0.0") : "-";
        var max = probe.MaxMs?.ToString() ?? "-";
        var loss = probe.PacketLossPercent.ToString("0.0");

        var prefix = probe.Status == LatencyStatus.Warning ? "Attention" : "OK";
        return $"{prefix} (min/avg/max {min}/{avg}/{max} ms, perte {loss} %, jitter {jitter})";
    }

    private enum RebootAdvice
    {
        None,
        Recommended,
        Required
    }
}

public sealed record StepResult(string Label, StepStatus Status, string Details)
{
    public static StepResult Ok(string label, string details) => new(label, StepStatus.Ok, details);

    public static StepResult Failed(string label, string details) => new(label, StepStatus.Failed, details);

    public static StepResult Ignored(string label, string details) => new(label, StepStatus.Ignored, details);
}

public enum StepStatus
{
    Ok,
    Failed,
    Ignored,
    Warning
}

public sealed record LatencyProbeResult(
    string Label,
    string Target,
    LatencyStatus Status,
    long? MinMs,
    double? AverageMs,
    long? MaxMs,
    double PacketLossPercent,
    double? JitterMs)
{
    public static LatencyProbeResult MissingGateway()
        => new("Passerelle locale", string.Empty, LatencyStatus.MissingGateway, null, null, null, 100, null);
}

public enum LatencyStatus
{
    Ok,
    Warning,
    Failure,
    DnsFailure,
    MissingGateway
}
