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
    RunWindowsUpdate,           // 20
    CheckGpuDrivers,            // 21

    // Spéciaux
    RamboMode,                  // 22
    ThanosChatWipe,             // 23
    ReloadConfiguration,        // 24
    RescanSystem                // 25
}
