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
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(20);

    private readonly INetworkCommandRunner _runner;
    private readonly IPrivilegeChecker _privilegeChecker;
    private readonly IPlatformInfo _platformInfo;
    private readonly IPingClient _pingClient;
    private readonly INetworkInfoProvider _networkInfoProvider;

    public NetworkService()
        : this(
            new NetworkCommandRunner(),
            new WindowsPrivilegeChecker(),
            new RuntimePlatformInfo(),
            new RuntimePingClient(),
            new RuntimeNetworkInfoProvider())
    {
    }

    public NetworkService(
        INetworkCommandRunner runner,
        IPrivilegeChecker privilegeChecker,
        IPlatformInfo platformInfo,
        IPingClient pingClient,
        INetworkInfoProvider networkInfoProvider)
    {
        _runner = runner;
        _privilegeChecker = privilegeChecker;
        _platformInfo = platformInfo;
        _pingClient = pingClient;
        _networkInfoProvider = networkInfoProvider;
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

        var sb = new StringBuilder();
        var gateway = _networkInfoProvider.GetDefaultGateway();
        var gatewayStatus = await EvaluateEndpointAsync(gateway, "Passerelle locale", ct).ConfigureAwait(false);
        var externalStatus = await EvaluateEndpointAsync("1.1.1.1", "Serveur externe stable", ct).ConfigureAwait(false);

        sb.AppendLine(gatewayStatus.Line);
        sb.AppendLine(externalStatus.Line);

        var summary = gatewayStatus.Status == "OK" && externalStatus.Status == "OK" ? "OK" : "Échec";
        sb.Append($"Résumé global: {summary}.");

        return ActionExecutionResult.Ok(sb.ToString().TrimEnd());
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

    private async Task<(string Status, string Line)> EvaluateEndpointAsync(string? host, string label, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return ("Échec", $"{label}: Échec (passerelle non détectée)");
        }

        var latencies = new List<long>();
        var failures = 0;
        var dnsFailures = 0;
        for (var i = 0; i < 10; i++)
        {
            var attempt = await _pingClient.SendAsync(host, timeoutMs: 2000, ct).ConfigureAwait(false);
            if (attempt.Status == PingAttemptStatus.Success)
            {
                latencies.Add(attempt.RoundtripTimeMs);
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

        if (latencies.Count == 0)
        {
            var reason = dnsFailures > 0 ? "DNS/resolve" : failures > 0 ? "Timeout" : "Inconnu";
            return ("Échec", $"{label}: Échec ({reason})");
        }

        var avg = (int)Math.Round(latencies.Average());
        var jitter = latencies.Count > 1 ? latencies.Max() - latencies.Min() : 0;
        var status = failures == 0 ? "OK" : "Instable";
        var reasonText = failures == 0 ? $"latence moyenne {avg} ms, jitter {jitter} ms" : "paquets perdus";

        return (status == "OK" ? "OK" : "Échec", $"{label}: {status} ({reasonText})");
    }

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

    private sealed record NetworkStepResult(string Label, string Status, string Message, bool OutputContainsRestartHint);
}
