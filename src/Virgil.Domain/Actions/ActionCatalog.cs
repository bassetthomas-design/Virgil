using System;
using System.Collections.Generic;

namespace Virgil.Domain.Actions;

/// <summary>
/// Central registry of Virgil actions and their backing implementation status.
/// This catalogue is reused by both the UI (buttons/chat) and the backend
/// routing (ChatActionBridge / ActionOrchestrator) to avoid free-form keys.
/// </summary>
public static class ActionCatalog
{
    private static readonly Dictionary<string, ActionDescriptor> _actions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["status"] = new(ActionKey: "status", VirgilActionId.ScanSystemExpress, "Afficher le statut", isDestructive: false, isImplemented: false, Service: "DiagnosticService"),
        // Maintenance rapide
        ["quick_scan"] = new(ActionKey: "quick_scan", VirgilActionId.ScanSystemExpress, "Scan système express", isDestructive: false, isImplemented: false, Service: "DiagnosticService"),
        ["quick_clean"] = new(ActionKey: "quick_clean", VirgilActionId.QuickClean, "Nettoyage rapide", isDestructive: false, isImplemented: true, Service: "CleanupService"),
        ["browser_soft_clean"] = new(ActionKey: "browser_soft_clean", VirgilActionId.LightBrowserClean, "Nettoyage navigateur (léger)", isDestructive: false, isImplemented: false, Service: "CleanupService"),
        ["ram_soft_free"] = new(ActionKey: "ram_soft_free", VirgilActionId.SoftRamFlush, "Libérer la RAM (soft)", isDestructive: false, isImplemented: false, Service: "PerformanceService"),

        // Maintenance avancée
        ["deep_disk_clean"] = new(ActionKey: "deep_disk_clean", VirgilActionId.AdvancedDiskClean, "Nettoyage disque avancé", isDestructive: true, isImplemented: false, Service: "CleanupService"),
        ["browser_deep_clean"] = new(ActionKey: "browser_deep_clean", VirgilActionId.DeepBrowserClean, "Nettoyage navigateur (profond)", isDestructive: false, isImplemented: false, Service: "CleanupService"),

        // Réseau
        ["network_diag"] = new(ActionKey: "network_diag", VirgilActionId.NetworkQuickDiag, "Diagnostic réseau", isDestructive: false, isImplemented: false, Service: "NetworkService"),
        ["network_soft_reset"] = new(ActionKey: "network_soft_reset", VirgilActionId.NetworkSoftReset, "Reset réseau (soft)", isDestructive: false, isImplemented: false, Service: "NetworkService"),
        ["network_hard_reset"] = new(ActionKey: "network_hard_reset", VirgilActionId.NetworkAdvancedReset, "Reset réseau (complet)", isDestructive: true, isImplemented: false, Service: "NetworkService"),
        ["network_latency_test"] = new(ActionKey: "network_latency_test", VirgilActionId.LatencyStabilityTest, "Test de latence", isDestructive: false, isImplemented: false, Service: "NetworkService"),

        // Performance
        ["perf_mode_on"] = new(ActionKey: "perf_mode_on", VirgilActionId.EnableGamingMode, "Activer le mode performance", isDestructive: false, isImplemented: false, Service: "PerformanceService"),
        ["perf_mode_off"] = new(ActionKey: "perf_mode_off", VirgilActionId.RestoreNormalMode, "Désactiver le mode performance", isDestructive: false, isImplemented: false, Service: "PerformanceService"),
        ["startup_analyze"] = new(ActionKey: "startup_analyze", VirgilActionId.StartupAnalysis, "Analyser le démarrage", isDestructive: false, isImplemented: false, Service: "PerformanceService"),
        ["gaming_kill_session"] = new(ActionKey: "gaming_kill_session", VirgilActionId.CloseGamingSession, "Couper les apps de fond", isDestructive: false, isImplemented: false, Service: "PerformanceService"),

        // Mises à jour
        ["apps_update_all"] = new(ActionKey: "apps_update_all", VirgilActionId.UpdateSoftwares, "Mettre à jour les logiciels", isDestructive: false, isImplemented: true, Service: "UpdateService"),
        ["windows_update"] = new(ActionKey: "windows_update", VirgilActionId.RunWindowsUpdate, "Mise à jour Windows", isDestructive: true, isImplemented: true, Service: "WindowsUpdateService"),
        ["gpu_driver_check"] = new(ActionKey: "gpu_driver_check", VirgilActionId.CheckGpuDrivers, "Vérifier les drivers GPU", isDestructive: false, isImplemented: false, Service: "UpdateService"),

        // Spéciaux
        ["rambo_repair"] = new(ActionKey: "rambo_repair", VirgilActionId.RamboMode, "Mode RAMBO", isDestructive: true, isImplemented: false, Service: "SpecialService"),
        ["chat_thanos"] = new(ActionKey: "chat_thanos", VirgilActionId.ThanosChatWipe, "Effet Thanos", isDestructive: true, isImplemented: true, Service: "ChatService"),
        ["app_reload_settings"] = new(ActionKey: "app_reload_settings", VirgilActionId.ReloadConfiguration, "Recharger la configuration", isDestructive: false, isImplemented: false, Service: "SpecialService"),
        ["monitoring_rescan"] = new(ActionKey: "monitoring_rescan", VirgilActionId.RescanSystem, "Re-scanner le système", isDestructive: false, isImplemented: false, Service: "DiagnosticService"),
        ["monitor_rescan"] = new(ActionKey: "monitor_rescan", VirgilActionId.RescanSystem, "Re-scanner le système", isDestructive: false, isImplemented: false, Service: "DiagnosticService"),
        ["clean_browsers"] = new(ActionKey: "clean_browsers", VirgilActionId.LightBrowserClean, "Nettoyage navigateurs", isDestructive: false, isImplemented: false, Service: "CleanupService"),
        ["maintenance_full"] = new(ActionKey: "maintenance_full", VirgilActionId.AdvancedDiskClean, "Maintenance complète", isDestructive: true, isImplemented: false, Service: "CleanupService"),
    };

    public static IReadOnlyDictionary<string, ActionDescriptor> All => _actions;

    public static bool TryGet(string key, out ActionDescriptor descriptor) => _actions.TryGetValue(key, out descriptor!);

    public static string DescribeStatus()
    {
        var lines = new List<string>();
        foreach (var action in _actions.Values)
        {
            var status = action.IsImplemented ? "OK" : "Stub";
            lines.Add($"{action.ActionKey} -> {action.VirgilActionId} ({status}, service: {action.Service})");
        }

        return string.Join(Environment.NewLine, lines);
    }
}

public sealed record ActionDescriptor(
    string ActionKey,
    VirgilActionId VirgilActionId,
    string DisplayName,
    bool IsDestructive,
    bool IsImplemented,
    string Service);
