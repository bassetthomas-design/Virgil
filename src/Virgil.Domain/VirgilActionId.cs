namespace Virgil.Domain.Actions;

public enum VirgilActionId
{
    // Maintenance rapide
    ScanSystemExpress,          // 1
    QuickClean,                 // 2
    LightBrowserClean,          // 3
    SoftRamFlush,               // 4
    SystemTempCleanup,          // 5

    // Maintenance avancée
    AdvancedDiskClean,          // 6
    DiskCheck,                  // 7
    SystemIntegrityCheck,       // 8
    DeepBrowserClean,           // 9

    // Réseau & Internet
    NetworkQuickDiag,           // 10
    NetworkSoftReset,           // 11
    NetworkAdvancedReset,       // 12
    LatencyStabilityTest,       // 13

    // Gaming / Performance
    EnableGamingMode,           // 14
    RestoreNormalMode,          // 15
    StartupAnalysis,            // 16
    CloseGamingSession,         // 17

    // Mises à jour
    UpdateSoftwares,            // 18
    ManageAutomaticUpdates,     // 19

    // Diagnostic matériel
    HardwareQuickDiagnostic,    // 20

    // Mises à jour (suite)
    RunWindowsUpdate,           // 21
    CheckGpuDrivers,            // 22

    // Spéciaux
    RamboMode,                  // 23
    ThanosChatWipe,             // 24
    ReloadConfiguration,        // 25
    RescanSystem                // 26
}
