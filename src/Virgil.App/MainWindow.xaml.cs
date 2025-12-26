// AUTO-PATCH: pass ChatService into MainViewModel ctor
using System.Windows;
using Virgil.App.Chat;
using Virgil.App.ViewModels;
using Virgil.App.Services;
using Virgil.Domain;
using Virgil.Services.Narration;

namespace Virgil.App
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            var chat = new ChatService();

            var monitoringVm = new MonitoringViewModel(
                new MonitoringService(),
                new SettingsService(),
                new NetworkInsightService()
            );

            // Services d'actions
            var systemActionsService = new SystemActionsService();
            var networkActionsService = new NetworkActionsService();
            var performanceActionsService = new PerformanceActionsService();
            var specialActionsService = new SpecialActionsService(chat, new SettingsService(), new MonitoringService());
            var phraseService = new VirgilPhraseService();
            var narrationService = new VirgilNarrationService(chat, phraseService);

            var actionsVm = new ActionsViewModel(
                systemActionsService,
                narrationService,
                networkActionsService,
                performanceActionsService,
                specialActionsService
            );

            DataContext = new MainViewModel(chat, monitoringVm, actionsVm);
        }
    }
}
