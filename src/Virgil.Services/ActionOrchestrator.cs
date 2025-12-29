using System;
using System.Threading;
using System.Threading.Tasks;
using Virgil.Domain.Actions;
using Virgil.Services.Abstractions;

namespace Virgil.Services;

public sealed class ActionOrchestrator : IActionOrchestrator
{
    private readonly ICleanupService _cleanup;
    private readonly IUpdateService _update;
    private readonly INetworkService _network;
    private readonly IPerformanceService _performance;
    private readonly IDiagnosticService _diagnostic;
    private readonly ISpecialService _special;
    private readonly IChatService _chat;

    public ActionOrchestrator(
        ICleanupService cleanup,
        IUpdateService update,
        INetworkService network,
        IPerformanceService performance,
        IDiagnosticService diagnostic,
        ISpecialService special,
        IChatService chat)
    {
        _cleanup = cleanup;
        _update = update;
        _network = network;
        _performance = performance;
        _diagnostic = diagnostic;
        _special = special;
        _chat = chat;
    }

    public async Task<ActionExecutionResult> RunAsync(VirgilActionId actionId, CancellationToken ct = default)
    {
        switch (actionId)
        {
            // Maintenance rapide
            case VirgilActionId.ScanSystemExpress:
                return await ExecuteAsync("Scan système express", () => _diagnostic.RunExpressAsync(ct), ct);

            case VirgilActionId.QuickClean:
                return await ExecuteAsync("Nettoyage rapide", () => _cleanup.RunSimpleAsync(ct), ct);

            case VirgilActionId.LightBrowserClean:
                return await ExecuteAsync("Nettoyage léger des navigateurs", () => _cleanup.RunBrowserLightAsync(ct), ct);

            case VirgilActionId.SoftRamFlush:
                return await ExecuteAsync("Libération douce de la RAM", () => _performance.SoftRamFlushAsync(ct), ct);

            // Maintenance avancée
            case VirgilActionId.AdvancedDiskClean:
                return await ExecuteAsync("Nettoyage disque avancé", () => _cleanup.RunAdvancedAsync(ct), ct);

            case VirgilActionId.DiskCheck:
                return await ExecuteAsync("Vérification du disque", () => _diagnostic.DiskCheckAsync(ct), ct);

            case VirgilActionId.SystemIntegrityCheck:
                return await ExecuteAsync("Vérification de l’intégrité système", () => _diagnostic.SystemIntegrityCheckAsync(ct), ct);

            case VirgilActionId.DeepBrowserClean:
                return await ExecuteAsync("Nettoyage profond des navigateurs", () => _cleanup.RunBrowserDeepAsync(ct), ct);

            // Réseau & Internet
            case VirgilActionId.NetworkQuickDiag:
                return await ExecuteAsync("Diagnostic réseau rapide", () => _network.RunQuickDiagnosticAsync(ct), ct);

            case VirgilActionId.NetworkSoftReset:
                return await ExecuteAsync("Réinitialisation réseau (soft)", () => _network.SoftResetAsync(ct), ct);

            case VirgilActionId.NetworkAdvancedReset:
                return await ExecuteAsync("Réinitialisation réseau avancée", () => _network.AdvancedResetAsync(ct), ct);

            case VirgilActionId.LatencyStabilityTest:
                return await ExecuteAsync("Test de latence et de stabilité", () => _network.RunLatencyTestAsync(ct), ct);

            // Gaming / Performance
            case VirgilActionId.EnableGamingMode:
                return await ExecuteAsync("Activation du mode Performance / Gaming", () => _performance.EnableGamingModeAsync(ct), ct);

            case VirgilActionId.RestoreNormalMode:
                return await ExecuteAsync("Retour au mode normal", () => _performance.RestoreNormalModeAsync(ct), ct);

            case VirgilActionId.StartupAnalysis:
                return await ExecuteAsync("Analyse du démarrage", () => _performance.AnalyzeStartupAsync(ct), ct);

            case VirgilActionId.CloseGamingSession:
                return await ExecuteAsync("Fermeture de la session gaming", () => _performance.CloseGamingSessionAsync(ct), ct);

            // Mises à jour
            case VirgilActionId.UpdateSoftwares:
                return await ExecuteAsync("Mise à jour des logiciels", () => _update.UpdateAppsAsync(ct), ct);

            case VirgilActionId.RunWindowsUpdate:
                return await ExecuteAsync("Lancement de Windows Update", () => _update.RunWindowsUpdateAsync(ct), ct);

            case VirgilActionId.CheckGpuDrivers:
                return await ExecuteAsync("Vérification des pilotes GPU", () => _update.CheckGpuDriversAsync(ct), ct);

            // Spéciaux
            case VirgilActionId.RamboMode:
                return await ExecuteAsync("Mode RAMBO", () => _special.RamboModeAsync(ct), ct);

            case VirgilActionId.ThanosChatWipe:
                await _chat.InfoAsync("Effet Thanos sur le chat.", ct);
                await _chat.ThanosWipeAsync(preservePinned: true, ct);
                return ActionExecutionResult.Ok("Chat effacé");

            case VirgilActionId.ReloadConfiguration:
                return await ExecuteAsync("Rechargement de la configuration", () => _special.ReloadConfigurationAsync(ct), ct);

            case VirgilActionId.RescanSystem:
                return await ExecuteAsync("Re-scan global du système", () => _diagnostic.RescanSystemAsync(ct), ct);

            default:
                var message = $"Action non gérée: {actionId}.";
                await _chat.ErrorAsync(message, ct);
                return ActionExecutionResult.Failure(message);
        }
    }

    private async Task<ActionExecutionResult> ExecuteAsync(string label, Func<Task<ActionExecutionResult>> action, CancellationToken ct)
    {
        await _chat.InfoAsync($"Exécution : {label}…", ct);
        try
        {
            var result = await action().ConfigureAwait(false);
            if (result.Success)
            {
                var msg = string.IsNullOrWhiteSpace(result.Message) ? label : result.Message;
                await _chat.InfoAsync($"Terminé : {msg}", ct);
            }
            else
            {
                await _chat.WarnAsync($"Échec/indispo : {result.Message}", ct);
            }

            if (result.TryGetDetails(out var details))
            {
                await _chat.InfoAsync(details, ct);
            }

            return result;
        }
        catch (Exception ex)
        {
            var failure = ActionExecutionResult.Failure($"Erreur pendant {label}: {ex.Message}");
            await _chat.ErrorAsync(failure.Message, ct);
            return failure;
        }
    }
}
