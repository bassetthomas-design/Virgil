using System.Windows;
using Virgil.App.Interfaces;
using Virgil.App.ViewModels;
using Virgil.App.Chat;

namespace Virgil.App.Views
{
    public partial class MainShell : Window, IActionInvoker
    {
        private readonly ChatService _chatService;
        public ChatViewModel Chat { get; }
        public MonitoringViewModel Monitoring { get; }
        public IActionInvoker Actions => this;

        public MainShell()
        {
            InitializeComponent();
            _chatService = new ChatService();
            Chat = new ChatViewModel(_chatService);
            Monitoring = new MonitoringViewModel();
            DataContext = this;
            Loaded += (_, __) => AddToolbarExtras();
        }

        public void InvokeAction(string actionId)
        {
            if (string.IsNullOrWhiteSpace(actionId))
            {
                return;
            }

            // Pour l’instant : routage simple affichant un message dans le chat.
            switch (actionId)
            {
                case "quick_scan":
                    _chatService.Post("Scan système express lancé.", MessageType.Info);
                    break;
                case "quick_clean":
                    _chatService.Post("Nettoyage rapide en cours.", MessageType.Info);
                    break;
                case "browser_soft_clean":
                    _chatService.Post("Nettoyage navigateur (léger) en cours.", MessageType.Info);
                    break;
                case "ram_soft_free":
                    _chatService.Post("Libération de la RAM (soft).", MessageType.Info);
                    break;
                case "deep_disk_clean":
                    _chatService.Post("Nettoyage disque avancé en cours.", MessageType.Info);
                    break;
                case "disk_check":
                    _chatService.Post("Vérification du disque en cours.", MessageType.Info);
                    break;
                case "system_integrity":
                    _chatService.Post("Vérification d'intégrité système en cours.", MessageType.Info);
                    break;
                case "browser_deep_clean":
                    _chatService.Post("Nettoyage navigateur profond en cours.", MessageType.Info);
                    break;
                case "network_diag":
                    _chatService.Post("Diagnostic réseau rapide en cours.", MessageType.Info);
                    break;
                case "network_soft_reset":
                    _chatService.Post("Réinitialisation réseau (soft) en cours.", MessageType.Info);
                    break;
                case "network_hard_reset":
                    _chatService.Post("Réinitialisation réseau (avancée) en cours.", MessageType.Info);
                    break;
                case "network_latency_test":
                    _chatService.Post("Test de latence / stabilité en cours.", MessageType.Info);
                    break;
                case "perf_mode_on":
                    _chatService.Post("Activation du mode Performance / Gaming.", MessageType.Info);
                    break;
                case "perf_mode_off":
                    _chatService.Post("Retour au mode normal.", MessageType.Info);
                    break;
                case "startup_analyze":
                    _chatService.Post("Analyse des programmes au démarrage en cours.", MessageType.Info);
                    break;
                case "gaming_kill_session":
                    _chatService.Post("Fermeture de la session gaming en cours.", MessageType.Info);
                    break;
                case "apps_update_all":
                    _chatService.Post("Mise à jour des applications installées.", MessageType.Info);
                    break;
                case "windows_update":
                    _chatService.Post("Recherche de mises à jour Windows en cours.", MessageType.Info);
                    break;
                case "gpu_driver_check":
                    _chatService.Post("Vérification des drivers graphiques en cours.", MessageType.Info);
                    break;
                case "rambo_repair":
                    _chatService.Post("Mode RAMBO activé : redémarrage de l'explorateur et purge du cache d'icônes.", MessageType.Info);
                    break;
                case "chat_thanos":
                    _chatService.ClearAll();
                    _chatService.Post("Snap ! Tous les messages ont disparu.", MessageType.Info);
                    break;
                case "app_reload_settings":
                    _chatService.Post("Rechargement de la configuration.", MessageType.Info);
                    break;
                case "monitoring_rescan":
                    _chatService.Post("Re-scan du monitoring système lancé.", MessageType.Info);
                    break;
                default:
                    _chatService.Post($"Action inconnue : {actionId}", MessageType.Warning);
                    break;
            }
        }

        // Méthodes existantes, conservées pour la compatibilité
        private void OnOpenSettings(object sender, RoutedEventArgs e)
        {
            // TODO: inject SettingsService and open dialog when wired
            // for now, no-op to keep build green
        }

        private void OnHudToggled(object sender, RoutedEventArgs e)
        {
            // TODO: toggle mini HUD visibility via AppSettings once wired
        }
    }
}
