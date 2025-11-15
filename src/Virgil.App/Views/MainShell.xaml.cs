using System;
using System.Windows;
using Virgil.App.Interfaces;
using Virgil.App.ViewModels;
using Virgil.App.Chat;
using Virgil.Domain.Actions;
using Virgil.Services;
using Virgil.Services.Abstractions;

namespace Virgil.App.Views
{
    public partial class MainShell : Window, IActionInvoker
    {
        private readonly ChatService _chatService;
        private readonly IActionOrchestrator _orchestrator;

        public ChatViewModel Chat { get; }
        public MonitoringViewModel Monitoring { get; }
        public IActionInvoker Actions => this;

        public MainShell()
        {
            InitializeComponent();

            // Chat & view models
            _chatService = new ChatService();
            Chat = new ChatViewModel(_chatService);
            Monitoring = new MonitoringViewModel();

            // Services & orchestrateur (stubs pour l’instant)
            var cleanup     = new CleanupService();
            var update      = new UpdateService();
            var network     = new NetworkService();
            var performance = new PerformanceService();
            var diagnostic  = new DiagnosticService();
            var special     = new SpecialService();

            _orchestrator = new ActionOrchestrator(
                cleanup,
                update,
                network,
                performance,
                diagnostic,
                special);

            DataContext = this;
        }

        public async void InvokeAction(string actionId)
        {
            if (string.IsNullOrWhiteSpace(actionId))
                return;

            try
            {
                switch (actionId)
                {
                    // Maintenance rapide
                    case "quick_scan":
                        await _chatService.Post("Scan système express lancé.", MessageType.Info);
                        await _orchestrator.RunAsync(VirgilActionId.ScanSystemExpress);
                        break;

                    case "quick_clean":
                        await _chatService.Post("Nettoyage rapide en cours.", MessageType.Info);
                        await _orchestrator.RunAsync(VirgilActionId.QuickClean);
                        break;

                    case "browser_soft_clean":
                        await _chatService.Post("Nettoyage navigateur (léger) en cours.", MessageType.Info);
                        await _orchestrator.RunAsync(VirgilActionId.LightBrowserClean);
                        break;

                    case "ram_soft_free":
                        await _chatService.Post("Libération de la RAM (soft).", MessageType.Info);
                        await _orchestrator.RunAsync(VirgilActionId.SoftRamFlush);
                        break;

                    // Maintenance avancée
                    case "deep_disk_clean":
                        await _chatService.Post("Nettoyage disque avancé en cours.", MessageType.Info);
                        await _orchestrator.RunAsync(VirgilActionId.AdvancedDiskClean);
                        break;

                    case "disk_check":
                        await _chatService.Post("Vérification du disque en cours.", MessageType.Info);
                        await _orchestrator.RunAsync(VirgilActionId.DiskCheck);
                        break;

                    case "system_integrity":
                        await _chatService.Post("Vérification de l’intégrité système en cours.", MessageType.Info);
                        await _orchestrator.RunAsync(VirgilActionId.SystemIntegrityCheck);
                        break;

                    case "browser_deep_clean":
                        await _chatService.Post("Nettoyage navigateur (profond) en cours.", MessageType.Info);
                        await _orchestrator.RunAsync(VirgilActionId.DeepBrowserClean);
                        break;

                    // Réseau & Internet
                    case "network_diag":
                        await _chatService.Post("Diagnostic réseau rapide en cours.", MessageType.Info);
                        await _orchestrator.RunAsync(VirgilActionId.NetworkQuickDiag);
                        break;

                    case "network_soft_reset":
                        await _chatService.Post("Réinitialisation réseau (soft) en cours.", MessageType.Info);
                        await _orchestrator.RunAsync(VirgilActionId.NetworkSoftReset);
                        break;

                    case "network_hard_reset":
                        await _chatService.Post("Réinitialisation réseau avancée en cours.", MessageType.Info);
                        await _orchestrator.RunAsync(VirgilActionId.NetworkAdvancedReset);
                        break;

                    case "network_latency_test":
                        await _chatService.Post("Test de latence / stabilité réseau en cours.", MessageType.Info);
                        await _orchestrator.RunAsync(VirgilActionId.LatencyStabilityTest);
                        break;

                    // Gaming / Performance
                    case "perf_mode_on":
                        await _chatService.Post("Activation du mode Performance / Gaming.", MessageType.Info);
                        await _orchestrator.RunAsync(VirgilActionId.EnableGamingMode);
                        break;

                    case "perf_mode_off":
                        await _chatService.Post("Retour au mode normal.", MessageType.Info);
                        await _orchestrator.RunAsync(VirgilActionId.RestoreNormalMode);
                        break;

                    case "startup_analyze":
                        await _chatService.Post("Analyse des programmes au démarrage en cours.", MessageType.Info);
                        await _orchestrator.RunAsync(VirgilActionId.StartupAnalysis);
                        break;

                    case "gaming_kill_session":
                        await _chatService.Post("Fermeture de la session gaming en cours.", MessageType.Info);
                        await _orchestrator.RunAsync(VirgilActionId.CloseGamingSession);
                        break;

                    // Mises à jour
                    case "apps_update_all":
                        await _chatService.Post("Mise à jour des applications installées.", MessageType.Info);
                        await _orchestrator.RunAsync(VirgilActionId.UpdateSoftwares);
                        break;

                    case "windows_update":
                        await _chatService.Post("Recherche de mises à jour Windows.", MessageType.Info);
                        await _orchestrator.RunAsync(VirgilActionId.RunWindowsUpdate);
                        break;

                    case "gpu_driver_check":
                        await _chatService.Post("Vérification des drivers GPU.", MessageType.Info);
                        await _orchestrator.RunAsync(VirgilActionId.CheckGpuDrivers);
                        break;

                    // Spéciaux
                    case "rambo_repair":
                        await _chatService.Post("Mode RAMBO activé : réparation de l’explorateur et purge du cache d’icônes.", MessageType.Info);
                        await _orchestrator.RunAsync(VirgilActionId.RamboMode);
                        break;

                    case "chat_thanos":
                        _chatService.ClearAll();
                        await _chatService.Post("Snap ! Tous les messages ont disparu.", MessageType.Info);
                        break;

                    case "app_reload_settings":
                        await _chatService.Post("Rechargement de la configuration.", MessageType.Info);
                        await _orchestrator.RunAsync(VirgilActionId.ReloadConfiguration);
                        break;

                    case "monitoring_rescan":
                        await _chatService.Post("Re-scan du monitoring système lancé.", MessageType.Info);
                        await _orchestrator.RunAsync(VirgilActionId.RescanSystem);
                        break;

                    default:
                        await _chatService.Post($"Action inconnue : {actionId}", MessageType.Warning);
                        break;
                }
            }
            catch (Exception ex)
            {
                await _chatService.Post($"Erreur pendant l’exécution de l’action {actionId} : {ex.Message}", MessageType.Error);
            }
        }

        private void OnOpenSettings(object sender, RoutedEventArgs e)
        {
            // TODO: inject SettingsService and open dialog when wired
        }

        private void OnHudToggled(object sender, RoutedEventArgs e)
        {
            // TODO: toggle mini HUD visibility via AppSettings once wired
        }
    }
}
