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
    private readonly INetworkCommandRunner _runner;
    private readonly IPrivilegeChecker _privilegeChecker;
    private readonly IPlatformInfo _platform;

    public NetworkService(
        INetworkCommandRunner? runner = null,
        IPrivilegeChecker? privilegeChecker = null,
        IPlatformInfo? platformInfo = null)
    {
        _runner = runner ?? new NetworkCommandRunner();
        _privilegeChecker = privilegeChecker ?? new WindowsPrivilegeChecker();
        _platform = platformInfo ?? new RuntimePlatformInfo();
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

    public Task<ActionExecutionResult> RunLatencyTestAsync(CancellationToken ct = default)
        => Task.FromResult(ActionExecutionResult.NotAvailable("Test de latence non implémenté"));

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
            return StepResult.Failed(label, "Impossible de relancer les adaptateurs réseau");
        }

        return failures > 0
            ? StepResult.Ok(label, $"Réactivation partielle ({failures} adaptateur(s) en échec)")
            : StepResult.Ok(label, hardReset ? "Adaptateurs réinitialisés" : "Adaptateurs relancés en douceur");
    }

    private async Task<StepResult> RemoveWifiProfilesAsync(CancellationToken ct)
    {
        const string label = "Suppression profils Wi-Fi";
        var result = await _runner.RunAsync("netsh", "wlan delete profile name=* i=*", CommandTimeout, ct).ConfigureAwait(false);

        if (!result.Success)
        {
            var error = string.IsNullOrWhiteSpace(result.Error) ? "Impossible de supprimer les profils Wi-Fi" : result.Error!;
            return StepResult.Failed(label, error);
        }

        return StepResult.Ok(label, "Profils Wi-Fi supprimés (il faudra se reconnecter)");
    }

    private async Task<StepResult> RemoveEthernetProfilesAsync(CancellationToken ct)
    {
        const string label = "Suppression réseaux Ethernet mémorisés";
        var result = await _runner.RunAsync("netsh", "lan delete profile interface=*", CommandTimeout, ct).ConfigureAwait(false);

        if (!result.Success)
        {
            var error = string.IsNullOrWhiteSpace(result.Error) ? "Impossible de purger les profils Ethernet" : result.Error!;
            return StepResult.Failed(label, error);
        }

        return StepResult.Ok(label, "Profils Ethernet effacés (configuration à refaire si besoin)");
    }

    private async Task<StepResult> RestartNetworkServicesAsync(CancellationToken ct)
    {
        const string label = "Redémarrage services réseau";
        var targets = new[] { "Dnscache", "Dhcp", "NlaSvc", "Netman", "WlanSvc", "WwanSvc" };

        var failures = 0;
        foreach (var service in targets)
        {
            var stop = await _runner.RunAsync("net", $"stop {service}", CommandTimeout, ct).ConfigureAwait(false);
            var start = await _runner.RunAsync("net", $"start {service}", CommandTimeout, ct).ConfigureAwait(false);

            if (!stop.Success || !start.Success)
            {
                failures++;
            }
        }

        if (failures == targets.Length)
        {
            return StepResult.Failed(label, "Impossible de relancer les services réseau critiques");
        }

        return failures > 0
            ? StepResult.Ok(label, $"Services relancés avec {failures} échec(s)")
            : StepResult.Ok(label, "Services réseau relancés");
    }

    private static IEnumerable<NetworkInterface> EnumerateTargetAdapters()
        => NetworkInterface.GetAllNetworkInterfaces()
            .Where(nic => nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .Where(nic => nic.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
            .Where(nic => !nic.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase));

    private static StepStatus ComputeGlobalStatus(IEnumerable<StepResult> results)
    {
        if (results.Any(r => r.Status == StepStatus.Failed))
        {
            return StepStatus.Failed;
        }

        return results.Any(r => r.Status == StepStatus.Ignored)
            ? StepStatus.Ignored
            : StepStatus.Ok;
    }

    private static string BuildSummary(StepStatus global, IReadOnlyCollection<StepResult> steps, bool isAdmin)
    {
        var sb = new StringBuilder();
        var globalText = global switch
        {
            StepStatus.Ok => "OK",
            StepStatus.Ignored => "Attention",
            _ => "Échec"
        };

        sb.AppendLine($"Reset réseau (soft): Résultat global: {globalText}. On a secoué la pile réseau, presque trop facile.");
        foreach (var step in steps)
        {
            sb.AppendLine($"- {step.Label}: {step.StatusLabel} — {step.Message}");
        }

        if (!isAdmin && steps.Any(s => s.Status == StepStatus.Ignored))
        {
            sb.AppendLine("Certaines étapes ont été ignorées faute de droits élevés. Pas de panique, rien n'a explosé.");
        }

        sb.Append("Prochaines options: Diagnostic réseau | Reset réseau (complet)");
        return sb.ToString();
    }

    private static string BuildAdvancedSummary(StepStatus global, IReadOnlyCollection<StepResult> steps)
    {
        var sb = new StringBuilder();
        var globalText = global switch
        {
            StepStatus.Ok => "OK",
            StepStatus.Ignored => "Attention",
            _ => "Échec"
        };

        sb.AppendLine($"Reset réseau (complet): Résultat global: {globalText}. Connexion perdue temporairement, mais on remet tout à zéro.");
        foreach (var step in steps)
        {
            sb.AppendLine($"- {step.Label}: {step.StatusLabel} — {step.Message}");
        }

        var rebootAdvice = ComputeRebootAdvice(steps);
        var rebootText = rebootAdvice switch
        {
            RebootAdvice.Required => "requis",
            RebootAdvice.Recommended => "recommandé",
            _ => "non"
        };

        sb.AppendLine($"Redémarrage: {rebootText}.");
        sb.AppendLine("À prévoir: reconfiguration Wi-Fi / VPN (profils supprimés, clients VPN préservés mais configuration réseau rafraîchie).");
        sb.Append("Prochaines options: Diagnostic réseau");
        return sb.ToString();
    }

    private static RebootAdvice ComputeRebootAdvice(IEnumerable<StepResult> steps)
    {
        if (steps.Any(s => s.Reboot == RebootAdvice.Required))
        {
            return RebootAdvice.Required;
        }

        if (steps.Any(s => s.Reboot == RebootAdvice.Recommended))
        {
            return RebootAdvice.Recommended;
        }

        return RebootAdvice.None;
    }

    private static RebootAdvice DetectRebootSignal(NetworkCommandResult result, RebootAdvice fallback)
    {
        if (ContainsRebootCue(result.Output) || ContainsRebootCue(result.Error))
        {
            return RebootAdvice.Required;
        }

        return fallback;
    }

    private static bool ContainsRebootCue(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.Contains("restart", StringComparison.OrdinalIgnoreCase)
            || text.Contains("reboot", StringComparison.OrdinalIgnoreCase)
            || text.Contains("redémarrer", StringComparison.OrdinalIgnoreCase)
            || text.Contains("redemarrer", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record StepResult(string Label, StepStatus Status, string Message, RebootAdvice Reboot = RebootAdvice.None)
    {
        public string StatusLabel => Status switch
        {
            StepStatus.Ok => "OK",
            StepStatus.Ignored => "Ignoré",
            _ => "Échec"
        };

        public static StepResult Ok(string label, string message, RebootAdvice reboot = RebootAdvice.None) => new(label, StepStatus.Ok, message, reboot);
        public static StepResult Ignored(string label, string message) => new(label, StepStatus.Ignored, message);
        public static StepResult Failed(string label, string message) => new(label, StepStatus.Failed, message);
    }

    private enum StepStatus
    {
        Ok,
        Ignored,
        Failed
    }

    private enum RebootAdvice
    {
        None,
        Recommended,
        Required
    }
}
