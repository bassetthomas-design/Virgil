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
    private readonly IAutomaticUpdateDataSource _automaticUpdates;

    public UpdateService(IAutomaticUpdateDataSource? automaticUpdates = null)
    {
        _windows = new Core.Services.WindowsUpdateService();
        _apps = new Core.Services.ApplicationUpdateService();
        _automaticUpdates = automaticUpdates ?? new RuntimeAutomaticUpdateDataSource(_windows, new WindowsPrivilegeChecker(), new RuntimePlatformInfo());
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
            var scanResult = await _windows.StartScanAsync().ConfigureAwait(false);
            var downloadResult = await _windows.StartDownloadAsync().ConfigureAwait(false);
            var installResult = await _windows.StartInstallAsync().ConfigureAwait(false);

            var steps = new List<ActionStepResult>();
            var outcomes = new List<WindowsUpdateStepOutcome>();

            outcomes.Add(BuildWindowsUpdateOutcome("Scan Windows Update", scanResult, steps));
            outcomes.Add(BuildWindowsUpdateOutcome("Téléchargement Windows Update", downloadResult, steps));
            outcomes.Add(BuildWindowsUpdateOutcome("Installation Windows Update", installResult, steps));

            var globalStatus = DeriveGlobalStatus(outcomes);
            var summary = BuildWindowsUpdateSummary(globalStatus, outcomes);

            return globalStatus switch
            {
                ActionResultStatus.Success => ActionExecutionResult.Ok("Windows Update", summary, steps),
                ActionResultStatus.PartialSuccess => ActionExecutionResult.Partial("Windows Update", summary, steps),
                ActionResultStatus.Failed => ActionExecutionResult.Failure("Windows Update", summary, steps),
                _ => ActionExecutionResult.Failure("Windows Update", summary, steps)
            };
        }
        catch (Exception ex)
        {
            return ActionExecutionResult.Failure("Windows Update", $"Erreur Windows Update: {ex.Message}");
        }
    }

    public Task<ActionExecutionResult> CheckGpuDriversAsync(CancellationToken ct = default)
        => Task.FromResult(ActionExecutionResult.NotAvailable("Vérification des pilotes GPU", "Vérification des drivers GPU non disponible (service absent)."));

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

    private sealed record WindowsUpdateStepOutcome(ActionResultStatus Status, string Summary, bool RebootRequired);

    private sealed record WindowsUpdateExitCodeInfo(ActionResultStatus Status, string Message, bool RebootRequired);

    private static readonly Dictionary<int, WindowsUpdateExitCodeInfo> WindowsUpdateExitCodes = new()
    {
        [0] = new WindowsUpdateExitCodeInfo(ActionResultStatus.Success, "Système à jour.", false),
        [3010] = new WindowsUpdateExitCodeInfo(ActionResultStatus.Success, "Mise à jour appliquée, redémarrage requis.", true),
        [1641] = new WindowsUpdateExitCodeInfo(ActionResultStatus.Success, "Redémarrage en cours après mise à jour.", true),
        [2359302] = new WindowsUpdateExitCodeInfo(ActionResultStatus.Success, "Mise à jour déjà installée.", false),
        [1602] = new WindowsUpdateExitCodeInfo(ActionResultStatus.PartialSuccess, "Opération annulée par l’utilisateur.", false),
        [1618] = new WindowsUpdateExitCodeInfo(ActionResultStatus.PartialSuccess, "Une autre installation est déjà en cours.", false),
        [5] = new WindowsUpdateExitCodeInfo(ActionResultStatus.Failed, "Accès refusé (droits administrateur requis).", false),
        [87] = new WindowsUpdateExitCodeInfo(ActionResultStatus.Failed, "Paramètre invalide.", false),
        [1603] = new WindowsUpdateExitCodeInfo(ActionResultStatus.Failed, "Erreur fatale pendant l’installation.", false)
    };

    private static WindowsUpdateStepOutcome BuildWindowsUpdateOutcome(
        string stepLabel,
        Core.Services.WindowsUpdateCommandResult commandResult,
        ICollection<ActionStepResult> steps)
    {
        var outcome = EvaluateWindowsUpdateResult(commandResult);
        var summary = outcome.Summary;
        var details = commandResult.Output?.Trim();
        if (!string.IsNullOrWhiteSpace(details))
        {
            summary = $"{summary} {details}";
        }

        steps.Add(new ActionStepResult(outcome.Status, stepLabel, summary));
        return outcome;
    }

    private static WindowsUpdateStepOutcome EvaluateWindowsUpdateResult(Core.Services.WindowsUpdateCommandResult commandResult)
    {
        if (commandResult.ExitCode is null)
        {
            return new WindowsUpdateStepOutcome(
                ActionResultStatus.Failed,
                "Impossible de lancer Windows Update (droits administrateur requis ou service indisponible).",
                false);
        }

        if (WindowsUpdateExitCodes.TryGetValue(commandResult.ExitCode.Value, out var info))
        {
            return new WindowsUpdateStepOutcome(info.Status, info.Message, info.RebootRequired);
        }

        return new WindowsUpdateStepOutcome(
            ActionResultStatus.Failed,
            "Résultat inattendu de Windows Update.",
            false);
    }

    private static ActionResultStatus DeriveGlobalStatus(IEnumerable<WindowsUpdateStepOutcome> outcomes)
    {
        var hasFailed = false;
        var hasPartial = false;

        foreach (var outcome in outcomes)
        {
            if (outcome.Status == ActionResultStatus.Failed)
            {
                hasFailed = true;
            }
            else if (outcome.Status == ActionResultStatus.PartialSuccess)
            {
                hasPartial = true;
            }
        }

        if (hasFailed)
        {
            return ActionResultStatus.Failed;
        }

        if (hasPartial)
        {
            return ActionResultStatus.PartialSuccess;
        }

        return ActionResultStatus.Success;
    }

    private static string BuildWindowsUpdateSummary(ActionResultStatus globalStatus, IEnumerable<WindowsUpdateStepOutcome> outcomes)
    {
        if (globalStatus == ActionResultStatus.Success)
        {
            var rebootRequired = false;
            foreach (var outcome in outcomes)
            {
                rebootRequired |= outcome.RebootRequired;
            }

            return rebootRequired
                ? "Mise à jour appliquée, redémarrage requis."
                : "Système à jour.";
        }

        return globalStatus == ActionResultStatus.PartialSuccess
            ? "Windows Update terminé partiellement."
            : "Windows Update a échoué.";
    }

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
            scanDetails = (await _windows.StartScanAsync().ConfigureAwait(false)).GetDisplayMessage();
            if (scanDetails.IndexOf("update", StringComparison.OrdinalIgnoreCase) >= 0
                || scanDetails.IndexOf("kb", StringComparison.OrdinalIgnoreCase) >= 0)
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
