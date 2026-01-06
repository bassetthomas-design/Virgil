using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Virgil.Services.Abstractions;
using Virgil.Services.Network;

namespace Virgil.Services;

public sealed class NetworkService : INetworkService
{
    private const string ExternalLatencyHost = "1.1.1.1";
    private const int LatencySampleCount = 10;
    private const int PingTimeoutMs = 2000;
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(20);

    private readonly INetworkCommandRunner _runner;
    private readonly IPrivilegeChecker _privilegeChecker;
    private readonly IPlatformInfo _platformInfo;
    private readonly IPingClient _pingClient;
    private readonly INetworkInfoProvider _networkInfoProvider;
    private readonly IInternetSpeedProbe _speedProbe;

    public NetworkService()
        : this(
            new NetworkCommandRunner(),
            new WindowsPrivilegeChecker(),
            new RuntimePlatformInfo(),
            new RuntimePingClient(),
            new RuntimeNetworkInfoProvider(),
            null)
    {
    }

    public NetworkService(
        INetworkCommandRunner runner,
        IPrivilegeChecker privilegeChecker,
        IPlatformInfo platformInfo,
        IPingClient pingClient,
        INetworkInfoProvider networkInfoProvider,
        IInternetSpeedProbe? speedProbe = null)
    {
        _runner = runner;
        _privilegeChecker = privilegeChecker;
        _platformInfo = platformInfo;
        _pingClient = pingClient;
        _networkInfoProvider = networkInfoProvider;
        _speedProbe = speedProbe ?? new HttpInternetSpeedProbe(_pingClient);
    }

    public Task<ActionExecutionResult> RunQuickDiagnosticAsync(CancellationToken ct = default)
        => Task.FromResult(ActionExecutionResult.NotAvailable("Diagnostic réseau rapide non implémenté"));

    public async Task<ActionExecutionResult> SoftResetAsync(CancellationToken ct = default)
    {
        if (!_platformInfo.IsWindows())
        {
            return ActionExecutionResult.NotAvailable("Reset réseau (soft) uniquement supporté sur Windows");
        }

        var isAdmin = _privilegeChecker.IsAdministrator();
        var steps = new List<NetworkStep>
        {
            NetworkStep.AdminOptional("Flush DNS", "ipconfig", "/flushdns", "Purge le cache DNS."),
            NetworkStep.AdminOptional("Renew IP", "ipconfig", "/renew", "Renouvelle la configuration DHCP."),
            NetworkStep.AdminRequired("Reset Winsock léger", "netsh", "winsock reset catalog", "Réinitialise Winsock sans purge complète."),
            NetworkStep.AdminRequired("Réinitialiser DNS custom", "netsh", "interface ip set dns name=\"*\" source=dhcp", "Rebasculer les interfaces en DNS automatique."),
            NetworkStep.AdminRequired("Réinitialiser adaptateurs (soft)", "powershell", "-Command \"Get-NetAdapter | Where-Object { $_.Status -eq 'Up' } | Restart-NetAdapter -Confirm:$false\"", "Redémarre les adaptateurs réseau actifs."),
        };

        var results = await ExecuteStepsAsync(steps, isAdmin, ct).ConfigureAwait(false);
        var globalStatus = DeriveGlobalStatus(results);
        var message = BuildSoftSummary(globalStatus, results, isAdmin);

        var success = globalStatus != "Échec";
        return new ActionExecutionResult(success, message);
    }

