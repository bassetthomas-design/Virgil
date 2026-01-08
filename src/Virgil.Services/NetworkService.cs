using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Virgil.Core.Models;
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

    public async Task<ActionExecutionResult> RunQuickDiagnosticAsync(CancellationToken ct = default)
    {
        if (!_platformInfo.IsWindows())
        {
            return ActionExecutionResult.NotAvailable("Diagnostic réseau", "Diagnostic réseau non disponible: service NetworkService absent.");
        }

        if (_runner is null || _networkInfoProvider is null || _pingClient is null)
        {
            return ActionExecutionResult.NotAvailable("Diagnostic réseau", "Diagnostic réseau non disponible: service NetworkService absent.");
        }

        var activeInterfaces = GetActiveNetworkInterfaces();
        var steps = new List<ActionStepResult>();
        var summaryLines = new List<string>();

        if (activeInterfaces.Count == 0)
        {
            steps.Add(new ActionStepResult(ActionResultStatus.Skipped, "Interfaces actives", "Aucune interface active détectée."));
            summaryLines.Add("Interfaces actives: aucune.");
        }
        else
        {
            steps.Add(new ActionStepResult(ActionResultStatus.Success, "Interfaces actives", $"{activeInterfaces.Count} interface(s) active(s)."));
            summaryLines.Add($"Interfaces actives: {FormatInterfaces(activeInterfaces)}.");
        }

        var gateway = _networkInfoProvider.GetDefaultGateway();
        if (string.IsNullOrWhiteSpace(gateway))
        {
            steps.Add(new ActionStepResult(ActionResultStatus.Skipped, "Passerelle par défaut", "Passerelle non détectée."));
            summaryLines.Add("Passerelle: non détectée.");
        }
        else
        {
            steps.Add(new ActionStepResult(ActionResultStatus.Success, "Passerelle par défaut", gateway));
            summaryLines.Add($"Passerelle: {gateway}.");
        }

        var dnsServers = GetDnsServers(activeInterfaces);
        if (dnsServers.Count == 0)
        {
            steps.Add(new ActionStepResult(ActionResultStatus.Skipped, "DNS", "Aucun serveur DNS détecté."));
            summaryLines.Add("DNS: non détectés.");
        }
        else
        {
            steps.Add(new ActionStepResult(ActionResultStatus.Success, "DNS", string.Join(", ", dnsServers)));
            summaryLines.Add($"DNS: {string.Join(", ", dnsServers)}.");
        }

        if (string.IsNullOrWhiteSpace(gateway))
        {
            steps.Add(new ActionStepResult(ActionResultStatus.Skipped, "Ping passerelle", "Passerelle indisponible."));
            summaryLines.Add("Ping passerelle: non applicable.");
        }
        else
        {
            var pingStep = await BuildGatewayPingStepAsync(gateway, ct).ConfigureAwait(false);
            steps.Add(pingStep);
            summaryLines.Add($"Ping passerelle: {pingStep.Summary}.");
        }

        return BuildDiagnosticResult(steps, summaryLines);
    }

    public async Task<ActionExecutionResult> SoftResetAsync(CancellationToken ct = default)
    {
        if (!_platformInfo.IsWindows())
        {
            return ActionExecutionResult.NotAvailable("Reset réseau (soft)", "Uniquement supporté sur Windows");
        }

        var isAdmin = _privilegeChecker.IsAdministrator();
        var activeInterfaces = GetActiveNetworkInterfaces();
        var noActiveInterfaceReason = activeInterfaces.Count == 0 ? "Non applicable: pas de connexion active." : null;

        var steps = new List<NetworkStep>
        {
            NetworkStep.AdminOptional("Flush DNS", "ipconfig", "/flushdns", "Purge le cache DNS."),
            NetworkStep.AdminOptional("Renew IP", "ipconfig", "/renew", "Renouvelle la configuration DHCP.", noActiveInterfaceReason),
            NetworkStep.AdminRequired("Reset Winsock léger", "netsh", "winsock reset", "Réinitialise Winsock sans purge complète."),
            NetworkStep.AdminRequired("Reset pile TCP/IP (soft)", "netsh", "int ip reset", "Réinitialise la pile TCP/IP."),
            NetworkStep.AdminRequired("Réinitialiser adaptateurs (soft)", "powershell", BuildPowerShellCommand("Get-NetAdapter | Where-Object { $_.Status -eq 'Up' } | Restart-NetAdapter -Confirm:$false"), "Redémarre les adaptateurs réseau actifs.", noActiveInterfaceReason),
        };

        steps.AddRange(BuildDnsResetSteps(activeInterfaces));

        var results = await ExecuteStepsAsync(steps, isAdmin, ct).ConfigureAwait(false);
        var globalStatus = DeriveGlobalStatus(results);
        var summary = BuildSoftSummary(globalStatus, isAdmin);
        var recommendations = BuildSoftRecommendations(isAdmin);

        return globalStatus switch
        {
            ActionResultStatus.Success => ActionExecutionResult.Ok("Reset réseau (soft)", summary, results, recommendations),
            ActionResultStatus.PartialSuccess => ActionExecutionResult.Partial("Reset réseau (soft)", summary, results, recommendations),
            ActionResultStatus.Failed => ActionExecutionResult.Failure("Reset réseau (soft)", summary, results, recommendations),
            _ => ActionExecutionResult.Skipped("Reset réseau (soft)", summary, results)
        };
    }

    public async Task<ActionExecutionResult> AdvancedResetAsync(CancellationToken ct = default)
    {
        if (!_platformInfo.IsWindows())
        {
            return ActionExecutionResult.NotAvailable("Reset réseau (complet)", "Uniquement supporté sur Windows");
        }

        if (!_privilegeChecker.IsAdministrator())
        {
            return ActionExecutionResult.Failure("Reset réseau (complet)", "Refusé : droits administrateur requis");
        }

        var steps = new List<NetworkStep>
        {
            NetworkStep.AdminRequired("Reset complet Winsock", "netsh", "winsock reset", "Réinitialise la configuration Winsock."),
            NetworkStep.AdminRequired("Reset pile TCP/IP", "netsh", "int ip reset", "Réinitialise la pile TCP/IP."),
            NetworkStep.AdminRequired("Réinitialisation adaptateurs réseau", "powershell", BuildPowerShellCommand("Get-NetAdapter | Where-Object { $_.Status -eq 'Up' } | Restart-NetAdapter -Confirm:$false"), "Redémarre les interfaces actives."),
            NetworkStep.AdminRequired("Suppression profils Wi-Fi", "netsh", "wlan delete profile name=*", "Purge les profils Wi-Fi enregistrés."),
            NetworkStep.AdminRequired("Suppression réseaux Ethernet mémorisés", "netsh", "lan delete profile name=* interface=*", "Supprime les profils LAN mémorisés."),
            NetworkStep.AdminRequired("Redémarrage services réseau", "powershell", BuildPowerShellCommand("'Dnscache','Dhcp','NlaSvc' | ForEach-Object { Stop-Service $_ -ErrorAction SilentlyContinue; Start-Service $_ -ErrorAction SilentlyContinue }"), "Relance les services réseau principaux."),
        };

        steps.AddRange(BuildDnsResetSteps(GetActiveNetworkInterfaces()));

        var results = await ExecuteStepsAsync(steps, isAdmin: true, ct).ConfigureAwait(false);
        var rebootRequired = results.Any(r => ContainsRestartHint(r.Summary));
        var globalStatus = DeriveGlobalStatus(results);
        var summary = BuildAdvancedSummary(globalStatus, rebootRequired);
        var recommendations = BuildAdvancedRecommendations(rebootRequired);

        return globalStatus switch
        {
            ActionResultStatus.Success => ActionExecutionResult.Ok("Reset réseau (complet)", summary, results, recommendations),
            ActionResultStatus.PartialSuccess => ActionExecutionResult.Partial("Reset réseau (complet)", summary, results, recommendations),
            ActionResultStatus.Failed => ActionExecutionResult.Failure("Reset réseau (complet)", summary, results, recommendations),
            _ => ActionExecutionResult.Skipped("Reset réseau (complet)", summary, results)
        };
    }

    public async Task<ActionExecutionResult> RunLatencyTestAsync(CancellationToken ct = default)
    {
        if (!_platformInfo.IsWindows())
        {
            return ActionExecutionResult.NotAvailable("Test de latence", "Uniquement supporté sur Windows");
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

        return ActionExecutionResult.Ok("Test de latence terminé", sb.ToString().TrimEnd());
    }

    public async Task<ActionExecutionResult> RunInternetSpeedTestAsync(CancellationToken ct = default)
    {
        var connectivity = await _pingClient.SendAsync(ExternalLatencyHost, PingTimeoutMs, ct).ConfigureAwait(false);
        if (connectivity.Status != PingAttemptStatus.Success)
        {
            return ActionExecutionResult.Failure("Test de débit Internet", "Connexion indisponible");
        }

        var probeResult = await _speedProbe.MeasureAsync(ct).ConfigureAwait(false);
        if (!probeResult.Success)
        {
            var reason = string.IsNullOrWhiteSpace(probeResult.FailureReason)
                ? "Test de débit indisponible"
                : probeResult.FailureReason;
            return ActionExecutionResult.Failure("Test de débit Internet", reason);
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

        return ActionExecutionResult.Ok("Test de débit Internet terminé", string.Join(Environment.NewLine, lines));
    }

    private async Task<IReadOnlyList<ActionStepResult>> ExecuteStepsAsync(IEnumerable<NetworkStep> steps, bool isAdmin, CancellationToken ct)
    {
        var results = new List<ActionStepResult>();

        foreach (var step in steps)
        {
            if (ct.IsCancellationRequested)
            {
                results.Add(new ActionStepResult(ActionResultStatus.Skipped, step.Label, "Annulé"));
                continue;
            }

            if (!string.IsNullOrWhiteSpace(step.SkipReason))
            {
                results.Add(new ActionStepResult(ActionResultStatus.Skipped, step.Label, step.SkipReason));
                continue;
            }

            if (step.AdminRequirement != AdminRequirement.Optional && !isAdmin)
            {
                var status = step.AdminRequirement == AdminRequirement.RequiredFail
                    ? ActionResultStatus.Failed
                    : ActionResultStatus.Skipped;
                results.Add(new ActionStepResult(status, step.Label, "Droits admin requis"));
                continue;
            }

            var result = await _runner.RunAsync(step.FileName, step.Arguments, DefaultTimeout, ct).ConfigureAwait(false);
            var message = !string.IsNullOrWhiteSpace(result.Output)
                ? result.Output
                : !string.IsNullOrWhiteSpace(result.Error)
                    ? result.Error!
                    : step.Description;

            results.Add(new ActionStepResult(result.Success ? ActionResultStatus.Success : ActionResultStatus.Failed, step.Label, TrimMessage(message)));
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

    private static ActionResultStatus DeriveGlobalStatus(IEnumerable<ActionStepResult> results)
    {
        var list = results.ToList();
        if (list.Count == 0)
        {
            return ActionResultStatus.Failed;
        }

        var succeeded = list.Count(r => r.Status == ActionResultStatus.Success);
        var failed = list.Count(r => r.Status == ActionResultStatus.Failed);

        if (failed == list.Count)
        {
            return ActionResultStatus.Failed;
        }

        if (succeeded > 0 && failed > 0)
        {
            return ActionResultStatus.PartialSuccess;
        }

        if (succeeded > 0)
        {
            return ActionResultStatus.Success;
        }

        return ActionResultStatus.Failed;
    }

    private static string BuildSoftSummary(ActionResultStatus globalStatus, bool isAdmin)
    {
        var statusLabel = globalStatus switch
        {
            ActionResultStatus.PartialSuccess => "Partiel",
            ActionResultStatus.Failed => "Échec",
            _ => "OK"
        };

        var note = isAdmin ? string.Empty : "Certaines actions nécessitent les droits administrateur.";
        var summary = $"Résultat global: {statusLabel}.";
        return string.IsNullOrWhiteSpace(note) ? summary : $"{summary} {note}";
    }

    private static string BuildAdvancedSummary(ActionResultStatus globalStatus, bool rebootRequired)
    {
        var statusLabel = globalStatus switch
        {
            ActionResultStatus.PartialSuccess => "Partiel",
            ActionResultStatus.Failed => "Échec",
            _ => "OK"
        };

        var reboot = rebootRequired ? "requis" : "recommandé";
        return $"Résultat global: {statusLabel}. Redémarrage: {reboot}.";
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

    private static IReadOnlyList<string> BuildSoftRecommendations(bool isAdmin)
    {
        var list = new List<string>();
        if (!isAdmin)
        {
            list.Add("Certaines actions nécessitent les droits administrateur.");
        }

        list.Add("Prochaines options: Diagnostic réseau | Reset réseau (complet)");
        return list;
    }

    private static IReadOnlyList<string> BuildAdvancedRecommendations(bool rebootRequired)
    {
        var list = new List<string>
        {
            "À prévoir: reconfiguration Wi-Fi / VPN (profils et réseaux supprimés, VPN non désinstallé).",
            "Prochaines options: Diagnostic réseau"
        };
        return list;
    }

    private IReadOnlyList<NetworkInterface> GetActiveNetworkInterfaces()
    {
        return NetworkInterface
            .GetAllNetworkInterfaces()
            .Where(nic => nic.OperationalStatus == OperationalStatus.Up)
            .Where(nic => nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .Where(nic => nic.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
            .ToList();
    }

    private static IReadOnlyList<string> GetDnsServers(IEnumerable<NetworkInterface> interfaces)
    {
        return interfaces
            .SelectMany(nic => nic.GetIPProperties().DnsAddresses)
            .Select(address => address.ToString())
            .Where(address => !string.IsNullOrWhiteSpace(address))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string FormatInterfaces(IEnumerable<NetworkInterface> interfaces)
    {
        var summaries = interfaces.Select(nic =>
        {
            var ips = nic.GetIPProperties()
                .UnicastAddresses
                .Select(addr => addr.Address.ToString())
                .Where(address => !string.IsNullOrWhiteSpace(address))
                .ToList();
            var ipLabel = ips.Count == 0 ? "IP inconnue" : string.Join(", ", ips);
            return $"{nic.Name} ({ipLabel})";
        });

        return string.Join("; ", summaries);
    }

    private async Task<ActionStepResult> BuildGatewayPingStepAsync(string gateway, CancellationToken ct)
    {
        var ping = await _pingClient.SendAsync(gateway, PingTimeoutMs, ct).ConfigureAwait(false);
        return ping.Status switch
        {
            PingAttemptStatus.Success => new ActionStepResult(ActionResultStatus.Success, "Ping passerelle", $"OK ({ping.RoundtripTimeMs} ms)"),
            PingAttemptStatus.Timeout => new ActionStepResult(ActionResultStatus.Skipped, "Ping passerelle", "Indisponible (timeout)"),
            PingAttemptStatus.DnsError => new ActionStepResult(ActionResultStatus.Skipped, "Ping passerelle", "Indisponible (erreur DNS)"),
            _ => new ActionStepResult(ActionResultStatus.Skipped, "Ping passerelle", "Indisponible")
        };
    }

    private static ActionExecutionResult BuildDiagnosticResult(IReadOnlyList<ActionStepResult> steps, IReadOnlyList<string> summaryLines)
    {
        var hasSuccess = steps.Any(step => step.Status == ActionResultStatus.Success);
        var summary = summaryLines.Count == 0 ? "Aucune donnée réseau disponible." : string.Join(Environment.NewLine, summaryLines);

        if (!hasSuccess)
        {
            return ActionExecutionResult.NotAvailable("Diagnostic réseau", summary, steps);
        }

        return ActionExecutionResult.Ok("Diagnostic réseau", summary, steps);
    }

    private static IReadOnlyList<NetworkStep> BuildDnsResetSteps(IReadOnlyList<NetworkInterface> interfaces)
    {
        if (interfaces.Count == 0)
        {
            return new[] { NetworkStep.Skipped("Réinitialiser DNS custom", "Aucune interface cible.") };
        }

        var steps = new List<NetworkStep>();
        foreach (var networkInterface in interfaces)
        {
            var name = networkInterface.Name.Replace("\"", string.Empty);
            steps.Add(NetworkStep.AdminRequiredFail(
                $"Réinitialiser DNS custom (IPv4 - {name})",
                "netsh",
                $"interface ipv4 set dnsservers name=\"{name}\" source=dhcp",
                "Réinitialise les DNS IPv4 sur DHCP."));
            steps.Add(NetworkStep.AdminRequiredFail(
                $"Réinitialiser DNS custom (IPv6 - {name})",
                "netsh",
                $"interface ipv6 set dnsservers name=\"{name}\" source=dhcp",
                "Réinitialise les DNS IPv6 sur DHCP."));
        }

        return steps;
    }

    private static string BuildPowerShellCommand(string command)
        => $"-NoProfile -ExecutionPolicy Bypass -Command \"[Console]::OutputEncoding=[System.Text.Encoding]::UTF8; {command}\"";

    private static string TrimMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "Voir détails";
        }

        var normalized = message.Replace(Environment.NewLine, " ").Trim();
        return normalized.Length > 220 ? normalized[..220] + "…" : normalized;
    }

    private sealed record NetworkStep(string Label, string FileName, string Arguments, string Description, AdminRequirement AdminRequirement, string? SkipReason)
    {
        public static NetworkStep AdminRequired(string label, string fileName, string arguments, string description, string? skipReason = null)
            => new(label, fileName, arguments, description, AdminRequirement.RequiredSkip, skipReason);

        public static NetworkStep AdminRequiredFail(string label, string fileName, string arguments, string description, string? skipReason = null)
            => new(label, fileName, arguments, description, AdminRequirement.RequiredFail, skipReason);

        public static NetworkStep Skipped(string label, string reason)
            => new(label, string.Empty, string.Empty, reason, AdminRequirement.Optional, reason);

        public static NetworkStep AdminOptional(string label, string fileName, string arguments, string description, string? skipReason = null)
            => new(label, fileName, arguments, description, AdminRequirement.Optional, skipReason);
    }

    private enum AdminRequirement
    {
        Optional,
        RequiredSkip,
        RequiredFail
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

    private static bool ContainsRestartHint(string? output)
        => !string.IsNullOrWhiteSpace(output)
           && (output.IndexOf("restart", StringComparison.OrdinalIgnoreCase) >= 0
               || output.IndexOf("reboot", StringComparison.OrdinalIgnoreCase) >= 0);
}
