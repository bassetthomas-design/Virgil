using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virgil.Services;

public enum NetworkResetStatus
{
    Pending,
    Success,
    Failed
}

public sealed record NetworkResetStep(string Label, string Command, string Description);

public sealed record NetworkResetStepResult(string Label, NetworkResetStatus Status, string Message)
{
    public string StatusLabel => Status.ToString();
}

public sealed record NetworkResetResult(NetworkResetStatus Status, IReadOnlyList<NetworkResetStepResult> Steps, string Summary);

/// <summary>
/// Provides helpers to reset common Windows network components (Winsock, TCP/IP, DNS cache).
/// The implementation executes the commands sequentially and reports a per-step status.
/// </summary>
public sealed class NetworkService
{
    private readonly IReadOnlyList<NetworkResetStep> _resetSteps = new List<NetworkResetStep>
    {
        new("Reset complet Winsock", "netsh winsock reset", "Réinitialise la configuration Winsock."),
        new("Reset pile TCP/IP", "netsh int ip reset", "Réinitialise la pile TCP/IP."),
        new("Vider cache DNS", "ipconfig /flushdns", "Purge le cache DNS."),
        new("Renouveler DHCP", "ipconfig /renew", "Renouvelle la configuration DHCP."),
        new("Relâcher DHCP", "ipconfig /release", "Libère l'adresse DHCP actuelle."),
        new("Réinitialiser pare-feu", "netsh advfirewall reset", "Restaure la configuration du pare-feu."),
        new("Réinitialiser configuration proxy", "netsh winhttp reset proxy", "Supprime la configuration de proxy WinHTTP."),
        new("Réinitialiser statistiques interface", "netstat -rn", "Affiche la table de routage pour vérification."),
        new("Réinitialiser adaptateurs", "ipconfig /registerdns", "Force l'enregistrement DNS des adaptateurs."),
        new("Réinitialiser paramètres Winsock supplémentaires", "netsh winsock reset catalog", "Réinitialise le catalogue Winsock."),
    };

    public async Task<NetworkResetResult> ResetNetworkAsync()
    {
        var stepResults = new List<NetworkResetStepResult>();
        foreach (var step in _resetSteps)
        {
            var result = await ExecuteStepAsync(step);
            stepResults.Add(result);
        }

        var globalStatus = stepResults.Any(r => r.Status == NetworkResetStatus.Failed)
            ? NetworkResetStatus.Failed
            : NetworkResetStatus.Success;

        var summaryBuilder = new StringBuilder();
        summaryBuilder.AppendLine($"Reset réseau (complet): Résultat global: {globalStatus}.");
        foreach (var step in stepResults)
        {
            summaryBuilder.AppendLine($"- {step.Label}: {step.StatusLabel} — {step.Message}");
        }

        return new NetworkResetResult(globalStatus, stepResults, summaryBuilder.ToString().TrimEnd());
    }

    private async Task<NetworkResetStepResult> ExecuteStepAsync(NetworkResetStep step)
    {
        try
        {
            var (success, output) = await RunCommandAsync(step.Command);
            var status = success ? NetworkResetStatus.Success : NetworkResetStatus.Failed;
            var message = string.IsNullOrWhiteSpace(output)
                ? step.Description
                : output.Trim();

            return new NetworkResetStepResult(step.Label, status, message);
        }
        catch (Exception ex)
        {
            return new NetworkResetStepResult(step.Label, NetworkResetStatus.Failed, ex.Message);
        }
    }

    private static async Task<(bool Success, string Output)> RunCommandAsync(string command)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/C {command}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        var outputBuilder = new StringBuilder();

        var tcs = new TaskCompletionSource<int>();
        process.EnableRaisingEvents = true;
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null) outputBuilder.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null) outputBuilder.AppendLine(e.Data);
        };
        process.Exited += (_, _) => tcs.TrySetResult(process.ExitCode);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var exitCode = await tcs.Task.ConfigureAwait(false);
        var output = outputBuilder.ToString();

        return (exitCode == 0, output);
    }
}
