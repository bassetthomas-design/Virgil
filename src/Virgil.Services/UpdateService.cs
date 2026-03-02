using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Virgil.Core.Models;
using Virgil.Services.Abstractions;
using Virgil.Services.Network;

namespace Virgil.Services;

/// <summary>
/// Implémentation basique qui délègue aux services Windows/winget déjà présents
/// dans Virgil.Core. Les actions indisponibles retournent un résultat explicite.
/// </summary>
public sealed class UpdateService : IUpdateService
{
    private readonly Core.Services.ApplicationUpdateService _apps;
    private readonly Core.Services.WindowsUpdateService _windows;
    private readonly Core.Services.DriverUpdateService _drivers;
    private readonly IAutomaticUpdateDataSource _automaticUpdates;
    private readonly Func<IProgress<double>?>? _progressProvider;
    private List<DriverUpdateItem> _lastDriverItems = new();

    public UpdateService(IAutomaticUpdateDataSource? automaticUpdates = null, Func<IProgress<double>?>? progressProvider = null)
    {
        _windows = new Core.Services.WindowsUpdateService();
        _apps = new Core.Services.ApplicationUpdateService();
        _drivers = new Core.Services.DriverUpdateService(progressProvider?.Invoke());
        _automaticUpdates = automaticUpdates ?? new RuntimeAutomaticUpdateDataSource(_windows, new WindowsPrivilegeChecker(), new RuntimePlatformInfo());
        _progressProvider = progressProvider;
    }