    public async Task<ActionExecutionResult> AdvancedResetAsync(CancellationToken ct = default)
    {
        if (!_platformInfo.IsWindows())
        {
            return ActionExecutionResult.NotAvailable("Reset réseau (complet) uniquement supporté sur Windows");
        }

        if (!_privilegeChecker.IsAdministrator())
        {
            return ActionExecutionResult.Failure("Reset réseau (complet) refusé : droits administrateur requis");
        }

        var steps = new List<NetworkStep>
        {
            NetworkStep.AdminRequired("Reset complet Winsock", "netsh", "winsock reset", "Réinitialise la configuration Winsock."),
            NetworkStep.AdminRequired("Reset pile TCP/IP", "netsh", "int ip reset", "Réinitialise la pile TCP/IP."),
            NetworkStep.AdminRequired("Réinitialisation adaptateurs réseau", "powershell", "-Command \"Get-NetAdapter | Where-Object { $_.Status -eq 'Up' } | Restart-NetAdapter -Confirm:$false\"", "Redémarre les interfaces actives."),
            NetworkStep.AdminRequired("Suppression configs IP custom", "netsh", "interface ip set dns name=\"*\" source=dhcp", "Supprime les DNS statiques / IP custom."),
            NetworkStep.AdminRequired("Suppression profils Wi-Fi", "netsh", "wlan delete profile name=*", "Purge les profils Wi-Fi enregistrés."),
            NetworkStep.AdminRequired("Suppression réseaux Ethernet mémorisés", "netsh", "lan delete profile name=* interface=*", "Supprime les profils LAN mémorisés."),
            NetworkStep.AdminRequired("Redémarrage services réseau", "powershell", "-Command \"'Dnscache','Dhcp','NlaSvc' | ForEach-Object { Stop-Service $_ -ErrorAction SilentlyContinue; Start-Service $_ -ErrorAction SilentlyContinue }\"", "Relance les services réseau principaux."),
        };

        var results = await ExecuteStepsAsync(steps, isAdmin: true, ct).ConfigureAwait(false);
        var rebootRequired = results.Any(r => r.OutputContainsRestartHint);
        var globalStatus = DeriveGlobalStatus(results);
        var message = BuildAdvancedSummary(globalStatus, results, rebootRequired);

        var success = globalStatus != "Échec";
        return new ActionExecutionResult(success, message);
    }

    public async Task<ActionExecutionResult> RunLatencyTestAsync(CancellationToken ct = default)
    {
        if (!_platformInfo.IsWindows())
        {
            return ActionExecutionResult.NotAvailable("Test de latence uniquement supporté sur Windows");
        }

        var gateway = _networkInfoProvider.GetDefaultGateway();
        var gatewayResult = await EvaluateEndpointMetricsAsync(gateway, "Passerelle locale", ct).ConfigureAwait(false);
        var externalResult = await EvaluateEndpointMetricsAsync(ExternalLatencyHost, "Serveur externe stable", ct).ConfigureAwait(false);

        var sb = new StringBuilder();
        sb.AppendLine(FormatEndpointResult(gatewayResult));
        sb.AppendLine(FormatEndpointResult(externalResult));

        var global = DeriveGlobalSummary(gatewayResult.Status, externalResult.Status);
        sb.AppendLine($"Résumé global: {global}.");
        sb.Append("Ton réseau respire… parfois.");

        return ActionExecutionResult.Ok(sb.ToString().TrimEnd());
    }

    public async Task<ActionExecutionResult> RunInternetSpeedTestAsync(CancellationToken ct = default)
    {
        var connectivity = await _pingClient.SendAsync(ExternalLatencyHost, PingTimeoutMs, ct).ConfigureAwait(false);
        if (connectivity.Status != PingAttemptStatus.Success)
        {
            return ActionExecutionResult.Failure("Connexion indisponible");
        }

        var probeResult = await _speedProbe.MeasureAsync(ct).ConfigureAwait(false);
        if (!probeResult.Success)
        {
            var reason = string.IsNullOrWhiteSpace(probeResult.FailureReason)
                ? "Test de débit indisponible"
                : probeResult.FailureReason;
            return ActionExecutionResult.Failure(reason);
        }

        var appreciation = DeriveInternetAppreciation(probeResult.DownloadMbps, probeResult.UploadMbps, probeResult.LatencyMs, probeResult.StabilityVariationPercent);
        var usage = SuggestUsage(appreciation, probeResult.StabilityVariationPercent);
        var stabilityNote = probeResult.StabilityVariationPercent.HasValue
            ? $" (Stabilité: variation {probeResult.StabilityVariationPercent.Value:F1}%{(probeResult.StabilityVariationPercent.Value < 8 ? ", plutôt stable" : string.Empty)})"
            : string.Empty;

        var lines = new List<string>
        {
            $"Débit descendant: {probeResult.DownloadMbps:F1} Mbps",
            $"Débit montant: {probeResult.UploadMbps:F1} Mbps",
            $"Latence mesurée: {probeResult.LatencyMs:F0} ms",
            $"Appréciation globale: {appreciation}",
            $"Usage conseillé: {usage}{stabilityNote}",
        };

        if (probeResult.UsedFallback)
        {
            lines.Add($"Serveur de test: {probeResult.ServerLabel}.");
        }

        lines.Add("Ce n’est pas de la fibre supersonique, mais ça fait le travail.");

        return ActionExecutionResult.Ok(string.Join(Environment.NewLine, lines));
    }

