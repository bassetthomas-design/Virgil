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
    StartupOptimize,            // 18
    StartupRestore,             // 19
    CloseGamingSession,         // 20

    // Mises à jour
    UpdateSoftwares,            // 21
    ManageAutomaticUpdates,     // 22

    // Diagnostic matériel
    HardwareQuickDiagnostic,    // 23

    // Mises à jour (suite)
    RunWindowsUpdate,           // 24
    DriverScan,                 // 25
    DriverInstall,              // 26

    // Spéciaux
    RamboMode,                  // 27
    ThanosChatWipe,             // 28
    ReloadConfiguration,        // 29
    RescanSystem,               // 30

    // Sécurité Windows
    DefenderQuickScan,          // 31
    DefenderFullScan,           // 32
    WindowsMalwareScan          // 33
}
