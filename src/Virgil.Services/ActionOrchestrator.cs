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

    public async Task RunAsync(VirgilActionId actionId, CancellationToken ct = default)
    {
        switch (actionId)
        {
            // Maintenance rapide
            case VirgilActionId.ScanSystemExpress:
                await _chat.InfoAsync("Je lance un scan système express.", ct);
                await _diagnostic.RunExpressAsync(ct);
                break;

            case VirgilActionId.QuickClean:
                await _chat.InfoAsync("Nettoyage rapide en cours.", ct);
                await _cleanup.RunSimpleAsync(ct);
                break;

            case VirgilActionId.LightBrowserClean:
                await _chat.InfoAsync("Nettoyage léger des navigateurs.", ct);
                await _cleanup.RunBrowserLightAsync(ct);
                break;

            case VirgilActionId.SoftRamFlush:
                await _chat.InfoAsync("Libération douce de la RAM.", ct);
                await _performance.CloseGamingSessionAsync(ct); // ou MemoryService plus tard
                break;

            // Maintenance avancée
            case VirgilActionId.AdvancedDiskClean:
                await _chat.InfoAsync("Nettoyage disque avancé.", ct);
                await _cleanup.RunAdvancedAsync(ct);
                break;

            case VirgilActionId.DiskCheck:
                await _chat.InfoAsync("Vérification du disque en cours.", ct);
                await _diagnostic.DiskCheckAsync(ct);
                break;

            case VirgilActionId.SystemIntegrityCheck:
                await _chat.InfoAsync("Vérification de l’intégrité système.", ct);
                await _diagnostic.SystemIntegrityCheckAsync(ct);
                break;

            case VirgilActionId.DeepBrowserClean:
                await _chat.InfoAsync("Nettoyage profond des navigateurs.", ct);
                await _cleanup.RunBrowserDeepAsync(ct);
                break;

            // Réseau & Internet
            case VirgilActionId.NetworkQuickDiag:
                await _chat.InfoAsync("Diagnostic réseau rapide.", ct);
                await _network.RunQuickDiagnosticAsync(ct);
                break;

            case VirgilActionId.NetworkSoftReset:
                await _chat.InfoAsync("Réinitialisation réseau (soft).", ct);
                await _network.SoftResetAsync(ct);
                break;

            case VirgilActionId.NetworkAdvancedReset:
                await _chat.InfoAsync("Réinitialisation réseau avancée.", ct);
                await _network.AdvancedResetAsync(ct);
                break;

            case VirgilActionId.LatencyStabilityTest:
                await _chat.InfoAsync("Test de latence et de stabilité.", ct);
                await _network.RunLatencyTestAsync(ct);
                break;

            // Gaming / Performance
            case VirgilActionId.EnableGamingMode:
                await _chat.InfoAsync("Activation du mode Performance / Gaming.", ct);
                await _performance.EnableGamingModeAsync(ct);
                break;

            case VirgilActionId.RestoreNormalMode:
                await _chat.InfoAsync("Retour au mode normal.", ct);
                await _performance.RestoreNormalModeAsync(ct);
                break;

            case VirgilActionId.StartupAnalysis:
                await _chat.InfoAsync("Analyse du démarrage en cours.", ct);
                await _performance.AnalyzeStartupAsync(ct);
                break;

            case VirgilActionId.CloseGamingSession:
                await _chat.InfoAsync("Fermeture de la session gaming.", ct);
                await _performance.CloseGamingSessionAsync(ct);
                break;

            // Mises à jour
            case VirgilActionId.UpdateSoftwares:
                await _chat.InfoAsync("Mise à jour des logiciels.", ct);
                await _update.UpdateAppsAsync(ct);
                break;

            case VirgilActionId.RunWindowsUpdate:
                await _chat.InfoAsync("Lancement de Windows Update.", ct);
                await _update.RunWindowsUpdateAsync(ct);
                break;

            case VirgilActionId.CheckGpuDrivers:
                await _chat.InfoAsync("Vérification des pilotes GPU.", ct);
                await _update.CheckGpuDriversAsync(ct);
                break;

            // Spéciaux
            case VirgilActionId.RamboMode:
                await _chat.InfoAsync("Mode RAMBO: réparation de l’explorateur et cie.", ct);
                await _special.RamboModeAsync(ct);
                break;

            case VirgilActionId.ThanosChatWipe:
                await _chat.InfoAsync("Effet Thanos sur le chat.", ct);
                await _chat.ThanosWipeAsync(preservePinned: true, ct);
                break;

            case VirgilActionId.ReloadConfiguration:
                await _chat.InfoAsync("Rechargement de la configuration.", ct);
                await _special.ReloadConfigurationAsync(ct);
                break;

            case VirgilActionId.RescanSystem:
                await _chat.InfoAsync("Re-scan global du système.", ct);
                await _diagnostic.RescanSystemAsync(ct);
                break;

            default:
                await _chat.ErrorAsync($"Action non gérée: {actionId}.", ct);
                break;
        }
    }
}
