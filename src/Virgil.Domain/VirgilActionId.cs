namespace Virgil.Domain.Actions;

public enum VirgilActionId
{
    // Maintenance rapide
    ScanSystemExpress,          // 1
    QuickClean,                 // 2
    LightBrowserClean,          // 3
    SoftRamFlush,               // 4

    // Maintenance avancée
    AdvancedDiskClean,          // 5
    DiskCheck,                  // 6
    SystemIntegrityCheck,       // 7
    DeepBrowserClean,           // 8

    // Réseau & Internet
    NetworkQuickDiag,           // 9
    NetworkSoftReset,           // 10
    NetworkAdvancedReset,       // 11
    LatencyStabilityTest,       // 12

    // Gaming / Performance
    EnableGamingMode,           // 13
    RestoreNormalMode,          // 14
    StartupAnalysis,            // 15
    CloseGamingSession,         // 16

    // Mises à jour
    UpdateSoftwares,            // 17
    RunWindowsUpdate,           // 18
    CheckGpuDrivers,            // 19

    // Spéciaux
    RamboMode,                  // 20
    ThanosChatWipe,             // 21
    ReloadConfiguration,        // 22
    RescanSystem                // 23
}