    public async Task<ActionExecutionResult> ManageAutomaticUpdatesAsync(AutoUpdateUserIntent? intent = null, CancellationToken ct = default)
    {
        intent ??= new AutoUpdateUserIntent();

        AutomaticUpdateSnapshot snapshot;
        try
        {
            snapshot = await _automaticUpdates.CaptureAsync(intent, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return ActionExecutionResult.Failure("Gestion des mises à jour automatiques", $"Erreur gestion mises à jour auto: {ex.Message}");
        }

        var message = BuildAutomaticUpdateMessage(snapshot);
        if (!snapshot.Supported)
        {
            return ActionExecutionResult.NotAvailable("Gestion des mises à jour automatiques", message);
        }

        var success = snapshot.ChangeApplied || !intent.Toggle.HasValue || !snapshot.AdminRequiredForChanges || snapshot.HasAdministrativeAccess;
        return success
            ? ActionExecutionResult.Ok("Gestion des mises à jour automatiques", message)
            : ActionExecutionResult.Partial("Gestion des mises à jour automatiques", message);
    }

    public async Task<ActionExecutionResult> UpdateAppsAsync(CancellationToken ct = default)
    {
        try
        {
            var log = await _apps.UpgradeAllAsync(includeUnknown: true, silent: true).ConfigureAwait(false);
            return ActionExecutionResult.Ok("Mise à jour des applications terminée", log);
        }
        catch (Exception ex)
        {
            return ActionExecutionResult.Failure("Mise à jour des applications", $"Erreur mise à jour applications: {ex.Message}");
        }
    }

    public async Task<ActionExecutionResult> RunWindowsUpdateAsync(CancellationToken ct = default)
    {
        try
        {
            var progress = _progressProvider?.Invoke();
            var result = await _windows.RunAsync(new WindowsUpdateOptions(), progress, ct).ConfigureAwait(false);
            var summary = BuildWindowsUpdateSummary(result);
            var status = BuildWindowsUpdateStatus(result);
            var debugInfo = BuildWindowsUpdateDiagnostics(result);

            return new ActionExecutionResult(status, "Windows Update", summary, debugInfo: debugInfo);
        }
        catch (Exception ex)
        {
            return ActionExecutionResult.Failure("Windows Update", $"Erreur Windows Update: {ex.Message}");
        }
    }

    public async Task<ActionExecutionResult> ScanDriversAsync(CancellationToken ct = default)
    {
        try
        {
            var result = await _drivers.ScanAsync(ct).ConfigureAwait(false);
            _lastDriverItems = result.Succeeded ? result.Items ?? new List<DriverUpdateItem>() : new List<DriverUpdateItem>();
            var summary = BuildDriverScanSummary(result);
            return new ActionExecutionResult(
                result.Succeeded ? ActionResultStatus.Success : ActionResultStatus.Failed,
                "Vérification des pilotes",
                summary,
                debugInfo: BuildDriverDebugInfo(result));
        }
        catch (Exception ex)
        {
            return ActionExecutionResult.Failure("Vérification des pilotes", $"Erreur pilotes: {ex.Message}");
        }
    }

    public async Task<ActionExecutionResult> InstallDriversAsync(CancellationToken ct = default)
    {
        try
        {
            var result = await _drivers.InstallAsync(_lastDriverItems, ct).ConfigureAwait(false);
            var summary = BuildDriverInstallSummary(result);
            return new ActionExecutionResult(
                result.Succeeded ? ActionResultStatus.Success : ActionResultStatus.Failed,
                "Installation des pilotes",
                summary,
                debugInfo: BuildDriverDebugInfo(result));
        }
        catch (Exception ex)
        {
            return ActionExecutionResult.Failure("Installation des pilotes", $"Erreur pilotes: {ex.Message}");
        }
    }

    private static string BuildDriverDebugInfo(DriverUpdateResult result)
        => $"drivers_found={result.Found};drivers_installed={result.Installed};reboot_required={result.RebootRequired.ToString().ToLowerInvariant()}";

    private static string BuildDriverScanSummary(DriverUpdateResult result)
    {
        if (!result.Succeeded)
        {
            var reason = string.IsNullOrWhiteSpace(result.FailureReason) ? "Erreur Windows Update" : result.FailureReason;
            return $"Pilotes: échec ({reason}).";
        }

        return result.Found == 0
            ? "Aucun pilote disponible."
            : $"{result.Found} pilote(s) trouvé(s). Installation en cours.";
    }

    private static string BuildDriverInstallSummary(DriverUpdateResult result)
    {
        if (!result.Succeeded)
        {
            var reason = string.IsNullOrWhiteSpace(result.FailureReason) ? "Erreur Windows Update" : result.FailureReason;
            return $"Pilotes: échec ({reason}).";
        }

        return result.RebootRequired
            ? $"Pilotes: {result.Installed} installé(s). Redémarrage requis."
            : $"Pilotes: {result.Installed} installé(s).";
    }

    private static string BuildAutomaticUpdateMessage(AutomaticUpdateSnapshot snapshot)
    {
        var sb = new StringBuilder();

        sb.AppendLine(snapshot.AutomaticUpdatesEnabled
            ? "Mises à jour automatiques: activées (le robot bosse pendant que tu dors)."
            : "Mises à jour automatiques: désactivées, mode manuel assumé.");

        if (snapshot.ChangeApplied)
        {
            sb.AppendLine("Demande prise en compte (sans charcuter les fichiers système).");
        }
        else if (snapshot.AdminRequiredForChanges && !snapshot.HasAdministrativeAccess)
        {
            sb.AppendLine("Pas de droits admin : la bascule reste inchangée.");
        }

        if (!string.IsNullOrWhiteSpace(snapshot.StatusDetails))
        {
            sb.AppendLine(snapshot.StatusDetails.Trim());
        }

        var scanInfo = string.IsNullOrWhiteSpace(snapshot.ScanDetails)
            ? "Scan des mises à jour: pas d'information exploitable."
            : $"Scan des mises à jour: {snapshot.ScanDetails.Trim()}";

        sb.AppendLine(scanInfo);

        if (snapshot.AvailableUpdates.Count == 0)
        {
            sb.AppendLine("Mises à jour disponibles: aucune. Ton système bronze en paix.");
        }
        else
        {
            sb.AppendLine("Mises à jour disponibles:");
            foreach (var update in snapshot.AvailableUpdates)
            {
                sb.AppendLine($"- {update}");
            }

            sb.AppendLine("Proposition: Lancer les mises à jour (je ne touche à rien sans toi).");
        }

        if (snapshot.ConflictDetected)
        {
            sb.AppendLine("Conflit détecté : une politique semble s'opposer aux mises à jour automatiques.");
        }

        if (!string.IsNullOrWhiteSpace(snapshot.Recommendation))
        {
            sb.AppendLine($"Recommandation: {snapshot.Recommendation.Trim()}");
        }

        sb.Append("Les mises à jour sont prêtes à faire leurs valises. Fais-les entrer ou laisse-les mariner.");

        return sb.ToString().Trim();
    }

    internal static string BuildAutoUpdateScanDetails(WindowsUpdateResult result)
    {
        if (!result.Succeeded)
        {
            return string.IsNullOrWhiteSpace(result.Summary) ? "Recherche indisponible" : result.Summary.Trim();
        }

        if (result.UpdatesFound == 0)
        {
            return "Aucune mise à jour disponible.";
        }

        return $"{result.UpdatesFound} mise(s) à jour détectée(s).";
    }

    private static ActionResultStatus BuildWindowsUpdateStatus(WindowsUpdateResult result)
    {
        if (!result.Succeeded)
        {
            return ActionResultStatus.Failed;
        }

        if (result.UpdatesFound > 0 && result.UpdatesInstalled < result.UpdatesFound)
        {
            return ActionResultStatus.PartialSuccess;
        }

        return ActionResultStatus.Success;
    }

    private static string BuildWindowsUpdateSummary(WindowsUpdateResult result)
    {
        if (!result.Succeeded)
        {
            var reason = BuildWindowsUpdateFailureReason(result);
            return $"Windows Update: échec ({reason}).";
        }

        if (result.UpdatesFound == 0)
        {
            return "Windows Update: rien à installer.";
        }

        var reboot = result.RebootRequired ? "Oui" : "Non";
        return $"Windows Update: {result.UpdatesFound} trouvées, {result.UpdatesInstalled} installées. Redémarrage requis: {reboot}.";
    }

    private static string BuildWindowsUpdateFailureReason(WindowsUpdateResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.Summary))
        {
            return result.Summary.Trim().TrimEnd('.');
        }