    private async Task<IReadOnlyList<NetworkStepResult>> ExecuteStepsAsync(IEnumerable<NetworkStep> steps, bool isAdmin, CancellationToken ct)
    {
        var results = new List<NetworkStepResult>();

        foreach (var step in steps)
        {
            if (ct.IsCancellationRequested)
            {
                results.Add(new NetworkStepResult(step.Label, "Ignoré", "Annulé", false));
                continue;
            }

            if (step.AdminOnly && !isAdmin)
            {
                results.Add(new NetworkStepResult(step.Label, "Ignoré", "Droits admin requis", false));
                continue;
            }

            var result = await _runner.RunAsync(step.FileName, step.Arguments, DefaultTimeout, ct).ConfigureAwait(false);
            var status = result.Success ? "OK" : "Échec";
            var message = !string.IsNullOrWhiteSpace(result.Output)
                ? result.Output
                : !string.IsNullOrWhiteSpace(result.Error)
                    ? result.Error!
                    : step.Description;

            var restartHint = (result.Output ?? string.Empty).IndexOf("restart", StringComparison.OrdinalIgnoreCase) >= 0
                || (result.Output ?? string.Empty).IndexOf("reboot", StringComparison.OrdinalIgnoreCase) >= 0;

            results.Add(new NetworkStepResult(step.Label, status, message, restartHint));
        }

        return results;
    }

    private async Task<EndpointLatencyResult> EvaluateEndpointMetricsAsync(string? host, string label, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return EndpointLatencyResult.Failure(label, "Échec (passerelle non détectée)");
        }

        var samples = new List<long>(LatencySampleCount);
        var failures = 0;
        var dnsFailures = 0;

        for (var i = 0; i < LatencySampleCount; i++)
        {
            var attempt = await _pingClient.SendAsync(host, PingTimeoutMs, ct).ConfigureAwait(false);
            if (attempt.Status == PingAttemptStatus.Success)
            {
                samples.Add(attempt.RoundtripTimeMs);
            }
            else
            {
                failures++;
                if (attempt.Status == PingAttemptStatus.DnsError)
                {
                    dnsFailures++;
                }
            }
        }

        if (samples.Count == 0)
        {
            var reason = dnsFailures > 0 ? "DNS/resolve" : failures > 0 ? "Timeout" : "Inconnu";
            return EndpointLatencyResult.Failure(label, $"Échec ({reason})");
        }

        var min = (int)Math.Round((double)samples.Min());
        var max = (int)Math.Round((double)samples.Max());
        var avg = (int)Math.Round(samples.Average());
        var loss = Math.Round((double)failures / (failures + samples.Count) * 100, 1);

        // Fallback jitter definition: mean absolute difference between consecutive RTTs.
        var diffs = samples.Zip(samples.Skip(1), (a, b) => Math.Abs(a - b)).ToList();
        var jitter = diffs.Count > 0 ? (int)Math.Round(diffs.Average()) : 0;

        var status = DeriveEndpointStatus(avg, jitter, loss);
        var metrics = new LatencyMetrics(min, avg, max, jitter, loss);
        var message = $"{label}: {StatusToText(status)} — min/avg/max: {min}/{avg}/{max} ms, perte: {loss}%, jitter: {jitter} ms";

