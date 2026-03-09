using System;
using System.Collections.Generic;
using System.Linq;

namespace Virgil.Services.Assistant;

public sealed record ActionIntent(string IntentName, string ActionId, string DisplayName);

public sealed class ActionIntentParser
{
    private static readonly (string IntentName, string ActionId, string DisplayName, string[] Keywords)[] Patterns =
    {
        ("DefenderQuickScan", "defender_quick_scan", "Scan rapide Defender", new[] { "scan rapide", "defender rapide", "quick scan defender" }),
        ("DefenderFullScan", "defender_full_scan", "Scan complet Defender", new[] { "scan complet", "analyse complète defender", "full scan defender" }),
        ("MalwareScan", "windows_malware_scan", "Analyse malware Windows", new[] { "malware", "mrt", "analyse malware" }),
        ("TempCleanup", "system_temp_clean", "Nettoyage fichiers temporaires", new[] { "nettoyage temp", "fichiers temp", "temp cleanup" }),
        ("BrowserCleanupLight", "browser_soft_clean", "Nettoyage navigateur léger", new[] { "nettoyage navigateur", "browser clean", "cookies" }),
        ("BrowserCleanupDeep", "browser_deep_clean", "Nettoyage navigateur profond", new[] { "nettoyage navigateur profond", "browser deep", "clean navigateur profond" }),
        ("StartupAnalysis", "startup_analyze", "Analyse du démarrage", new[] { "analyse le démarrage", "analyse démarrage", "startup analysis" }),
        ("StartupOptimize", "startup_optimize", "Optimisation du démarrage", new[] { "optimise le démarrage", "startup optimize" }),
        ("WindowsUpdateCheck", "windows_update", "Vérification mises à jour Windows", new[] { "vérifie windows update", "check windows update", "vérifie les mises à jour" }),
        ("WindowsUpdateInstall", "windows_update", "Installation mises à jour Windows", new[] { "mets à jour windows", "installe les mises à jour", "windows update" }),
        ("DriverScan", "drivers_scan", "Scan des pilotes", new[] { "vérifie les pilotes", "scan pilotes", "driver scan" }),
        ("DriverInstall", "drivers_install", "Installation des pilotes", new[] { "installe les pilotes", "driver install" }),
        ("LaunchRambo", "rambo_repair", "Mode RAMBO", new[] { "lance rambo", "mode rambo", "rambo" }),
        ("NetworkDiagnostic", "network_diag", "Diagnostic réseau", new[] { "diagnostic réseau", "vérifie le réseau", "network diagnostic" }),
        ("NetworkSoftReset", "network_soft_reset", "Reset réseau léger", new[] { "reset réseau soft", "network soft reset", "réinitialise le réseau" }),
        ("NetworkFullReset", "network_hard_reset", "Reset réseau complet", new[] { "reset réseau complet", "network full reset", "hard reset réseau" })
    };

    public ActionIntent? Parse(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var normalized = message.Trim().ToLowerInvariant();
        var match = Patterns.FirstOrDefault(pattern => pattern.Keywords.Any(normalized.Contains));
        return string.IsNullOrWhiteSpace(match.IntentName)
            ? null
            : new ActionIntent(match.IntentName, match.ActionId, match.DisplayName);
    }
}