        return "erreur Windows Update";
    }

    private static string? BuildWindowsUpdateDiagnostics(WindowsUpdateResult result)
        => string.IsNullOrWhiteSpace(result.FailureReason) ? null : result.FailureReason;

}

public sealed class RuntimeAutomaticUpdateDataSource : IAutomaticUpdateDataSource
{
    private readonly Core.Services.WindowsUpdateService _windows;
    private readonly IPrivilegeChecker _privilegeChecker;
    private readonly IPlatformInfo _platformInfo;

    public RuntimeAutomaticUpdateDataSource(Core.Services.WindowsUpdateService windows, IPrivilegeChecker privilegeChecker, IPlatformInfo platformInfo)
    {
        _windows = windows ?? throw new ArgumentNullException(nameof(windows));
        _privilegeChecker = privilegeChecker ?? throw new ArgumentNullException(nameof(privilegeChecker));
        _platformInfo = platformInfo ?? throw new ArgumentNullException(nameof(platformInfo));
    }

    public async Task<AutomaticUpdateSnapshot> CaptureAsync(AutoUpdateUserIntent intent, CancellationToken ct = default)
    {
        if (!_platformInfo.IsWindows())
        {
            var reason = "Gestion des mises à jour automatiques supportée uniquement sur Windows.";
            return AutomaticUpdateSnapshot.Unsupported(reason);
        }

        var hasAdmin = _privilegeChecker.IsAdministrator();
        var autoEnabled = true;
        var statusDetails = "Politique par défaut: vérification et téléchargement automatiques (lecture best-effort).";

        var updates = new List<string>();
        var scanDetails = string.Empty;

        try
        {
            var scanResult = await _windows.RunAsync(new WindowsUpdateOptions { SearchOnly = true }, null, ct).ConfigureAwait(false);
            scanDetails = UpdateService.BuildAutoUpdateScanDetails(scanResult);
            if (scanResult.UpdatesFound > 0)
            {
                updates.Add("Windows Update signale des correctifs en attente (voir journal détaillé).");
            }
        }
        catch (Exception ex)
        {
            scanDetails = $"Recherche indisponible ({ex.Message})";
        }

        if (intent.Toggle.HasValue)
        {
            if (!hasAdmin)
            {
                statusDetails += " Demande de bascule ignorée (droits admin requis).";
            }
            else
            {
                autoEnabled = intent.Toggle.Value == AutoUpdateToggle.Enable;
                statusDetails += autoEnabled
                    ? " Mode automatique demandé (simulation, aucune clé système modifiée)."
                    : " Mode manuel demandé (simulation, aucune clé système modifiée).";
            }
        }

        var recommendation = updates.Count > 0
            ? "Planifie un créneau et lance les mises à jour."
            : "Rien à installer : profite de ce calme plat.";

        var conflict = scanDetails.IndexOf("policy", StringComparison.OrdinalIgnoreCase) >= 0
            || scanDetails.IndexOf("refus", StringComparison.OrdinalIgnoreCase) >= 0;

        var changeApplied = intent.Toggle.HasValue && hasAdmin;

        return new AutomaticUpdateSnapshot(
            Supported: true,
            AutomaticUpdatesEnabled: autoEnabled,
            AdminRequiredForChanges: true,
            HasAdministrativeAccess: hasAdmin,
            ChangeApplied: changeApplied,
            AvailableUpdates: updates,
            StatusDetails: statusDetails,
            ScanDetails: scanDetails,
            Recommendation: recommendation,
            ConflictDetected: conflict);
    }
}