        return new EndpointLatencyResult(label, status, message, metrics, dnsFailures > 0);
    }

    private static EndpointStatus DeriveEndpointStatus(int avg, int jitter, double loss)
    {
        // Conservative thresholds documented here: warn when loss > 0%, avg > 200ms or jitter > 50ms.
        if (loss >= 50)
        {
            return EndpointStatus.Failure;
        }

        if (loss > 0 || avg > 200 || jitter > 50)
        {
            return EndpointStatus.Attention;
        }

        return EndpointStatus.Ok;
    }

    private static string DeriveGlobalSummary(EndpointStatus gateway, EndpointStatus external)
    {
        if (gateway == EndpointStatus.Failure || external == EndpointStatus.Failure)
        {
            return "Échec";
        }

        if (gateway == EndpointStatus.Attention || external == EndpointStatus.Attention)
        {
            return "Attention";
        }

        return "OK";
    }

    private static string FormatEndpointResult(EndpointLatencyResult result)
    {
        if (result.Metrics is null)
        {
            return $"{result.Label}: {result.Message}";
        }

        return result.Message;
    }

    private static string StatusToText(EndpointStatus status) => status switch
    {
        EndpointStatus.Ok => "OK",
        EndpointStatus.Attention => "Attention",
        _ => "Échec"
    };

    private static string DeriveGlobalStatus(IEnumerable<NetworkStepResult> results)
    {
        if (results.Any(r => r.Status == "Échec"))
        {
            return "Échec";
        }

        if (results.Any(r => r.Status == "Ignoré"))
        {
            return "Attention";
        }

        return "OK";
    }

    private static string BuildSoftSummary(string globalStatus, IReadOnlyList<NetworkStepResult> steps, bool isAdmin)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Reset réseau (soft): Résultat global: {globalStatus}.");

        foreach (var step in steps)
        {
            sb.AppendLine($"- {step.Label}: {step.Status} — {FormatMessage(step.Message)}");
        }

        if (!isAdmin)
        {
            sb.AppendLine("Note: certaines actions nécessitent les droits administrateur.");
        }

        sb.Append("Prochaines options: Diagnostic réseau | Reset réseau (complet)");
        return sb.ToString().TrimEnd();
    }

    private static string BuildAdvancedSummary(string globalStatus, IReadOnlyList<NetworkStepResult> steps, bool rebootRequired)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Reset réseau (complet): Résultat global: {globalStatus}.");

        foreach (var step in steps)
        {
            sb.AppendLine($"- {step.Label}: {step.Status} — {FormatMessage(step.Message)}");
        }

        sb.AppendLine($"Redémarrage: {(rebootRequired ? "requis" : "recommandé")}");
        sb.AppendLine("À prévoir: reconfiguration Wi-Fi / VPN (profils et réseaux supprimés, VPN non désinstallé).");
        sb.Append("Prochaines options: Diagnostic réseau");
        return sb.ToString().TrimEnd();
    }

    private static string DeriveInternetAppreciation(double downloadMbps, double uploadMbps, double latencyMs, double? stability)
    {
        if (downloadMbps >= 80 && uploadMbps >= 20 && latencyMs <= 30)
        {
            return "Bon";
        }

        if (downloadMbps >= 25 && uploadMbps >= 5 && latencyMs <= 90)
        {
            return "Moyen";
        }

        if (stability.HasValue && stability.Value < 6)
        {
            return "Faible mais stable";
        }

        return "Faible";
    }

    private static string SuggestUsage(string appreciation, double? stability)
        => appreciation switch
        {
            "Bon" => "jeu en ligne et streaming 4K sans sourciller",
            "Moyen" => "streaming HD, visio et télétravail tranquille",
            "Faible mais stable" => "navigation, musique en ligne, streaming SD stable",
            _ => stability.HasValue && stability.Value < 12
                ? "navigation basique et streaming léger"
                : "navigation basique et mails (le reste sera au ralenti)",
        };

    private static string FormatMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "Voir détails";
        }

        var normalized = message.Replace(Environment.NewLine, " ").Trim();
        return normalized.Length > 220 ? normalized[..220] + "…" : normalized;
    }

    private sealed record NetworkStep(string Label, string FileName, string Arguments, string Description, bool AdminOnly)
    {
        public static NetworkStep AdminRequired(string label, string fileName, string arguments, string description)
            => new(label, fileName, arguments, description, true);

        public static NetworkStep AdminOptional(string label, string fileName, string arguments, string description)
            => new(label, fileName, arguments, description, false);
    }

    private sealed record LatencyMetrics(int Min, int Avg, int Max, int Jitter, double LossPercent);

    private sealed record EndpointLatencyResult(string Label, EndpointStatus Status, string Message, LatencyMetrics? Metrics, bool HadDnsFailure)
    {
        public static EndpointLatencyResult Failure(string label, string message)
            => new(label, EndpointStatus.Failure, message, null, false);
    }

    private enum EndpointStatus
    {
        Ok,
        Attention,
        Failure
    }

    private sealed record NetworkStepResult(string Label, string Status, string Message, bool OutputContainsRestartHint);
}
