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
    InternetSpeedTest,          // 14

    // Gaming / Performance
    EnableGamingMode,           // 15
    RestoreNormalMode,          // 16
    StartupAnalysis,            // 17
    CloseGamingSession,         // 18

    // Mises à jour
    UpdateSoftwares,            // 19
    ManageAutomaticUpdates,     // 20

    // Diagnostic matériel
    HardwareQuickDiagnostic,    // 21

    // Mises à jour (suite)
    RunWindowsUpdate,           // 22
    DriverScan,                 // 23
    DriverInstall,              // 24

    // Spéciaux
    RamboMode,                  // 25
    ThanosChatWipe,             // 26
    ReloadConfiguration,        // 27
    RescanSystem                // 28
}
